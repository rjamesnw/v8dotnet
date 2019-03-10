using System;
using System.IO;
using System.Runtime.InteropServices;

namespace V8.Net
{
    // ########################################################################################################################

    public unsafe static partial class V8NetProxy
    {
        // --------------------------------------------------------------------------------------------------------------------

#if NETSTANDARD
        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string fileName);

        public const int RTLD_NOW = 0x002;
        [DllImport("libdl")] // (could be "libdl.so.2" also: https://github.com/mellinoe/nativelibraryloader/issues/2#issuecomment-414476716)
        public static extern IntPtr DLOpen(string fileName, int flags);

        [DllImport("libdl.so.2")]
        public static extern IntPtr DLOpen2(string fileName, int flags);
#endif

        static void TryLoad(string rootPath)
        {
#if NETSTANDARD
            Exception innerEx = null;
            try
            {
                var libname = "V8_Net_Proxy_" + (Environment.Is64BitProcess ? "x64" : "x86");
                var filepath = Path.Combine(rootPath, libname);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    try { DLOpen(filepath + ".dylib", RTLD_NOW); } catch (Exception ex) { innerEx = ex; DLOpen2(filepath + ".dylib", RTLD_NOW); }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    try { DLOpen(filepath + ".os", RTLD_NOW); } catch (Exception ex) { innerEx = ex; DLOpen2(filepath + ".os", RTLD_NOW); }
                else
                    LoadLibrary(filepath + ".dll");
            }
            catch (Exception ex) { throw new DllNotFoundException(ex.GetFullErrorMessage(), innerEx); }
#endif
        }

        static V8NetProxy() // (See also: https://github.com/mellinoe/nativelibraryloader)
        {
            if (Directory.Exists("libs"))
                try { TryLoad(@"libs\"); }
                catch
                {
                }
        }

        // --------------------------------------------------------------------------------------------------------------------

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateV8EngineProxy")]
        public extern static NativeV8EngineProxy* CreateV8EngineProxy64(bool enableDebugging, void* debugMessageDispatcher, int debugPort);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "DestroyV8EngineProxy", ExactSpelling = false)]
        public static extern void DestroyV8EngineProxy64(NativeV8EngineProxy* engine);

        // [DllImport("V8_Net_Proxy_x64", EntryPoint = "WithV8IsolateScope")]
        //? public static extern void WithV8IsolateScope64(NativeV8EngineProxy* engine, Action action);

        // [DllImport("V8_Net_Proxy_x64", EntryPoint = "WithV8ContextScope")]
        //? public static extern void WithV8ContextScope64(NativeV8EngineProxy* engine, Action action);

        // [DllImport("V8_Net_Proxy_x64", EntryPoint = "WithV8HandleScope")]
        //? public static extern void WithV8HandleScope64(NativeV8EngineProxy* engine, Action action);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetFlagsFromString")]
        public static unsafe extern void SetFlagsFromString64(NativeV8EngineProxy* engine, [MarshalAs(UnmanagedType.AnsiBStr)]string name);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "RegisterGCCallback")]
        public static extern void RegisterGCCallback64(NativeV8EngineProxy* engine, V8GarbageCollectionRequestCallback garbageCollectionRequestCallback);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "ForceGC")]
        public static extern void ForceGC64(NativeV8EngineProxy* engine);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "DoIdleNotification")]
        public static extern bool DoIdleNotification64(NativeV8EngineProxy* engine, int hint = 1000);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "V8Execute", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* V8Execute64(NativeV8EngineProxy* engine, string script, string sourceName = null);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "V8Compile", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* V8Compile64(NativeV8EngineProxy* engine, string script, string sourceName = null);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "V8ExecuteCompiledScript")]
        public static extern HandleProxy* V8ExecuteCompiledScript64(NativeV8EngineProxy* engine, HandleProxy* script);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "TerminateExecution")]
        public static extern void TerminateExecution64(NativeV8EngineProxy* engine);

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateObjectTemplateProxy")]
        public static unsafe extern NativeObjectTemplateProxy* CreateObjectTemplateProxy64(NativeV8EngineProxy* engine);

        // Return: NativeObjectTemplateProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "DeleteObjectTemplateProxy")]
        public static extern unsafe void DeleteObjectTemplateProxy64(NativeObjectTemplateProxy* objectTemplateProxy);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetGlobalObjectTemplate")]
        public static unsafe extern HandleProxy* SetGlobalObjectTemplate64(NativeV8EngineProxy* engine, NativeObjectTemplateProxy* proxy);

        // Return: HandleProxy*
        // (Note: returns a handle to the global object created by the context when the object template was set)

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "RegisterNamedPropertyHandlers")]
        public static extern void RegisterNamedPropertyHandlers64(NativeObjectTemplateProxy* proxy,

            ManagedNamedPropertyGetter getter,
            ManagedNamedPropertySetter setter,
            ManagedNamedPropertyQuery query,
            ManagedNamedPropertyDeleter deleter,
            ManagedNamedPropertyEnumerator enumerator);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "RegisterIndexedPropertyHandlers")]
        public static extern void RegisterIndexedPropertyHandlers64(NativeObjectTemplateProxy* proxy,

            ManagedIndexedPropertyGetter getter,
            ManagedIndexedPropertySetter setter,
            ManagedIndexedPropertyQuery query,
            ManagedIndexedPropertyDeleter deleter,
            ManagedIndexedPropertyEnumerator enumerator);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "UnregisterNamedPropertyHandlers")]
        public static extern void UnregisterNamedPropertyHandlers64(NativeObjectTemplateProxy* proxy);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "UnregisterIndexedPropertyHandlers")]
        public static extern void UnregisterIndexedPropertyHandlers64(NativeObjectTemplateProxy* proxy);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetCallAsFunctionHandler")]
        public static extern void SetCallAsFunctionHandler64(NativeObjectTemplateProxy* proxy, ManagedJSFunctionCallback callback);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateObjectFromTemplate")]
        public static unsafe extern HandleProxy* CreateObjectFromTemplate64(NativeObjectTemplateProxy* objectTemplateProxy, Int32 objID);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "ConnectObject")]
        public static unsafe extern void ConnectObject64(HandleProxy* handleProxy, Int32 objID, void* templateProxy = null);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetObjectPrototype")]
        public static unsafe extern HandleProxy* GetObjectPrototype64(HandleProxy* handleProxy);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "Call", CharSet = CharSet.Unicode)]
        /// <summary>
        /// Calls a property with a given name on a specified object as a function and returns the result.
        /// If the function name is null, then the subject is assumed to be a function object.
        /// </summary>
        public static unsafe extern HandleProxy* Call64(HandleProxy* subject, string functionName, HandleProxy* _this, Int32 argCount, HandleProxy** args);
        // Return: HandleProxy*

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetObjectPropertyByName", CharSet = CharSet.Unicode)]
        public static unsafe extern bool SetObjectPropertyByName64(HandleProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetObjectPropertyByIndex")]
        public static unsafe extern bool SetObjectPropertyByIndex64(HandleProxy* proxy, Int32 index, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetObjectPropertyByName", CharSet = CharSet.Unicode)]
        public static unsafe extern HandleProxy* GetObjectPropertyByName64(HandleProxy* proxy, string name);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetObjectPropertyByIndex")]
        public static unsafe extern HandleProxy* GetObjectPropertyByIndex64(HandleProxy* proxy, Int32 index);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "DeleteObjectPropertyByName", CharSet = CharSet.Unicode)]
        public static unsafe extern bool DeleteObjectPropertyByName64(HandleProxy* proxy, string name);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "DeleteObjectPropertyByIndex")]
        public static unsafe extern bool DeleteObjectPropertyByIndex64(HandleProxy* proxy, Int32 index);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetObjectAccessor", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectAccessor64(HandleProxy* proxy, Int32 managedObjectID, string name,

            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetObjectTemplateAccessor", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectTemplateAccessor64(NativeObjectTemplateProxy* proxy, Int32 managedObjectID, string name,

            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetObjectTemplateProperty", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectTemplateProperty64(NativeObjectTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetPropertyNames")]
        public static unsafe extern HandleProxy* GetPropertyNames64(HandleProxy* proxy);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetOwnPropertyNames")]
        public static unsafe extern HandleProxy* GetOwnPropertyNames64(HandleProxy* proxy);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetPropertyAttributes", CharSet = CharSet.Unicode)]
        public static unsafe extern V8PropertyAttributes GetPropertyAttributes64(HandleProxy* proxy, string name);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetArrayLength")]
        public static unsafe extern Int32 GetArrayLength64(HandleProxy* proxy);


        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateFunctionTemplateProxy", CharSet = CharSet.Unicode)]
        public static unsafe extern NativeFunctionTemplateProxy* CreateFunctionTemplateProxy64(NativeV8EngineProxy* engine, string className, ManagedJSFunctionCallback callback);

        // Return: NativeFunctionTemplateProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "DeleteFunctionTemplateProxy")]
        public static extern unsafe void DeleteFunctionTemplateProxy64(NativeFunctionTemplateProxy* functionTemplateProxy);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetFunctionInstanceTemplateProxy")]
        public static unsafe extern NativeObjectTemplateProxy* GetFunctionInstanceTemplateProxy64(NativeFunctionTemplateProxy* functionTemplateProxy);

        // Return: NativeObjectTemplateProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetFunctionPrototypeTemplateProxy")]
        public static unsafe extern NativeObjectTemplateProxy* GetFunctionPrototypeTemplateProxy64(NativeFunctionTemplateProxy* functionTemplateProxy);

        // Return: NativeObjectTemplateProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetFunction")]
        public static unsafe extern HandleProxy* GetFunction64(NativeFunctionTemplateProxy* functionTemplateProxy);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateInstanceFromFunctionTemplate")]
        public static unsafe extern HandleProxy* CreateInstanceFromFunctionTemplate64(NativeFunctionTemplateProxy* functionTemplateProxy, Int32 objID, Int32 argCount = 0, HandleProxy** args = null);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "SetFunctionTemplateProperty", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetFunctionTemplateProperty64(NativeFunctionTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);



        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateBoolean")]
        public static extern HandleProxy* CreateBoolean64(NativeV8EngineProxy* engine, bool b);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateInteger")]
        public static extern HandleProxy* CreateInteger64(NativeV8EngineProxy* engine, Int32 num);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateNumber")]
        public static extern HandleProxy* CreateNumber64(NativeV8EngineProxy* engine, double num);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateString", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateString64(NativeV8EngineProxy* engine, string str);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateError", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateError64(NativeV8EngineProxy* engine, string message, JSValueType errorType);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateDate")]
        public static extern HandleProxy* CreateDate64(NativeV8EngineProxy* engine, double ms);

        // Return: HandleProxy*

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateObject")]
        public static extern HandleProxy* CreateObject64(NativeV8EngineProxy* engine, Int32 managedObjectID);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateArray")]
        public static extern HandleProxy* CreateArray64(NativeV8EngineProxy* engine, HandleProxy** items = null, Int32 length = 0);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateStringArray", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateStringArray64(NativeV8EngineProxy* engine, char** items, Int32 length = 0);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateNullValue", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateNullValue64(NativeV8EngineProxy* engine);


        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "MakeWeakHandle")]
        public static extern void MakeWeakHandle64(HandleProxy* handleProxy);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "MakeStrongHandle")]
        public static extern void MakeStrongHandle64(HandleProxy* handleProxy);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "DisposeHandleProxy")]
        public static extern void DisposeHandleProxy64(HandleProxy* handle);

        // (required for disposing of the associated V8 handle marshalled in "_HandleProxy")

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "UpdateHandleValue")]
        public static extern void UpdateHandleValue64(HandleProxy* handle);


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "GetHandleManagedObjectID")]
        public static extern int GetHandleManagedObjectID64(HandleProxy* handle);


        // --------------------------------------------------------------------------------------------------------------------
        // Tests

        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateHandleProxyTest")]
        public static extern HandleProxy* CreateHandleProxyTest64();


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateV8EngineProxyTest")]
        public static extern NativeV8EngineProxy* CreateV8EngineProxyTest64();


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateObjectTemplateProxyTest")]
        public static extern NativeObjectTemplateProxy* CreateObjectTemplateProxyTest64();


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "CreateFunctionTemplateProxyTest")]
        public static extern NativeFunctionTemplateProxy* CreateFunctionTemplateProxyTest64();


        [DllImport("V8_Net_Proxy_x64", EntryPoint = "DeleteTestData")]
        public static extern void DeleteTestData64(void* data);


        // --------------------------------------------------------------------------------------------------------------------
    }

    // ########################################################################################################################
}
