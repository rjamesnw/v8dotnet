using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace V8.Net
{
    // ########################################################################################################################

    public unsafe static partial class V8NetProxy
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string fileName);

        public const int RTLD_NOW = 0x002;
        [DllImport("libdl")] // (could be "libdl.so.2" also: https://github.com/mellinoe/nativelibraryloader/issues/2#issuecomment-414476716)
        public static extern IntPtr DLOpen(string fileName, int flags);

        [DllImport("libdl.so.2")]
        public static extern IntPtr DLOpen2(string fileName, int flags);

        static V8NetProxy() // (See also: https://github.com/mellinoe/nativelibraryloader)
        {
            //var libname = "V8_Net_Proxy." + (Environment.Is64BitProcess ? "x64" : "x86");
            ////Loader.ResolveDependencies();
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            //    try { DLOpen(libname + ".dylib", RTLD_NOW); } catch (Exception ex) { try { DLOpen2(libname + ".dylib", RTLD_NOW); } catch (Exception ex2) { throw new DllNotFoundException(ex2.GetFullErrorMessage(), ex); } }
            //else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            //    try { DLOpen(libname + ".os", RTLD_NOW); } catch (Exception ex) { try { DLOpen2(libname + ".os", RTLD_NOW); } catch (Exception ex2) { throw new DllNotFoundException(ex2.GetFullErrorMessage(), ex); } }
            //else
            //    LoadLibrary(libname + ".dll");
            var exportExtration = new System.Text.RegularExpressions.Regex(@"^\s+(\[DllImport\(""(\w+_(x64))""\)\])[^\u0000]*?extern.*\s[A-Z0-9*]+\s+(\w+)\(.*\);", RegexOptions.Multiline | RegexOptions.IgnoreCase); // (Note: Keep in sync with CodeExtractionCompiler.REGION_PARSER)
            var matches = exportExtration.Matches("");
            var items = matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups).ToArray();
            var gitems = items[0].Cast<System.Text.RegularExpressions.Group>().ToArray();
        }

        // --------------------------------------------------------------------------------------------------------------------
        // DllImport.*extern\s+([^ ]+)\s+(\w+)(.*)
        [DllImport("V8_Net_Proxy_x64")]
        public extern static NativeV8EngineProxy* CreateV8EngineProxy(bool enableDebugging, void* debugMessageDispatcher, int debugPort);
        //public delegate NativeV8EngineProxy* CreateV8EngineProxyFunc(bool enableDebugging, void* debugMessageDispatcher, int debugPort);
        //public static CreateV8EngineProxyFunc CreateV8EngineProxy = (Environment.Is64BitProcess ? (CreateV8EngineProxyFunc)CreateV8EngineProxy64 : CreateV8EngineProxy32);

        [DllImport("V8_Net_Proxy_x64", ExactSpelling = false)]
        public static extern void DestroyV8EngineProxy(NativeV8EngineProxy* engine);

        //        [DllImport("V8_Net_Proxy_x64")]
        //?        public static extern void WithV8IsolateScope(NativeV8EngineProxy* engine, Action action);

        //        [DllImport("V8_Net_Proxy_x64")]
        //?        public static extern void WithV8ContextScope(NativeV8EngineProxy* engine, Action action);

        //        [DllImport("V8_Net_Proxy_x64")]
        //?        public static extern void WithV8HandleScope(NativeV8EngineProxy* engine, Action action);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern void SetFlagsFromString(NativeV8EngineProxy* engine, [MarshalAs(UnmanagedType.AnsiBStr)]string name);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void RegisterGCCallback(NativeV8EngineProxy* engine, V8GarbageCollectionRequestCallback garbageCollectionRequestCallback);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void ForceGC(NativeV8EngineProxy* engine);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern bool DoIdleNotification(NativeV8EngineProxy* engine, int hint = 1000);

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* V8Execute(NativeV8EngineProxy* engine, string script, string sourceName = null);

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* V8Compile(NativeV8EngineProxy* engine, string script, string sourceName = null);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern HandleProxy* V8ExecuteCompiledScript(NativeV8EngineProxy* engine, HandleProxy* script);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void TerminateExecution(NativeV8EngineProxy* engine);

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern NativeObjectTemplateProxy* CreateObjectTemplateProxy(NativeV8EngineProxy* engine);
        // Return: NativeObjectTemplateProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static extern unsafe void DeleteObjectTemplateProxy(NativeObjectTemplateProxy* objectTemplateProxy);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern HandleProxy* SetGlobalObjectTemplate(NativeV8EngineProxy* engine, NativeObjectTemplateProxy* proxy);
        // Return: HandleProxy*
        // (Note: returns a handle to the global object created by the context when the object template was set)

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void RegisterNamedPropertyHandlers(NativeObjectTemplateProxy* proxy,
            ManagedNamedPropertyGetter getter,
            ManagedNamedPropertySetter setter,
            ManagedNamedPropertyQuery query,
            ManagedNamedPropertyDeleter deleter,
            ManagedNamedPropertyEnumerator enumerator);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void RegisterIndexedPropertyHandlers(NativeObjectTemplateProxy* proxy,
            ManagedIndexedPropertyGetter getter,
            ManagedIndexedPropertySetter setter,
            ManagedIndexedPropertyQuery query,
            ManagedIndexedPropertyDeleter deleter,
            ManagedIndexedPropertyEnumerator enumerator);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void UnregisterNamedPropertyHandlers(NativeObjectTemplateProxy* proxy);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void UnregisterIndexedPropertyHandlers(NativeObjectTemplateProxy* proxy);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void SetCallAsFunctionHandler(NativeObjectTemplateProxy* proxy, ManagedJSFunctionCallback callback);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern HandleProxy* CreateObjectFromTemplate(NativeObjectTemplateProxy* objectTemplateProxy, Int32 objID);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern void ConnectObject(HandleProxy* handleProxy, Int32 objID, void* templateProxy = null);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern HandleProxy* GetObjectPrototype(HandleProxy* handleProxy);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        /// <summary>
        /// Calls a property with a given name on a specified object as a function and returns the result.
        /// If the function name is null, then the subject is assumed to be a function object.
        /// </summary>
        public static unsafe extern HandleProxy* Call(HandleProxy* subject, string functionName, HandleProxy* _this, Int32 argCount, HandleProxy** args);
        // Return: HandleProxy*

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern bool SetObjectPropertyByName(HandleProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern bool SetObjectPropertyByIndex(HandleProxy* proxy, Int32 index, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern HandleProxy* GetObjectPropertyByName(HandleProxy* proxy, string name);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern HandleProxy* GetObjectPropertyByIndex(HandleProxy* proxy, Int32 index);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern bool DeleteObjectPropertyByName(HandleProxy* proxy, string name);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern bool DeleteObjectPropertyByIndex(HandleProxy* proxy, Int32 index);

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectAccessor(HandleProxy* proxy, Int32 managedObjectID, string name,
            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectTemplateAccessor(NativeObjectTemplateProxy* proxy, Int32 managedObjectID, string name,
            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectTemplateProperty(NativeObjectTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern HandleProxy* GetPropertyNames(HandleProxy* proxy);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern HandleProxy* GetOwnPropertyNames(HandleProxy* proxy);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern V8PropertyAttributes GetPropertyAttributes(HandleProxy* proxy, string name);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern Int32 GetArrayLength(HandleProxy* proxy);

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern NativeFunctionTemplateProxy* CreateFunctionTemplateProxy(NativeV8EngineProxy* engine, string className, ManagedJSFunctionCallback callback);
        // Return: NativeFunctionTemplateProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static extern unsafe void DeleteFunctionTemplateProxy(NativeFunctionTemplateProxy* functionTemplateProxy);

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern NativeObjectTemplateProxy* GetFunctionInstanceTemplateProxy(NativeFunctionTemplateProxy* functionTemplateProxy);
        // Return: NativeObjectTemplateProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern NativeObjectTemplateProxy* GetFunctionPrototypeTemplateProxy(NativeFunctionTemplateProxy* functionTemplateProxy);
        // Return: NativeObjectTemplateProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern HandleProxy* GetFunction(NativeFunctionTemplateProxy* functionTemplateProxy);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static unsafe extern HandleProxy* CreateInstanceFromFunctionTemplate(NativeFunctionTemplateProxy* functionTemplateProxy, Int32 objID, Int32 argCount = 0, HandleProxy** args = null);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetFunctionTemplateProperty(NativeFunctionTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);


        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64")]
        public static extern HandleProxy* CreateBoolean(NativeV8EngineProxy* engine, bool b);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static extern HandleProxy* CreateInteger(NativeV8EngineProxy* engine, Int32 num);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static extern HandleProxy* CreateNumber(NativeV8EngineProxy* engine, double num);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateString(NativeV8EngineProxy* engine, string str);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateError(NativeV8EngineProxy* engine, string message, JSValueType errorType);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static extern HandleProxy* CreateDate(NativeV8EngineProxy* engine, double ms);
        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64")]
        public static extern HandleProxy* CreateObject(NativeV8EngineProxy* engine, Int32 managedObjectID);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern HandleProxy* CreateArray(NativeV8EngineProxy* engine, HandleProxy** items = null, Int32 length = 0);

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateStringArray(NativeV8EngineProxy* engine, char** items, Int32 length = 0);

        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateNullValue(NativeV8EngineProxy* engine);

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void MakeWeakHandle(HandleProxy* handleProxy);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void MakeStrongHandle(HandleProxy* handleProxy);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void DisposeHandleProxy(HandleProxy* handle);
        // (required for disposing of the associated V8 handle marshalled in "_HandleProxy")

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void UpdateHandleValue(HandleProxy* handle);

        [DllImport("V8_Net_Proxy_x64")]
        public static extern int GetHandleManagedObjectID(HandleProxy* handle);

        // --------------------------------------------------------------------------------------------------------------------
        // Tests

        [DllImport("V8_Net_Proxy_x64")]
        public static extern HandleProxy* CreateHandleProxyTest();

        [DllImport("V8_Net_Proxy_x64")]
        public static extern NativeV8EngineProxy* CreateV8EngineProxyTest();

        [DllImport("V8_Net_Proxy_x64")]
        public static extern NativeObjectTemplateProxy* CreateObjectTemplateProxyTest();

        [DllImport("V8_Net_Proxy_x64")]
        public static extern NativeFunctionTemplateProxy* CreateFunctionTemplateProxyTest();

        [DllImport("V8_Net_Proxy_x64")]
        public static extern void DeleteTestData(void* data);

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ########################################################################################################################
}
