using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
namespace V8.Net
{
    public unsafe static partial class V8NetProxy
    {
        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateV8EngineProxy")]
        public extern static NativeV8EngineProxy* CreateV8EngineProxy32(bool enableDebugging, void* debugMessageDispatcher, int debugPort);
        public delegate NativeV8EngineProxy* CreateV8EngineProxy_ImportFuncType(bool enableDebugging, void* debugMessageDispatcher, int debugPort);
        public static CreateV8EngineProxy_ImportFuncType CreateV8EngineProxy = (Environment.Is64BitProcess ? (CreateV8EngineProxy_ImportFuncType)CreateV8EngineProxy64 : CreateV8EngineProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DestroyV8EngineProxy", ExactSpelling = false)]
        public static extern void DestroyV8EngineProxy32(NativeV8EngineProxy* engine);
        public delegate void DestroyV8EngineProxy_ImportFuncType(NativeV8EngineProxy* engine);
        public static DestroyV8EngineProxy_ImportFuncType DestroyV8EngineProxy = (Environment.Is64BitProcess ? (DestroyV8EngineProxy_ImportFuncType)DestroyV8EngineProxy64 : DestroyV8EngineProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateContext")]
        public extern static NativeContext* CreateContext32(NativeV8EngineProxy* engine, NativeObjectTemplateProxy* templatePoxy);
        public delegate NativeContext* CreateContext_ImportFuncType(NativeV8EngineProxy* engine, NativeObjectTemplateProxy* templatePoxy);
        public static CreateContext_ImportFuncType CreateContext = (Environment.Is64BitProcess ? (CreateContext_ImportFuncType)CreateContext64 : CreateContext32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DeleteContext")]
        public extern static NativeContext* DeleteContext32(NativeContext *context);
        public delegate NativeContext* DeleteContext_ImportFuncType(NativeContext *context);
        public static DeleteContext_ImportFuncType DeleteContext = (Environment.Is64BitProcess ? (DeleteContext_ImportFuncType)DeleteContext64 : DeleteContext32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetContext")]
        public extern static HandleProxy* SetContext32(NativeV8EngineProxy* engine, NativeContext* context);
        public delegate HandleProxy* SetContext_ImportFuncType(NativeV8EngineProxy* engine, NativeContext* context);
        public static SetContext_ImportFuncType SetContext = (Environment.Is64BitProcess ? (SetContext_ImportFuncType)SetContext64 : SetContext32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetFlagsFromString")]
        public static unsafe extern void SetFlagsFromString32(NativeV8EngineProxy* engine, [MarshalAs(UnmanagedType.AnsiBStr)]string name);
        public delegate void SetFlagsFromString_ImportFuncType(NativeV8EngineProxy* engine, [MarshalAs(UnmanagedType.AnsiBStr)]string name);
        public static SetFlagsFromString_ImportFuncType SetFlagsFromString = (Environment.Is64BitProcess ? (SetFlagsFromString_ImportFuncType)SetFlagsFromString64 : SetFlagsFromString32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "RegisterGCCallback")]
        public static extern void RegisterGCCallback32(NativeV8EngineProxy* engine, V8GarbageCollectionRequestCallback garbageCollectionRequestCallback);
        public delegate void RegisterGCCallback_ImportFuncType(NativeV8EngineProxy* engine, V8GarbageCollectionRequestCallback garbageCollectionRequestCallback);
        public static RegisterGCCallback_ImportFuncType RegisterGCCallback = (Environment.Is64BitProcess ? (RegisterGCCallback_ImportFuncType)RegisterGCCallback64 : RegisterGCCallback32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "ForceGC")]
        public static extern void ForceGC32(NativeV8EngineProxy* engine);
        public delegate void ForceGC_ImportFuncType(NativeV8EngineProxy* engine);
        public static ForceGC_ImportFuncType ForceGC = (Environment.Is64BitProcess ? (ForceGC_ImportFuncType)ForceGC64 : ForceGC32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DoIdleNotification")]
        public static extern bool DoIdleNotification32(NativeV8EngineProxy* engine, int hint = 1000);
        public delegate bool DoIdleNotification_ImportFuncType(NativeV8EngineProxy* engine, int hint = 1000);
        public static DoIdleNotification_ImportFuncType DoIdleNotification = (Environment.Is64BitProcess ? (DoIdleNotification_ImportFuncType)DoIdleNotification64 : DoIdleNotification32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "V8Execute", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* V8Execute32(NativeV8EngineProxy* engine, string script, string sourceName = null);
        public delegate HandleProxy* V8Execute_ImportFuncType(NativeV8EngineProxy* engine, string script, string sourceName = null);
        public static V8Execute_ImportFuncType V8Execute = (Environment.Is64BitProcess ? (V8Execute_ImportFuncType)V8Execute64 : V8Execute32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "V8Compile", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* V8Compile32(NativeV8EngineProxy* engine, string script, string sourceName = null);
        public delegate HandleProxy* V8Compile_ImportFuncType(NativeV8EngineProxy* engine, string script, string sourceName = null);
        public static V8Compile_ImportFuncType V8Compile = (Environment.Is64BitProcess ? (V8Compile_ImportFuncType)V8Compile64 : V8Compile32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "V8ExecuteCompiledScript")]
        public static extern HandleProxy* V8ExecuteCompiledScript32(NativeV8EngineProxy* engine, HandleProxy* script);
        public delegate HandleProxy* V8ExecuteCompiledScript_ImportFuncType(NativeV8EngineProxy* engine, HandleProxy* script);
        public static V8ExecuteCompiledScript_ImportFuncType V8ExecuteCompiledScript = (Environment.Is64BitProcess ? (V8ExecuteCompiledScript_ImportFuncType)V8ExecuteCompiledScript64 : V8ExecuteCompiledScript32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "TerminateExecution")]
        public static extern void TerminateExecution32(NativeV8EngineProxy* engine);
        public delegate void TerminateExecution_ImportFuncType(NativeV8EngineProxy* engine);
        public static TerminateExecution_ImportFuncType TerminateExecution = (Environment.Is64BitProcess ? (TerminateExecution_ImportFuncType)TerminateExecution64 : TerminateExecution32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateObjectTemplateProxy")]
        public static unsafe extern NativeObjectTemplateProxy* CreateObjectTemplateProxy32(NativeV8EngineProxy* engine);
        public delegate NativeObjectTemplateProxy* CreateObjectTemplateProxy_ImportFuncType(NativeV8EngineProxy* engine);
        public static CreateObjectTemplateProxy_ImportFuncType CreateObjectTemplateProxy = (Environment.Is64BitProcess ? (CreateObjectTemplateProxy_ImportFuncType)CreateObjectTemplateProxy64 : CreateObjectTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DeleteObjectTemplateProxy")]
        public static extern unsafe bool DeleteObjectTemplateProxy32(NativeObjectTemplateProxy* objectTemplateProxy);
        public delegate bool DeleteObjectTemplateProxy_ImportFuncType(NativeObjectTemplateProxy* objectTemplateProxy);
        public static DeleteObjectTemplateProxy_ImportFuncType DeleteObjectTemplateProxy = (Environment.Is64BitProcess ? (DeleteObjectTemplateProxy_ImportFuncType)DeleteObjectTemplateProxy64 : DeleteObjectTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "RegisterNamedPropertyHandlers")]
        public static extern void RegisterNamedPropertyHandlers32(NativeObjectTemplateProxy* proxy,

            ManagedNamedPropertyGetter getter,
            ManagedNamedPropertySetter setter,
            ManagedNamedPropertyQuery query,
            ManagedNamedPropertyDeleter deleter,
            ManagedNamedPropertyEnumerator enumerator);
        public delegate void RegisterNamedPropertyHandlers_ImportFuncType(NativeObjectTemplateProxy* proxy,

            ManagedNamedPropertyGetter getter,
            ManagedNamedPropertySetter setter,
            ManagedNamedPropertyQuery query,
            ManagedNamedPropertyDeleter deleter,
            ManagedNamedPropertyEnumerator enumerator);
        public static RegisterNamedPropertyHandlers_ImportFuncType RegisterNamedPropertyHandlers = (Environment.Is64BitProcess ? (RegisterNamedPropertyHandlers_ImportFuncType)RegisterNamedPropertyHandlers64 : RegisterNamedPropertyHandlers32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "RegisterIndexedPropertyHandlers")]
        public static extern void RegisterIndexedPropertyHandlers32(NativeObjectTemplateProxy* proxy,

            ManagedIndexedPropertyGetter getter,
            ManagedIndexedPropertySetter setter,
            ManagedIndexedPropertyQuery query,
            ManagedIndexedPropertyDeleter deleter,
            ManagedIndexedPropertyEnumerator enumerator);
        public delegate void RegisterIndexedPropertyHandlers_ImportFuncType(NativeObjectTemplateProxy* proxy,

            ManagedIndexedPropertyGetter getter,
            ManagedIndexedPropertySetter setter,
            ManagedIndexedPropertyQuery query,
            ManagedIndexedPropertyDeleter deleter,
            ManagedIndexedPropertyEnumerator enumerator);
        public static RegisterIndexedPropertyHandlers_ImportFuncType RegisterIndexedPropertyHandlers = (Environment.Is64BitProcess ? (RegisterIndexedPropertyHandlers_ImportFuncType)RegisterIndexedPropertyHandlers64 : RegisterIndexedPropertyHandlers32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "UnregisterNamedPropertyHandlers")]
        public static extern void UnregisterNamedPropertyHandlers32(NativeObjectTemplateProxy* proxy);
        public delegate void UnregisterNamedPropertyHandlers_ImportFuncType(NativeObjectTemplateProxy* proxy);
        public static UnregisterNamedPropertyHandlers_ImportFuncType UnregisterNamedPropertyHandlers = (Environment.Is64BitProcess ? (UnregisterNamedPropertyHandlers_ImportFuncType)UnregisterNamedPropertyHandlers64 : UnregisterNamedPropertyHandlers32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "UnregisterIndexedPropertyHandlers")]
        public static extern void UnregisterIndexedPropertyHandlers32(NativeObjectTemplateProxy* proxy);
        public delegate void UnregisterIndexedPropertyHandlers_ImportFuncType(NativeObjectTemplateProxy* proxy);
        public static UnregisterIndexedPropertyHandlers_ImportFuncType UnregisterIndexedPropertyHandlers = (Environment.Is64BitProcess ? (UnregisterIndexedPropertyHandlers_ImportFuncType)UnregisterIndexedPropertyHandlers64 : UnregisterIndexedPropertyHandlers32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetCallAsFunctionHandler")]
        public static extern void SetCallAsFunctionHandler32(NativeObjectTemplateProxy* proxy, ManagedJSFunctionCallback callback);
        public delegate void SetCallAsFunctionHandler_ImportFuncType(NativeObjectTemplateProxy* proxy, ManagedJSFunctionCallback callback);
        public static SetCallAsFunctionHandler_ImportFuncType SetCallAsFunctionHandler = (Environment.Is64BitProcess ? (SetCallAsFunctionHandler_ImportFuncType)SetCallAsFunctionHandler64 : SetCallAsFunctionHandler32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateObjectFromTemplate")]
        public static unsafe extern HandleProxy* CreateObjectFromTemplate32(NativeObjectTemplateProxy* objectTemplateProxy, Int32 objID);
        public delegate HandleProxy* CreateObjectFromTemplate_ImportFuncType(NativeObjectTemplateProxy* objectTemplateProxy, Int32 objID);
        public static CreateObjectFromTemplate_ImportFuncType CreateObjectFromTemplate = (Environment.Is64BitProcess ? (CreateObjectFromTemplate_ImportFuncType)CreateObjectFromTemplate64 : CreateObjectFromTemplate32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "ConnectObject")]
        public static unsafe extern void ConnectObject32(HandleProxy* handleProxy, Int32 objID, void* templateProxy = null);
        public delegate void ConnectObject_ImportFuncType(HandleProxy* handleProxy, Int32 objID, void* templateProxy = null);
        public static ConnectObject_ImportFuncType ConnectObject = (Environment.Is64BitProcess ? (ConnectObject_ImportFuncType)ConnectObject64 : ConnectObject32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetObjectPrototype")]
        public static unsafe extern HandleProxy* GetObjectPrototype32(HandleProxy* handleProxy);
        public delegate HandleProxy* GetObjectPrototype_ImportFuncType(HandleProxy* handleProxy);
        public static GetObjectPrototype_ImportFuncType GetObjectPrototype = (Environment.Is64BitProcess ? (GetObjectPrototype_ImportFuncType)GetObjectPrototype64 : GetObjectPrototype32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "Call", CharSet = CharSet.Unicode)]
        /// <summary>
        /// Calls a property with a given name on a specified object as a function and returns the result.
        /// If the function name is null, then the subject is assumed to be a function object.
        /// </summary>
        public static unsafe extern HandleProxy* Call32(HandleProxy* subject, string functionName, HandleProxy* _this, Int32 argCount, HandleProxy** args);
        public delegate HandleProxy* Call_ImportFuncType(HandleProxy* subject, string functionName, HandleProxy* _this, Int32 argCount, HandleProxy** args);
        public static Call_ImportFuncType Call = (Environment.Is64BitProcess ? (Call_ImportFuncType)Call64 : Call32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetObjectPropertyByName", CharSet = CharSet.Unicode)]
        public static unsafe extern bool SetObjectPropertyByName32(HandleProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public delegate bool SetObjectPropertyByName_ImportFuncType(HandleProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public static SetObjectPropertyByName_ImportFuncType SetObjectPropertyByName = (Environment.Is64BitProcess ? (SetObjectPropertyByName_ImportFuncType)SetObjectPropertyByName64 : SetObjectPropertyByName32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetObjectPropertyByIndex")]
        public static unsafe extern bool SetObjectPropertyByIndex32(HandleProxy* proxy, Int32 index, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public delegate bool SetObjectPropertyByIndex_ImportFuncType(HandleProxy* proxy, Int32 index, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public static SetObjectPropertyByIndex_ImportFuncType SetObjectPropertyByIndex = (Environment.Is64BitProcess ? (SetObjectPropertyByIndex_ImportFuncType)SetObjectPropertyByIndex64 : SetObjectPropertyByIndex32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetObjectPropertyByName", CharSet = CharSet.Unicode)]
        public static unsafe extern HandleProxy* GetObjectPropertyByName32(HandleProxy* proxy, string name);
        public delegate HandleProxy* GetObjectPropertyByName_ImportFuncType(HandleProxy* proxy, string name);
        public static GetObjectPropertyByName_ImportFuncType GetObjectPropertyByName = (Environment.Is64BitProcess ? (GetObjectPropertyByName_ImportFuncType)GetObjectPropertyByName64 : GetObjectPropertyByName32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetObjectPropertyByIndex")]
        public static unsafe extern HandleProxy* GetObjectPropertyByIndex32(HandleProxy* proxy, Int32 index);
        public delegate HandleProxy* GetObjectPropertyByIndex_ImportFuncType(HandleProxy* proxy, Int32 index);
        public static GetObjectPropertyByIndex_ImportFuncType GetObjectPropertyByIndex = (Environment.Is64BitProcess ? (GetObjectPropertyByIndex_ImportFuncType)GetObjectPropertyByIndex64 : GetObjectPropertyByIndex32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DeleteObjectPropertyByName", CharSet = CharSet.Unicode)]
        public static unsafe extern bool DeleteObjectPropertyByName32(HandleProxy* proxy, string name);
        public delegate bool DeleteObjectPropertyByName_ImportFuncType(HandleProxy* proxy, string name);
        public static DeleteObjectPropertyByName_ImportFuncType DeleteObjectPropertyByName = (Environment.Is64BitProcess ? (DeleteObjectPropertyByName_ImportFuncType)DeleteObjectPropertyByName64 : DeleteObjectPropertyByName32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DeleteObjectPropertyByIndex")]
        public static unsafe extern bool DeleteObjectPropertyByIndex32(HandleProxy* proxy, Int32 index);
        public delegate bool DeleteObjectPropertyByIndex_ImportFuncType(HandleProxy* proxy, Int32 index);
        public static DeleteObjectPropertyByIndex_ImportFuncType DeleteObjectPropertyByIndex = (Environment.Is64BitProcess ? (DeleteObjectPropertyByIndex_ImportFuncType)DeleteObjectPropertyByIndex64 : DeleteObjectPropertyByIndex32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetObjectAccessor", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectAccessor32(HandleProxy* proxy, Int32 managedObjectID, string name,

            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);
        public delegate void SetObjectAccessor_ImportFuncType(HandleProxy* proxy, Int32 managedObjectID, string name,

            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);
        public static SetObjectAccessor_ImportFuncType SetObjectAccessor = (Environment.Is64BitProcess ? (SetObjectAccessor_ImportFuncType)SetObjectAccessor64 : SetObjectAccessor32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetObjectTemplateAccessor", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectTemplateAccessor32(NativeObjectTemplateProxy* proxy, Int32 managedObjectID, string name,

            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);
        public delegate void SetObjectTemplateAccessor_ImportFuncType(NativeObjectTemplateProxy* proxy, Int32 managedObjectID, string name,

            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);
        public static SetObjectTemplateAccessor_ImportFuncType SetObjectTemplateAccessor = (Environment.Is64BitProcess ? (SetObjectTemplateAccessor_ImportFuncType)SetObjectTemplateAccessor64 : SetObjectTemplateAccessor32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetObjectTemplateProperty", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetObjectTemplateProperty32(NativeObjectTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public delegate void SetObjectTemplateProperty_ImportFuncType(NativeObjectTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public static SetObjectTemplateProperty_ImportFuncType SetObjectTemplateProperty = (Environment.Is64BitProcess ? (SetObjectTemplateProperty_ImportFuncType)SetObjectTemplateProperty64 : SetObjectTemplateProperty32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetPropertyNames")]
        public static unsafe extern HandleProxy* GetPropertyNames32(HandleProxy* proxy);
        public delegate HandleProxy* GetPropertyNames_ImportFuncType(HandleProxy* proxy);
        public static GetPropertyNames_ImportFuncType GetPropertyNames = (Environment.Is64BitProcess ? (GetPropertyNames_ImportFuncType)GetPropertyNames64 : GetPropertyNames32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetOwnPropertyNames")]
        public static unsafe extern HandleProxy* GetOwnPropertyNames32(HandleProxy* proxy);
        public delegate HandleProxy* GetOwnPropertyNames_ImportFuncType(HandleProxy* proxy);
        public static GetOwnPropertyNames_ImportFuncType GetOwnPropertyNames = (Environment.Is64BitProcess ? (GetOwnPropertyNames_ImportFuncType)GetOwnPropertyNames64 : GetOwnPropertyNames32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetPropertyAttributes", CharSet = CharSet.Unicode)]
        public static unsafe extern V8PropertyAttributes GetPropertyAttributes32(HandleProxy* proxy, string name);
        public delegate V8PropertyAttributes GetPropertyAttributes_ImportFuncType(HandleProxy* proxy, string name);
        public static GetPropertyAttributes_ImportFuncType GetPropertyAttributes = (Environment.Is64BitProcess ? (GetPropertyAttributes_ImportFuncType)GetPropertyAttributes64 : GetPropertyAttributes32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetArrayLength")]
        public static unsafe extern Int32 GetArrayLength32(HandleProxy* proxy);
        public delegate Int32 GetArrayLength_ImportFuncType(HandleProxy* proxy);
        public static GetArrayLength_ImportFuncType GetArrayLength = (Environment.Is64BitProcess ? (GetArrayLength_ImportFuncType)GetArrayLength64 : GetArrayLength32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateFunctionTemplateProxy", CharSet = CharSet.Unicode)]
        public static unsafe extern NativeFunctionTemplateProxy* CreateFunctionTemplateProxy32(NativeV8EngineProxy* engine, string className, ManagedJSFunctionCallback callback);
        public delegate NativeFunctionTemplateProxy* CreateFunctionTemplateProxy_ImportFuncType(NativeV8EngineProxy* engine, string className, ManagedJSFunctionCallback callback);
        public static CreateFunctionTemplateProxy_ImportFuncType CreateFunctionTemplateProxy = (Environment.Is64BitProcess ? (CreateFunctionTemplateProxy_ImportFuncType)CreateFunctionTemplateProxy64 : CreateFunctionTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DeleteFunctionTemplateProxy")]
        public static extern unsafe bool DeleteFunctionTemplateProxy32(NativeFunctionTemplateProxy* functionTemplateProxy);
        public delegate bool DeleteFunctionTemplateProxy_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy);
        public static DeleteFunctionTemplateProxy_ImportFuncType DeleteFunctionTemplateProxy = (Environment.Is64BitProcess ? (DeleteFunctionTemplateProxy_ImportFuncType)DeleteFunctionTemplateProxy64 : DeleteFunctionTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetFunctionInstanceTemplateProxy")]
        public static unsafe extern NativeObjectTemplateProxy* GetFunctionInstanceTemplateProxy32(NativeFunctionTemplateProxy* functionTemplateProxy);
        public delegate NativeObjectTemplateProxy* GetFunctionInstanceTemplateProxy_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy);
        public static GetFunctionInstanceTemplateProxy_ImportFuncType GetFunctionInstanceTemplateProxy = (Environment.Is64BitProcess ? (GetFunctionInstanceTemplateProxy_ImportFuncType)GetFunctionInstanceTemplateProxy64 : GetFunctionInstanceTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetFunctionPrototypeTemplateProxy")]
        public static unsafe extern NativeObjectTemplateProxy* GetFunctionPrototypeTemplateProxy32(NativeFunctionTemplateProxy* functionTemplateProxy);
        public delegate NativeObjectTemplateProxy* GetFunctionPrototypeTemplateProxy_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy);
        public static GetFunctionPrototypeTemplateProxy_ImportFuncType GetFunctionPrototypeTemplateProxy = (Environment.Is64BitProcess ? (GetFunctionPrototypeTemplateProxy_ImportFuncType)GetFunctionPrototypeTemplateProxy64 : GetFunctionPrototypeTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetFunction")]
        public static unsafe extern HandleProxy* GetFunction32(NativeFunctionTemplateProxy* functionTemplateProxy);
        public delegate HandleProxy* GetFunction_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy);
        public static GetFunction_ImportFuncType GetFunction = (Environment.Is64BitProcess ? (GetFunction_ImportFuncType)GetFunction64 : GetFunction32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateInstanceFromFunctionTemplate")]
        public static unsafe extern HandleProxy* CreateInstanceFromFunctionTemplate32(NativeFunctionTemplateProxy* functionTemplateProxy, Int32 objID, Int32 argCount = 0, HandleProxy** args = null);
        public delegate HandleProxy* CreateInstanceFromFunctionTemplate_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy, Int32 objID, Int32 argCount = 0, HandleProxy** args = null);
        public static CreateInstanceFromFunctionTemplate_ImportFuncType CreateInstanceFromFunctionTemplate = (Environment.Is64BitProcess ? (CreateInstanceFromFunctionTemplate_ImportFuncType)CreateInstanceFromFunctionTemplate64 : CreateInstanceFromFunctionTemplate32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "SetFunctionTemplateProperty", CharSet = CharSet.Unicode)]
        public static unsafe extern void SetFunctionTemplateProperty32(NativeFunctionTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public delegate void SetFunctionTemplateProperty_ImportFuncType(NativeFunctionTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public static SetFunctionTemplateProperty_ImportFuncType SetFunctionTemplateProperty = (Environment.Is64BitProcess ? (SetFunctionTemplateProperty_ImportFuncType)SetFunctionTemplateProperty64 : SetFunctionTemplateProperty32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateBoolean")]
        public static extern HandleProxy* CreateBoolean32(NativeV8EngineProxy* engine, bool b);
        public delegate HandleProxy* CreateBoolean_ImportFuncType(NativeV8EngineProxy* engine, bool b);
        public static CreateBoolean_ImportFuncType CreateBoolean = (Environment.Is64BitProcess ? (CreateBoolean_ImportFuncType)CreateBoolean64 : CreateBoolean32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateInteger")]
        public static extern HandleProxy* CreateInteger32(NativeV8EngineProxy* engine, Int32 num);
        public delegate HandleProxy* CreateInteger_ImportFuncType(NativeV8EngineProxy* engine, Int32 num);
        public static CreateInteger_ImportFuncType CreateInteger = (Environment.Is64BitProcess ? (CreateInteger_ImportFuncType)CreateInteger64 : CreateInteger32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateNumber")]
        public static extern HandleProxy* CreateNumber32(NativeV8EngineProxy* engine, double num);
        public delegate HandleProxy* CreateNumber_ImportFuncType(NativeV8EngineProxy* engine, double num);
        public static CreateNumber_ImportFuncType CreateNumber = (Environment.Is64BitProcess ? (CreateNumber_ImportFuncType)CreateNumber64 : CreateNumber32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateString", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateString32(NativeV8EngineProxy* engine, string str);
        public delegate HandleProxy* CreateString_ImportFuncType(NativeV8EngineProxy* engine, string str);
        public static CreateString_ImportFuncType CreateString = (Environment.Is64BitProcess ? (CreateString_ImportFuncType)CreateString64 : CreateString32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateError", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateError32(NativeV8EngineProxy* engine, string message, JSValueType errorType);
        public delegate HandleProxy* CreateError_ImportFuncType(NativeV8EngineProxy* engine, string message, JSValueType errorType);
        public static CreateError_ImportFuncType CreateError = (Environment.Is64BitProcess ? (CreateError_ImportFuncType)CreateError64 : CreateError32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateDate")]
        public static extern HandleProxy* CreateDate32(NativeV8EngineProxy* engine, double ms);
        public delegate HandleProxy* CreateDate_ImportFuncType(NativeV8EngineProxy* engine, double ms);
        public static CreateDate_ImportFuncType CreateDate = (Environment.Is64BitProcess ? (CreateDate_ImportFuncType)CreateDate64 : CreateDate32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateObject")]
        public static extern HandleProxy* CreateObject32(NativeV8EngineProxy* engine, Int32 managedObjectID);
        public delegate HandleProxy* CreateObject_ImportFuncType(NativeV8EngineProxy* engine, Int32 managedObjectID);
        public static CreateObject_ImportFuncType CreateObject = (Environment.Is64BitProcess ? (CreateObject_ImportFuncType)CreateObject64 : CreateObject32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateArray")]
        public static extern HandleProxy* CreateArray32(NativeV8EngineProxy* engine, HandleProxy** items = null, Int32 length = 0);
        public delegate HandleProxy* CreateArray_ImportFuncType(NativeV8EngineProxy* engine, HandleProxy** items = null, Int32 length = 0);
        public static CreateArray_ImportFuncType CreateArray = (Environment.Is64BitProcess ? (CreateArray_ImportFuncType)CreateArray64 : CreateArray32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateStringArray", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateStringArray32(NativeV8EngineProxy* engine, char** items, Int32 length = 0);
        public delegate HandleProxy* CreateStringArray_ImportFuncType(NativeV8EngineProxy* engine, char** items, Int32 length = 0);
        public static CreateStringArray_ImportFuncType CreateStringArray = (Environment.Is64BitProcess ? (CreateStringArray_ImportFuncType)CreateStringArray64 : CreateStringArray32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateNullValue", CharSet = CharSet.Unicode)]
        public static extern HandleProxy* CreateNullValue32(NativeV8EngineProxy* engine);
        public delegate HandleProxy* CreateNullValue_ImportFuncType(NativeV8EngineProxy* engine);
        public static CreateNullValue_ImportFuncType CreateNullValue = (Environment.Is64BitProcess ? (CreateNullValue_ImportFuncType)CreateNullValue64 : CreateNullValue32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "MakeWeakHandle")]
        public static extern void MakeWeakHandle32(HandleProxy* handleProxy);
        public delegate void MakeWeakHandle_ImportFuncType(HandleProxy* handleProxy);
        public static MakeWeakHandle_ImportFuncType MakeWeakHandle = (Environment.Is64BitProcess ? (MakeWeakHandle_ImportFuncType)MakeWeakHandle64 : MakeWeakHandle32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "MakeStrongHandle")]
        public static extern void MakeStrongHandle32(HandleProxy* handleProxy);
        public delegate void MakeStrongHandle_ImportFuncType(HandleProxy* handleProxy);
        public static MakeStrongHandle_ImportFuncType MakeStrongHandle = (Environment.Is64BitProcess ? (MakeStrongHandle_ImportFuncType)MakeStrongHandle64 : MakeStrongHandle32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DisposeHandleProxy")]
        public static extern void DisposeHandleProxy32(HandleProxy* handle);
        public delegate void DisposeHandleProxy_ImportFuncType(HandleProxy* handle);
        public static DisposeHandleProxy_ImportFuncType DisposeHandleProxy = (Environment.Is64BitProcess ? (DisposeHandleProxy_ImportFuncType)DisposeHandleProxy64 : DisposeHandleProxy32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "UpdateHandleValue")]
        public static extern void UpdateHandleValue32(HandleProxy* handle);
        public delegate void UpdateHandleValue_ImportFuncType(HandleProxy* handle);
        public static UpdateHandleValue_ImportFuncType UpdateHandleValue = (Environment.Is64BitProcess ? (UpdateHandleValue_ImportFuncType)UpdateHandleValue64 : UpdateHandleValue32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "GetHandleManagedObjectID")]
        public static extern int GetHandleManagedObjectID32(HandleProxy* handle);
        public delegate int GetHandleManagedObjectID_ImportFuncType(HandleProxy* handle);
        public static GetHandleManagedObjectID_ImportFuncType GetHandleManagedObjectID = (Environment.Is64BitProcess ? (GetHandleManagedObjectID_ImportFuncType)GetHandleManagedObjectID64 : GetHandleManagedObjectID32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateHandleProxyTest")]
        public static extern HandleProxy* CreateHandleProxyTest32();
        public delegate HandleProxy* CreateHandleProxyTest_ImportFuncType();
        public static CreateHandleProxyTest_ImportFuncType CreateHandleProxyTest = (Environment.Is64BitProcess ? (CreateHandleProxyTest_ImportFuncType)CreateHandleProxyTest64 : CreateHandleProxyTest32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateV8EngineProxyTest")]
        public static extern NativeV8EngineProxy* CreateV8EngineProxyTest32();
        public delegate NativeV8EngineProxy* CreateV8EngineProxyTest_ImportFuncType();
        public static CreateV8EngineProxyTest_ImportFuncType CreateV8EngineProxyTest = (Environment.Is64BitProcess ? (CreateV8EngineProxyTest_ImportFuncType)CreateV8EngineProxyTest64 : CreateV8EngineProxyTest32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateObjectTemplateProxyTest")]
        public static extern NativeObjectTemplateProxy* CreateObjectTemplateProxyTest32();
        public delegate NativeObjectTemplateProxy* CreateObjectTemplateProxyTest_ImportFuncType();
        public static CreateObjectTemplateProxyTest_ImportFuncType CreateObjectTemplateProxyTest = (Environment.Is64BitProcess ? (CreateObjectTemplateProxyTest_ImportFuncType)CreateObjectTemplateProxyTest64 : CreateObjectTemplateProxyTest32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "CreateFunctionTemplateProxyTest")]
        public static extern NativeFunctionTemplateProxy* CreateFunctionTemplateProxyTest32();
        public delegate NativeFunctionTemplateProxy* CreateFunctionTemplateProxyTest_ImportFuncType();
        public static CreateFunctionTemplateProxyTest_ImportFuncType CreateFunctionTemplateProxyTest = (Environment.Is64BitProcess ? (CreateFunctionTemplateProxyTest_ImportFuncType)CreateFunctionTemplateProxyTest64 : CreateFunctionTemplateProxyTest32);

        [DllImport("V8_Net_Proxy_x86", EntryPoint = "DeleteTestData")]
        public static extern void DeleteTestData32(void* data);
        public delegate void DeleteTestData_ImportFuncType(void* data);
        public static DeleteTestData_ImportFuncType DeleteTestData = (Environment.Is64BitProcess ? (DeleteTestData_ImportFuncType)DeleteTestData64 : DeleteTestData32);

    }
}
