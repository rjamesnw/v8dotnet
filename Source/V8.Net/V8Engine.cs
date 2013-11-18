/* All V8.NET source is governed by the LGPL licensing model. Please keep these comments intact, thanks.
 * Developer: James Wilkins (jameswilkins.net).
 * Source, Documentation, and Support: https://v8dotnet.codeplex.com
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Web;

namespace V8.Net
{
    // ========================================================================================================================
    // (.NET and Mono Marshalling: http://www.mono-project.com/Interop_with_Native_Libraries)
    // (Mono portable code: http://www.mono-project.com/Guidelines:Application_Portability)
    // (Cross platform p/invoke: http://www.gordonml.co.uk/software-development/mono-net-cross-platform-dynamic-pinvoke/)

    /// <summary>
    /// Creates a new managed V8Engine wrapper instance and associates it with a new native V8 engine.
    /// The engine does not implement locks, so to make it thread safe, you should lock against an engine instance (i.e. lock(myEngine){...}).  The native V8
    /// environment, however, is thread safe (but blocks to allow only one thread at a time).
    /// </summary>
    public unsafe partial class V8Engine : IDisposable
    {
        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Set to the fixed date of Jan 1, 1970. This is used when converting DateTime values to JavaScript Date objects.
        /// </summary>
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A static array of all V8 engines created.
        /// </summary>
        public V8Engine[] Engines { get { return _Engines; } }
        internal static V8Engine[] _Engines = new V8Engine[10];

        void _RegisterEngine(int engineID) // ('engineID' is the managed side engine ID, which starts at 0)
        {
            lock (_Engines)
            {
                if (engineID >= _Engines.Length)
                {
                    Array.Resize(ref _Engines, (5 + engineID) * 2); // (if many engines get allocated, for whatever crazy reason, "*2" creates an exponential capacity increase)
                }
                _Engines[engineID] = this;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        internal NativeV8EngineProxy* _NativeV8EngineProxy;
        V8GarbageCollectionRequestCallback ___V8GarbageCollectionRequestCallback; // (need to keep a reference to this otherwise the reverse p/invoke will fail)
        static readonly object _GlobalLock = new object();

        ObjectTemplate _GlobalObjectTemplateProxy;

        public ObjectHandle GlobalObject { get; private set; }

#if !(V1_1 || V2 || V3 || V3_5)
        public dynamic DynamicGlobalObject { get { return GlobalObject; } }
#endif

        // --------------------------------------------------------------------------------------------------------------------

        static Assembly Resolver(object sender, ResolveEventArgs args)
        {
            if (args.Name.StartsWith("V8.Net.Proxy.Interface"))
            {
                string assemblyRoot = "";

                if (HttpContext.Current != null)
                    assemblyRoot = HttpContext.Current.Server.MapPath("~/bin");
                else
                    assemblyRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                //// ... validate access to the root folder ...
                //var permission = new FileIOPermission(FileIOPermissionAccess.Read, assemblyRoot);
                //var permissionSet = new PermissionSet(PermissionState.None);
                //permissionSet.AddPermission(permission);
                //if (!permissionSet.IsSubsetOf(AppDomain.CurrentDomain.PermissionSet))

                // ... get platform details ...

                var bitStr = IntPtr.Size == 8 ? "x64" : "x86";
                var platformLibraryPath = Path.Combine(assemblyRoot, bitStr);
                var fileName = Path.Combine(platformLibraryPath, "V8.Net.Proxy.Interface." + bitStr + ".dll");

                // ... attempt to update environment variable automatically for the native DLLs ...
                // (see: http://stackoverflow.com/questions/7996263/how-do-i-get-iis-to-load-a-native-dll-referenced-by-my-wcf-service
                //   and http://stackoverflow.com/questions/344608/unmanaged-dlls-fail-to-load-on-asp-net-server)

                try
                {
                    var path = System.Environment.GetEnvironmentVariable("PATH"); // TODO: Detect other systems if necessary.
                    System.Environment.SetEnvironmentVariable("PATH", path + ";" + platformLibraryPath);
                }
                catch { }

                AppDomain.CurrentDomain.AssemblyResolve -= Resolver;  // (handler is only needed once)

                // ... if the current directory has an x86 or x64 folder for the bit depth, automatically change to that folder ...
                // (this is required to load the correct VC++ libraries if made available locally)

                var bitLibFolder = Path.Combine(Directory.GetCurrentDirectory(), bitStr);
                if (Directory.Exists(bitLibFolder))
                    Directory.SetCurrentDirectory(bitLibFolder);

                try
                {
                    return Assembly.LoadFile(fileName);
                }
                catch (Exception ex)
                {
                    var msg = "Failed to load '" + fileName + "'.  V8.NET is running in the '" + bitStr + "' mode.  Some areas to check: \r\n"
                        + "1. The VC++ 2012 redistributable libraries are included, but if missing  for some reason, download and install from the Microsoft Site.\r\n"
                        + "2. Did you download the DLLs from a ZIP file? If done so on Windows, you must open the file properties of the zip file and 'Unblock' it before extracting the files.\r\n"
                        ;
                    if (HttpContext.Current != null)
                        msg += "3. Make sure the path '" + assemblyRoot + "' is accessible to the application pool identity (usually Read & Execute for 'IIS_IUSRS', or a similar user/group)";
                    else
                        msg += "3. Make sure the path '" + assemblyRoot + "' is accessible to the application for loading the required libraries.";
                    throw new InvalidOperationException(msg + "\r\n", ex);
                }
            }

            return null;
        }

        static V8Engine()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolver;
        }

        public V8Engine()
        {
            this.RunMarshallingTests();

            lock (_GlobalLock) // (required because engine proxy instance IDs are tracked on the native side in a static '_DisposedEngines' vector [for quick disposal of handles])
            {
                _NativeV8EngineProxy = V8NetProxy.CreateV8EngineProxy(false, null, 0);

                _RegisterEngine(_NativeV8EngineProxy->ID);

                _GlobalObjectTemplateProxy = CreateObjectTemplate<ObjectTemplate>();
                _GlobalObjectTemplateProxy.UnregisterPropertyInterceptors(); // (it's much faster to use a native object for the global scope)
                GlobalObject = V8NetProxy.SetGlobalObjectTemplate(_NativeV8EngineProxy, _GlobalObjectTemplateProxy._NativeObjectTemplateProxy); // (returns the global object handle)
            }

            ___V8GarbageCollectionRequestCallback = _V8GarbageCollectionRequestCallback;
            V8NetProxy.RegisterGCCallback(_NativeV8EngineProxy, ___V8GarbageCollectionRequestCallback);

            _Initialize_Handles();
            _Initialize_ObjectTemplate();
            _Initialize_Worker(); // (DO THIS LAST!!! - the worker expects everything to be ready)
        }

        ~V8Engine()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_NativeV8EngineProxy != null)
            {
                _TerminateWorker(); // (will return only when it has successfully terminated)

                // ... clear all handles of object IDs for disposal ...

                HandleProxy* hProxy;

                for (var i = 0; i < _HandleProxies.Length; i++)
                {
                    hProxy = _HandleProxies[i];
                    if (hProxy != null && !hProxy->IsDisposed)
                        hProxy->_ObjectID = -2; // (note: this must be <= -2, otherwise the ID auto updates -1 to -2 to flag the ID as already processed)
                }

                // ... allow all objects to be finalized by the GC ...

                ObservableWeakReference<V8NativeObject> weakRef;

                for (var i = 0; i < _Objects.Count; i++)
                    if ((weakRef = _Objects[i]) != null && weakRef.Object != null)
                    {
                        weakRef.Object._ID = null;
                        weakRef.Object.Template = null;
                        weakRef.Object._Handle = ObjectHandle.Empty;
                    }

                // ... destroy the native engine ...

                if (_NativeV8EngineProxy != null)
                {
                    _Engines[_NativeV8EngineProxy->ID] = null; // (notifies any lingering handles that this engine is now gone)
                    V8NetProxy.DestroyV8EngineProxy(_NativeV8EngineProxy);
                    _NativeV8EngineProxy = null;
                }
            }
        }

        /// <summary>
        /// Returns true once this engine has been disposed.
        /// </summary>
        public bool IsDisposed { get { return _NativeV8EngineProxy == null; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calling this method forces an "idle" loop in the native proxy until the V8 engine finishes pending work tasks.
        /// The work performed helps to reduce the memory footprint within the native V8 engine.
        /// <para>(See also: <seealso cref="DoIdleNotification(int)"/>)</para>
        /// <para>Note: You CANNOT GC CLR objects using this method.  This only applies to collection of native V8 handles that are no longer in use.
        /// To *force* the disposal of an object, do this: "{Handle}.ReleaseManagedObject(); {Handle}.Dispose(); GC.Collect();"</para>
        /// </summary>
        public void ForceV8GarbageCollection()
        {
            V8NetProxy.ForceGC(_NativeV8EngineProxy);
        }

        /// <summary>
        /// Calling this method notifies the native V8 engine to perform up to 1000 pending work tasks before returning (this is the default setting in V8).
        /// The work performed helps to reduce the memory footprint within V8.
        /// This helps the garbage collector know when to start collecting objects and values that are no longer in use.
        /// This method returns true if there is still more work pending.
        /// <para>(See also: <seealso cref="ForceV8GarbageCollection()"/>)</para>
        /// </summary>
        /// <param name="hint">Gives the native V8 engine a hint on how much work can be performed before returning (V8's default is 1000 work tasks).</param>
        /// <returns>True if more work is pending.</returns>
        public bool DoIdleNotification(int hint = 1000)
        {
            return V8NetProxy.DoIdleNotification(_NativeV8EngineProxy, hint);
        }

        bool _V8GarbageCollectionRequestCallback(HandleProxy* persistedObjectHandle)
        {
            if (persistedObjectHandle->_ObjectID >= 0)
            {
                var weakRef = _GetObjectWeakReference(persistedObjectHandle->_ObjectID);
                if (weakRef != null)
                    return weakRef.Object._OnNativeGCRequested(); // (notify the object that a V8 GC is requested)
            }
            return true; // (the managed handle doesn't exist, so go ahead and dispose of the native one [the proxy handle])
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Executes JavaScript on the V8 engine and returns the result.
        /// </summary>
        /// <param name="script">The script to run.</param>
        /// <param name="sourceName">A string that identifies the source of the script (handy for debug purposes).</param>
        /// <param name="throwExceptionOnError">If true, and the return value represents an error, an exception is thrown (default is 'false').</param>
        public Handle Execute(string script, string sourceName = "V8.NET", bool throwExceptionOnError = false)
        {
            Handle result = V8NetProxy.V8Execute(_NativeV8EngineProxy, script, sourceName);
            // (note: speed is not an issue when executing whole scripts, so the result is returned in a handle object instead of a value [safer])

            if (throwExceptionOnError)
                result.ThrowOnError();

            return result;
        }

        /// <summary>
        /// Executes JavaScript on the V8 engine and returns the result.
        /// </summary>
        /// <param name="script">The script to run.</param>
        /// <param name="sourceName">A string that identifies the source of the script (handy for debug purposes).</param>
        /// <param name="throwExceptionOnError">If true, and the return value represents an error, an exception is thrown (default is 'false').</param>
        public Handle Execute(Handle script, bool throwExceptionOnError = false)
        {
            if (script.ValueType != JSValueType.Script)
                throw new InvalidOperationException("The handle must represent pre-compiled JavaScript.");

            Handle result = V8NetProxy.V8ExecuteCompiledScript(_NativeV8EngineProxy, script);
            // (note: speed is not an issue when executing whole scripts, so the result is returned in a handle object instead of a value [safer])

            if (throwExceptionOnError)
                result.ThrowOnError();

            return result;
        }

        /// <summary>
        /// Executes JavaScript on the V8 engine and automatically writes the result to the console (only valid for applications that support 'Console' methods).
        /// <para>Note: This is just a shortcut to calling 'Execute()' followed by 'Console.WriteLine()'.</para>
        /// </summary>
        /// <param name="script">The script to run.</param>
        /// <param name="sourceName">A string that identifies the source of the script (handy for debug purposes).</param>
        /// <param name="throwExceptionOnError">If true, and the return value represents an error, an exception is thrown (default is 'false').</param>
        public void ConsoleExecute(string script, string sourceName = "V8.NET", bool throwExceptionOnError = false)
        {
            Console.WriteLine(Execute(script, sourceName, throwExceptionOnError).AsString);
        }

        /// <summary>
        /// Executes JavaScript on the V8 engine and automatically writes the result to the console (only valid for applications that support 'Console' methods).
        /// The script is output to the console window before it gets executed.
        /// <para>Note: This is just a shortcut to calling 'Console.WriteLine(script)', followed by 'ConsoleExecute()'.</para>
        /// </summary>
        /// <param name="script">The script to run.</param>
        /// <param name="sourceName">A string that identifies the source of the script (handy for debug purposes).</param>
        /// <param name="throwExceptionOnError">If true, and the return value represents an error, an exception is thrown (default is 'false').</param>
        public void VerboseConsoleExecute(string script, string sourceName = "V8.NET", bool throwExceptionOnError = false)
        {
            Console.WriteLine(script);
            Console.WriteLine(Execute(script, sourceName, throwExceptionOnError).AsString);
        }

        /// <summary>
        /// Compiles JavaScript on the V8 engine and returns the result.
        /// Since V8 JIT-compiles script every time, repeated tasks can take advantage of re-executing pre-compiled scripts for a speed boost.
        /// </summary>
        /// <param name="script">The script to run.</param>
        /// <param name="sourceName">A string that identifies the source of the script (handy for debug purposes).</param>
        /// <param name="throwExceptionOnError">If true, and the return value represents an error, an exception is thrown (default is 'false').</param>
        /// <returns>A handle to the compiled script.</returns>
        public Handle Compile(string script, string sourceName = "V8.NET", bool throwExceptionOnError = false)
        {
            Handle result = V8NetProxy.V8Compile(_NativeV8EngineProxy, script, sourceName);
            // (note: speed is not an issue when executing whole scripts, so the result is returned in a handle object instead of a value [safer])

            if (throwExceptionOnError)
                result.ThrowOnError();

            return result;
        }

        /// <summary>
        /// Loads a JavaScript file from the current working directory (or specified absolute path) and executes it in the V8 engine, then returns the result.
        /// </summary>
        /// <param name="scriptFile">The script file to load.</param>
        /// <param name="sourceName">A string that identifies the source of the script (handy for debug purposes).</param>
        /// <param name="throwExceptionOnError">If true, and the return value represents an error, or the file fails to load, an exception is thrown (default is 'false').</param>
        public Handle LoadScript(string scriptFile, string sourceName = null, bool throwExceptionOnError = false)
        {
            Handle result;
            try
            {
                var jsText = File.ReadAllText(scriptFile);
                result = Execute(jsText, sourceName ?? scriptFile, throwExceptionOnError);
                if (throwExceptionOnError)
                    result.ThrowOnError();
            }
            catch (Exception ex)
            {
                if (throwExceptionOnError)
                    throw ex;
                result = CreateValue(Exceptions.GetFullErrorMessage(ex));
                result._Handle._HandleProxy->_ValueType = JSValueType.InternalError; // (required to flag that an error has occurred)
            }
            return result;
        }

        /// <summary>
        /// Loads a JavaScript file from the current working directory (or specified absolute path) and compiles it in the V8 engine, then returns the compiled script.
        /// You will need to call 'Execute(...)' with the script handle to execute it.
        /// </summary>
        /// <param name="scriptFile">The script file to load.</param>
        /// <param name="sourceName">A string that identifies the source of the script (handy for debug purposes).</param>
        /// <param name="throwExceptionOnError">If true, and the return value represents an error, or the file fails to load, an exception is thrown (default is 'false').</param>
        public Handle LoadScriptCompiled(string scriptFile, string sourceName = "V8.NET", bool throwExceptionOnError = false)
        {
            Handle result;
            try
            {
                var jsText = File.ReadAllText(scriptFile);
                result = Compile(jsText, sourceName, throwExceptionOnError);
                if (throwExceptionOnError)
                    result.ThrowOnError();
            }
            catch (Exception ex)
            {
                if (throwExceptionOnError)
                    throw ex;
                result = CreateValue(Exceptions.GetFullErrorMessage(ex));
                result._Handle._HandleProxy->_ValueType = JSValueType.InternalError; // (required to flag that an error has occurred)
            }
            return result;
        }
        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a new native V8 ObjectTemplate and associates it with a new managed ObjectTemplate.
        /// <para>Object templates are required in order to generate objects with property interceptors (that is, all property access is redirected to the managed side).</para>
        /// </summary>
        /// <param name="registerPropertyInterceptors">If true (default) then property interceptors (call-backs) will be used to support 'IV8ManagedObject' objects.
        /// <para>Note: Setting this to false provides a huge performance increase because all properties will be stored on the native side only (but 'IV8ManagedObject'
        /// objects created by this template will not intercept property access).</para></param>
        /// <typeparam name="T">Normally this is always 'ObjectTemplate', unless you have a derivation of it.</typeparam>
        public T CreateObjectTemplate<T>(bool registerPropertyInterceptors = true) where T : ObjectTemplate, new()
        {
            var template = new T();
            template._Initialize(this, registerPropertyInterceptors);
            return template;
        }

        /// <summary>
        /// Creates a new native V8 ObjectTemplate and associates it with a new managed ObjectTemplate.
        /// <para>Object templates are required in order to generate objects with property interceptors (that is, all property access is redirected to the managed side).</para>
        /// </summary>
        public ObjectTemplate CreateObjectTemplate() { return CreateObjectTemplate<ObjectTemplate>(); }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a new native V8 FunctionTemplate and associates it with a new managed FunctionTemplate.
        /// <para>Function templates are required in order to associated managed delegates with JavaScript functions within V8.</para>
        /// </summary>
        /// <typeparam name="T">Normally this is always 'FunctionTemplate', unless you have a derivation of it.</typeparam>
        /// <param name="className">The "class name" in V8 is the type name returned when "valueOf()" is used on an object. If this is null then 'V8Function' is assumed.</param>
        /// <param name="callbackSource">A delegate to call when the function is executed.</param>
        public T CreateFunctionTemplate<T>(string className) where T : FunctionTemplate, new()
        {
            var template = new T();
            template._Initialize(this, className ?? typeof(V8Function).Name);
            return template;
        }

        /// <summary>
        /// Creates a new native V8 FunctionTemplate and associates it with a new managed FunctionTemplate.
        /// <para>Function templates are required in order to associated managed delegates with JavaScript functions within V8.</para>
        /// </summary>
        /// <param name="className">The "class name" in V8 is the type name returned when "valueOf()" is used on an object. If this is null (default) then 'V8Function' is assumed.</param>
        /// <param name="callbackSource">A delegate to call when the function is executed.</param>
        public FunctionTemplate CreateFunctionTemplate(string className = null)
        {
            return CreateFunctionTemplate<FunctionTemplate>(className);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the native V8 proxy library to create the value instance for use within the V8 JavaScript environment.
        /// It's ok to use 'WithHandleScope' with this method.
        /// </summary>
        public InternalHandle CreateValue(bool b) { return V8NetProxy.CreateBoolean(_NativeV8EngineProxy, b); }

        /// <summary>
        /// Calls the native V8 proxy library to create a 32-bit integer for use within the V8 JavaScript environment.
        /// </summary>
        public InternalHandle CreateValue(Int32 num) { return V8NetProxy.CreateInteger(_NativeV8EngineProxy, num); }

        /// <summary>
        /// Calls the native V8 proxy library to create a 64-bit number (double) for use within the V8 JavaScript environment.
        /// </summary>
        public InternalHandle CreateValue(double num) { return V8NetProxy.CreateNumber(_NativeV8EngineProxy, num); }

        /// <summary>
        /// Calls the native V8 proxy library to create a string for use within the V8 JavaScript environment.
        /// </summary>
        public InternalHandle CreateValue(string str) { return V8NetProxy.CreateString(_NativeV8EngineProxy, str); }

        /// <summary>
        /// Calls the native V8 proxy library to create an error string for use within the V8 JavaScript environment.
        /// <para>Note: The error flag exists in the associated proxy object only.  If the handle is passed along to another operation, only the string message will get passed.</para>
        /// </summary>
        public InternalHandle CreateError(string message, JSValueType errorType)
        {
            if (errorType >= 0) throw new InvalidOperationException("Invalid error type.");
            return V8NetProxy.CreateError(_NativeV8EngineProxy, message, errorType);
        }

        /// <summary>
        /// Calls the native V8 proxy library to create a date for use within the V8 JavaScript environment.
        /// </summary>
        /// <param name="ms">The number of milliseconds since epoch (Jan 1, 1970). This is the same value as 'SomeDate.getTime()' in JavaScript.</param>
        public InternalHandle CreateValue(TimeSpan ms) { return V8NetProxy.CreateDate(_NativeV8EngineProxy, ms.TotalMilliseconds); }

        /// <summary>
        /// Calls the native V8 proxy library to create a date for use within the V8 JavaScript environment.
        /// </summary>
        public InternalHandle CreateValue(DateTime date) { return CreateValue(date.ToUniversalTime().Subtract(Epoch)); }

        /// <summary>
        /// Wraps a given object handle with a managed object, and optionally associates it with a template instance.
        /// <para>Note: Any other managed object associated with the given handle will cause an error.
        /// You should check '{Handle}.HasManagedObject', or use the "GetObject()" methods to make sure a managed object doesn't already exist.</para>
        /// <para>This was method exists to support the following cases: 1. The V8 context auto-generates the global object, and
        /// 2. V8 function objects are not generated from templates, but still need a managed wrapper.</para>
        /// <para>Note: </para>
        /// </summary>
        /// <typeparam name="T">The wrapper type to create (such as V8ManagedObject).</typeparam>
        /// <param name="v8Object">A handle to a native V8 object.</param>
        /// <param name="initialize">If true (default) then then 'IV8NativeObject.Initialize()' is called on the created object before returning.</param>
        internal T _CreateObject<T>(ITemplate template, InternalHandle v8Object, bool initialize = true, bool connectNativeObject = true)
            where T : V8NativeObject, new()
        {
            try
            {
                if (!v8Object.IsObjectType)
                    throw new InvalidOperationException("An object handle type is required (such as a JavaScript object or function handle).");

                // ... create the new managed JavaScript object, store it (to get the "ID"), and connect it to the native V8 object ...
                var obj = _CreateManagedObject<T>(template, v8Object.PassOn(), connectNativeObject);

                if (initialize)
                    obj.Initialize(false, null);

                return obj;
            }
            finally
            {
                v8Object._DisposeIfFirst();
            }
        }

        /// <summary>
        /// Wraps a given object handle with a managed object.
        /// <para>Note: Any other managed object associated with the given handle will cause an error.
        /// You should check '{Handle}.HasManagedObject', or use the "GetObject()" methods to make sure a managed object doesn't already exist.</para>
        /// <para>This was method exists to support the following cases: 1. The V8 context auto-generates the global object, and
        /// 2. V8 function objects are not generated from templates, but still need a managed wrapper.</para>
        /// <para>Note: </para>
        /// </summary>
        /// <typeparam name="T">The wrapper type to create (such as V8ManagedObject).</typeparam>
        /// <param name="v8Object">A handle to a native V8 object.</param>
        /// <param name="initialize">If true (default) then then 'IV8NativeObject.Initialize()' is called on the created object before returning.</param>
        public T CreateObject<T>(InternalHandle v8Object, bool initialize = true)
            where T : V8NativeObject, new()
        {
            return _CreateObject<T>(null, v8Object, initialize);
        }

        /// <summary>
        /// See <see cref="CreateObject&lt;T>(InternalHandle, bool)"/>.
        /// </summary>
        public V8NativeObject CreateObject(InternalHandle v8Object, bool initialize = true) { return CreateObject<V8NativeObject>(v8Object, initialize); }

        /// <summary>
        /// Creates a new CLR object which will be tracked by a new V8 native object.
        /// </summary>
        /// <param name="initialize">If true (default) then then 'IV8NativeObject.Initialize()' is called on the created wrapper before returning.</param>
        /// <typeparam name="T">A custom 'V8NativeObject' type, or just use 'V8NativeObject' as a default.</typeparam>
        public T CreateObject<T>(bool initialize = true)
            where T : V8NativeObject, new()
        {
            // ... create the new managed JavaScript object and store it (to get the "ID")...
            var obj = _CreateManagedObject<T>(null, null);

            try
            {
                // ... create a new native object and associated it with the new managed object ID ...
                obj._Handle._Set(V8NetProxy.CreateObject(_NativeV8EngineProxy, obj.ID));

                /* The V8 object will have an associated internal field set to the index of the created managed object above for quick lookup.  This index is used
                 * to locate the associated managed object when a call-back occurs. The lookup is a fast O(1) operation using the custom 'IndexedObjectList' manager.
                 */
            }
            catch (Exception ex)
            {
                // ... something went wrong, so remove the new managed object ...
                _RemoveObjectWeakReference(obj.ID);
                throw ex;
            }

            if (initialize)
                obj.Initialize(false, null);

            return (T)obj;
        }

        /// <summary>
        /// Creates a new native V8 object only.
        /// </summary>
        /// <param name="objectID">You can associate arbitrary NEGATIVE numbers with objects to use for tracking purposes.  The numbers have to be less than or
        /// equal to -2. Values greater or equal to 0 are used for internal tracking of V8NativeObject instances. -1 is a default value that is set automatically
        /// when new objects are created (which simply means "no ID is set").</param>
        public InternalHandle CreateObject(Int32 objectID = -2)
        {
            if (objectID > -2) throw new InvalidOperationException("Object IDs must be <= -2.");
            return V8NetProxy.CreateObject(_NativeV8EngineProxy, objectID);
        }

        /// <summary>
        /// Calls the native V8 proxy library to create a JavaScript array for use within the V8 JavaScript environment.
        /// </summary>
        public InternalHandle CreateArray(params InternalHandle[] items)
        {
            HandleProxy** nativeArrayMem = items.Length > 0 ? Utilities.MakeHandleProxyArray(items) : null;

            InternalHandle handle = V8NetProxy.CreateArray(_NativeV8EngineProxy, nativeArrayMem, items.Length);

            Utilities.FreeNativeMemory((IntPtr)nativeArrayMem);

            return handle;
        }

        /// <summary>
        /// Converts an enumeration of values (usually from a collection, list, or array) into a JavaScript array.
        /// By default, an exception will occur if any type cannot be converted.
        /// </summary>
        /// <param name="enumerable">An enumerable object to convert into a native V8 array.</param>
        /// <returns>A native V8 array.</returns>
        public InternalHandle CreateValue(IEnumerable enumerable, bool ignoreErrors = false)
        {
            var values = (enumerable).Cast<object>().ToArray();

            InternalHandle[] handles = new InternalHandle[values.Length];

            for (var i = 0; i < values.Length; i++)
                try { handles[i] = CreateValue(values[i]); }
                catch (Exception ex) { if (!ignoreErrors) throw ex; }

            return CreateArray(handles);
        }

        /// <summary>
        /// Calls the native V8 proxy library to create the value instance for use within the V8 JavaScript environment.
        /// <para>This overload provides a *quick way* to construct an array of strings.
        /// One big memory block is created to marshal the given strings at one time, which is many times faster than having to create an array of individual native strings.</para>
        /// </summary>
        public InternalHandle CreateValue(IEnumerable<string> items)
        {
            if (items == null) return V8NetProxy.CreateArray(_NativeV8EngineProxy, null, 0);

            var itemsEnum = items.GetEnumerator();
            int strBufSize = 0; // (size needed for the string chars portion of the memory block)
            int itemsCount = 0;

            while (itemsEnum.MoveNext())
            {
                // get length of all strings together
                strBufSize += itemsEnum.Current.Length + 1; // (+1 for null char)
                itemsCount++;
            }

            itemsEnum.Reset();

            int strPtrBufSize = Marshal.SizeOf(typeof(IntPtr)) * itemsCount; // start buffer size with size needed for all string pointers.
            char** oneBigStringBlock = (char**)Utilities.AllocNativeMemory(strPtrBufSize + Marshal.SystemDefaultCharSize * strBufSize);
            char** ptrWritePtr = oneBigStringBlock;
            char* strWritePtr = (char*)(((byte*)oneBigStringBlock) + strPtrBufSize);
            int itemLength;

            while (itemsEnum.MoveNext())
            {
                itemLength = itemsEnum.Current.Length;
                Marshal.Copy(itemsEnum.Current.ToCharArray(), 0, (IntPtr)strWritePtr, itemLength);
                Marshal.WriteInt16((IntPtr)(strWritePtr + itemLength), 0);
                Marshal.WriteIntPtr((IntPtr)ptrWritePtr++, (IntPtr)strWritePtr);
                strWritePtr += itemLength + 1;
            }

            InternalHandle handle = V8NetProxy.CreateStringArray(_NativeV8EngineProxy, oneBigStringBlock, itemsCount);

            Utilities.FreeNativeMemory((IntPtr)oneBigStringBlock);

            return handle;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Simply creates and returns a 'null' JavaScript value.
        /// </summary>
        public InternalHandle CreateNullValue() { return V8NetProxy.CreateNullValue(_NativeV8EngineProxy); }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a native V8 JavaScript value that the best represents the given managed value.
        /// Object instance values will be bound to a 'V8NativeObject' wrapper and returned.
        /// To include implicit wrapping of object-type fields and properties for object instances, set 'recursive' to true, otherwise they will be skipped.
        /// <para>Warning: Integers are 32-bit, and Numbers (double) are 64-bit.  This means converting 64-bit integers may result in data loss.</para>
        /// </summary>
        /// <param name="value">One of the supported value types: bool, byte, Int16-64, Single, float, double, string, char, StringBuilder, DateTime, or TimeSpan. (Warning: Int64 will be converted to Int32 [possible data loss])</param>
        /// <param name="recursive">For object instances, if true, then nested objects are included, otherwise only the object itself is bound and returned.
        /// For security reasons, public members that point to object instances will be ignored. This must be true to included those as well, effectively allowing
        /// in-script traversal of the object reference tree (so make sure this doesn't expose sensitive methods/properties/fields).</param>
        /// <param name="memberSecurity">For object instances, these are default flags that describe JavaScript properties for all object instance members that
        /// don't have any 'ScriptMember' attribute.  The flags should be 'OR'd together as needed.</param>
        /// <returns>A native value that best represents the given managed value.</returns>
        public InternalHandle CreateValue(object value, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        {
            if (value == null)
                return CreateNullValue();
            else if (value is IHandleBased)
                return ((IHandleBased)value).AsInternalHandle; // (already a V8.NET value!)
            else if (value is bool)
                return CreateValue((bool)value);
            else if (value is byte)
                return CreateValue((Int32)(byte)value);
            else if (value is sbyte)
                return CreateValue((Int32)(sbyte)value);
            else if (value is Int16)
                return CreateValue((Int32)(Int16)value);
            else if (value is UInt16)
                return CreateValue((Int32)(UInt16)value);
            else if (value is Int32)
                return CreateValue((Int32)value);
            else if (value is UInt32)
                return CreateValue((double)(UInt32)value);
            else if (value is Int64)
                return CreateValue((double)(Int64)value); // (warning: data loss may occur when converting 64int->64float)
            else if (value is UInt64)
                return CreateValue((double)(UInt64)value); // (warning: data loss may occur when converting 64int->64float)
            else if (value is Single)
                return CreateValue((double)(Single)value);
            else if (value is float)
                return CreateValue((double)(float)value);
            else if (value is double)
                return CreateValue((double)value);
            else if (value is string)
                return CreateValue((string)value);
            else if (value is char)
                return CreateValue(((char)value).ToString());
            else if (value is StringBuilder)
                return CreateValue(((StringBuilder)value).ToString());
            else if (value is DateTime)
                return CreateValue((DateTime)value);
            else if (value is TimeSpan)
                return CreateValue((TimeSpan)value);
            else if (value is Enum) // (enums are simply integer like values)
                return CreateValue((int)value);
            else if (value is Array)
                return CreateValue((IEnumerable)value);
            else //??if (value.GetType().IsClass)
                return CreateBinding(value, null, recursive, memberSecurity);

            //??var type = value != null ? value.GetType().Name : "null";
            //??throw new NotSupportedException("Cannot convert object of type '" + type + "' to a JavaScript value.");
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
