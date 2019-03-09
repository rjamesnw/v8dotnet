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
    partial class ProxyLoader
    {
        [DllImport("V8_Net_Proxy_x86")]
        public extern static NativeV8EngineProxy*  CreateV8EngineProxy32(bool enableDebugging, void* debugMessageDispatcher, int debugPort);
        public delegate NativeV8EngineProxy* CreateV8EngineProxy_ImportFuncType(bool enableDebugging, void* debugMessageDispatcher, int debugPort);
        public static CreateV8EngineProxy_ImportFuncType CreateV8EngineProxy = (Environment.Is64BitProcess ? (CreateV8EngineProxy_ImportFuncType)CreateV8EngineProxy32 : CreateV8EngineProxy32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern void  SetFlagsFromString32(NativeV8EngineProxy* engine, [MarshalAs(UnmanagedType.AnsiBStr)]string name);
        public delegate void SetFlagsFromString_ImportFuncType(NativeV8EngineProxy* engine, [MarshalAs(UnmanagedType.AnsiBStr)]string name);
        public static SetFlagsFromString_ImportFuncType SetFlagsFromString = (Environment.Is64BitProcess ? (SetFlagsFromString_ImportFuncType)SetFlagsFromString32 : SetFlagsFromString32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  RegisterGCCallback32(NativeV8EngineProxy* engine, V8GarbageCollectionRequestCallback garbageCollectionRequestCallback);
        public delegate void RegisterGCCallback_ImportFuncType(NativeV8EngineProxy* engine, V8GarbageCollectionRequestCallback garbageCollectionRequestCallback);
        public static RegisterGCCallback_ImportFuncType RegisterGCCallback = (Environment.Is64BitProcess ? (RegisterGCCallback_ImportFuncType)RegisterGCCallback32 : RegisterGCCallback32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  ForceGC32(NativeV8EngineProxy* engine);
        public delegate void ForceGC_ImportFuncType(NativeV8EngineProxy* engine);
        public static ForceGC_ImportFuncType ForceGC = (Environment.Is64BitProcess ? (ForceGC_ImportFuncType)ForceGC32 : ForceGC32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern bool  DoIdleNotification32(NativeV8EngineProxy* engine, int hint = 1000);
        public delegate bool DoIdleNotification_ImportFuncType(NativeV8EngineProxy* engine, int hint = 1000);
        public static DoIdleNotification_ImportFuncType DoIdleNotification = (Environment.Is64BitProcess ? (DoIdleNotification_ImportFuncType)DoIdleNotification32 : DoIdleNotification32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern HandleProxy*  V8ExecuteCompiledScript32(NativeV8EngineProxy* engine, HandleProxy* script);
        public delegate HandleProxy* V8ExecuteCompiledScript_ImportFuncType(NativeV8EngineProxy* engine, HandleProxy* script);
        public static V8ExecuteCompiledScript_ImportFuncType V8ExecuteCompiledScript = (Environment.Is64BitProcess ? (V8ExecuteCompiledScript_ImportFuncType)V8ExecuteCompiledScript32 : V8ExecuteCompiledScript32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  TerminateExecution32(NativeV8EngineProxy* engine);
        public delegate void TerminateExecution_ImportFuncType(NativeV8EngineProxy* engine);
        public static TerminateExecution_ImportFuncType TerminateExecution = (Environment.Is64BitProcess ? (TerminateExecution_ImportFuncType)TerminateExecution32 : TerminateExecution32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern NativeObjectTemplateProxy*  CreateObjectTemplateProxy32(NativeV8EngineProxy* engine);
        public delegate NativeObjectTemplateProxy* CreateObjectTemplateProxy_ImportFuncType(NativeV8EngineProxy* engine);
        public static CreateObjectTemplateProxy_ImportFuncType CreateObjectTemplateProxy = (Environment.Is64BitProcess ? (CreateObjectTemplateProxy_ImportFuncType)CreateObjectTemplateProxy32 : CreateObjectTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern unsafe void  DeleteObjectTemplateProxy32(NativeObjectTemplateProxy* objectTemplateProxy);
        public delegate void DeleteObjectTemplateProxy_ImportFuncType(NativeObjectTemplateProxy* objectTemplateProxy);
        public static DeleteObjectTemplateProxy_ImportFuncType DeleteObjectTemplateProxy = (Environment.Is64BitProcess ? (DeleteObjectTemplateProxy_ImportFuncType)DeleteObjectTemplateProxy32 : DeleteObjectTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern HandleProxy*  SetGlobalObjectTemplate32(NativeV8EngineProxy* engine, NativeObjectTemplateProxy* proxy);
        public delegate HandleProxy* SetGlobalObjectTemplate_ImportFuncType(NativeV8EngineProxy* engine, NativeObjectTemplateProxy* proxy);
        public static SetGlobalObjectTemplate_ImportFuncType SetGlobalObjectTemplate = (Environment.Is64BitProcess ? (SetGlobalObjectTemplate_ImportFuncType)SetGlobalObjectTemplate32 : SetGlobalObjectTemplate32);

        [DllImport("V8_Net_Proxy_x86")]
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
        public static extern void  UnregisterNamedPropertyHandlers32(NativeObjectTemplateProxy* proxy);
        public delegate void UnregisterNamedPropertyHandlers_ImportFuncType(NativeObjectTemplateProxy* proxy);
        public static UnregisterNamedPropertyHandlers_ImportFuncType UnregisterNamedPropertyHandlers = (Environment.Is64BitProcess ? (UnregisterNamedPropertyHandlers_ImportFuncType)UnregisterNamedPropertyHandlers32 : UnregisterNamedPropertyHandlers32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  UnregisterIndexedPropertyHandlers32(NativeObjectTemplateProxy* proxy);
        public delegate void UnregisterIndexedPropertyHandlers_ImportFuncType(NativeObjectTemplateProxy* proxy);
        public static UnregisterIndexedPropertyHandlers_ImportFuncType UnregisterIndexedPropertyHandlers = (Environment.Is64BitProcess ? (UnregisterIndexedPropertyHandlers_ImportFuncType)UnregisterIndexedPropertyHandlers32 : UnregisterIndexedPropertyHandlers32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  SetCallAsFunctionHandler32(NativeObjectTemplateProxy* proxy, ManagedJSFunctionCallback callback);
        public delegate void SetCallAsFunctionHandler_ImportFuncType(NativeObjectTemplateProxy* proxy, ManagedJSFunctionCallback callback);
        public static SetCallAsFunctionHandler_ImportFuncType SetCallAsFunctionHandler = (Environment.Is64BitProcess ? (SetCallAsFunctionHandler_ImportFuncType)SetCallAsFunctionHandler32 : SetCallAsFunctionHandler32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern HandleProxy*  CreateObjectFromTemplate32(NativeObjectTemplateProxy* objectTemplateProxy, Int32 objID);
        public delegate HandleProxy* CreateObjectFromTemplate_ImportFuncType(NativeObjectTemplateProxy* objectTemplateProxy, Int32 objID);
        public static CreateObjectFromTemplate_ImportFuncType CreateObjectFromTemplate = (Environment.Is64BitProcess ? (CreateObjectFromTemplate_ImportFuncType)CreateObjectFromTemplate32 : CreateObjectFromTemplate32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern void  ConnectObject32(HandleProxy* handleProxy, Int32 objID, void* templateProxy = null);
        public delegate void ConnectObject_ImportFuncType(HandleProxy* handleProxy, Int32 objID, void* templateProxy = null);
        public static ConnectObject_ImportFuncType ConnectObject = (Environment.Is64BitProcess ? (ConnectObject_ImportFuncType)ConnectObject32 : ConnectObject32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern HandleProxy*  GetObjectPrototype32(HandleProxy* handleProxy);
        public delegate HandleProxy* GetObjectPrototype_ImportFuncType(HandleProxy* handleProxy);
        public static GetObjectPrototype_ImportFuncType GetObjectPrototype = (Environment.Is64BitProcess ? (GetObjectPrototype_ImportFuncType)GetObjectPrototype32 : GetObjectPrototype32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern bool  SetObjectPropertyByIndex32(HandleProxy* proxy, Int32 index, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public delegate bool SetObjectPropertyByIndex_ImportFuncType(HandleProxy* proxy, Int32 index, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
        public static SetObjectPropertyByIndex_ImportFuncType SetObjectPropertyByIndex = (Environment.Is64BitProcess ? (SetObjectPropertyByIndex_ImportFuncType)SetObjectPropertyByIndex32 : SetObjectPropertyByIndex32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern HandleProxy*  GetObjectPropertyByIndex32(HandleProxy* proxy, Int32 index);
        public delegate HandleProxy* GetObjectPropertyByIndex_ImportFuncType(HandleProxy* proxy, Int32 index);
        public static GetObjectPropertyByIndex_ImportFuncType GetObjectPropertyByIndex = (Environment.Is64BitProcess ? (GetObjectPropertyByIndex_ImportFuncType)GetObjectPropertyByIndex32 : GetObjectPropertyByIndex32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern bool  DeleteObjectPropertyByIndex32(HandleProxy* proxy, Int32 index);
        public delegate bool DeleteObjectPropertyByIndex_ImportFuncType(HandleProxy* proxy, Int32 index);
        public static DeleteObjectPropertyByIndex_ImportFuncType DeleteObjectPropertyByIndex = (Environment.Is64BitProcess ? (DeleteObjectPropertyByIndex_ImportFuncType)DeleteObjectPropertyByIndex32 : DeleteObjectPropertyByIndex32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern HandleProxy*  GetPropertyNames32(HandleProxy* proxy);
        public delegate HandleProxy* GetPropertyNames_ImportFuncType(HandleProxy* proxy);
        public static GetPropertyNames_ImportFuncType GetPropertyNames = (Environment.Is64BitProcess ? (GetPropertyNames_ImportFuncType)GetPropertyNames32 : GetPropertyNames32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern HandleProxy*  GetOwnPropertyNames32(HandleProxy* proxy);
        public delegate HandleProxy* GetOwnPropertyNames_ImportFuncType(HandleProxy* proxy);
        public static GetOwnPropertyNames_ImportFuncType GetOwnPropertyNames = (Environment.Is64BitProcess ? (GetOwnPropertyNames_ImportFuncType)GetOwnPropertyNames32 : GetOwnPropertyNames32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern Int32  GetArrayLength32(HandleProxy* proxy);
        public delegate Int32 GetArrayLength_ImportFuncType(HandleProxy* proxy);
        public static GetArrayLength_ImportFuncType GetArrayLength = (Environment.Is64BitProcess ? (GetArrayLength_ImportFuncType)GetArrayLength32 : GetArrayLength32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern unsafe void  DeleteFunctionTemplateProxy32(NativeFunctionTemplateProxy* functionTemplateProxy);
        public delegate void DeleteFunctionTemplateProxy_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy);
        public static DeleteFunctionTemplateProxy_ImportFuncType DeleteFunctionTemplateProxy = (Environment.Is64BitProcess ? (DeleteFunctionTemplateProxy_ImportFuncType)DeleteFunctionTemplateProxy32 : DeleteFunctionTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern NativeObjectTemplateProxy*  GetFunctionInstanceTemplateProxy32(NativeFunctionTemplateProxy* functionTemplateProxy);
        public delegate NativeObjectTemplateProxy* GetFunctionInstanceTemplateProxy_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy);
        public static GetFunctionInstanceTemplateProxy_ImportFuncType GetFunctionInstanceTemplateProxy = (Environment.Is64BitProcess ? (GetFunctionInstanceTemplateProxy_ImportFuncType)GetFunctionInstanceTemplateProxy32 : GetFunctionInstanceTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern NativeObjectTemplateProxy*  GetFunctionPrototypeTemplateProxy32(NativeFunctionTemplateProxy* functionTemplateProxy);
        public delegate NativeObjectTemplateProxy* GetFunctionPrototypeTemplateProxy_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy);
        public static GetFunctionPrototypeTemplateProxy_ImportFuncType GetFunctionPrototypeTemplateProxy = (Environment.Is64BitProcess ? (GetFunctionPrototypeTemplateProxy_ImportFuncType)GetFunctionPrototypeTemplateProxy32 : GetFunctionPrototypeTemplateProxy32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern HandleProxy*  GetFunction32(NativeFunctionTemplateProxy* functionTemplateProxy);
        public delegate HandleProxy* GetFunction_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy);
        public static GetFunction_ImportFuncType GetFunction = (Environment.Is64BitProcess ? (GetFunction_ImportFuncType)GetFunction32 : GetFunction32);

        [DllImport("V8_Net_Proxy_x86")]
        public static unsafe extern HandleProxy*  CreateInstanceFromFunctionTemplate32(NativeFunctionTemplateProxy* functionTemplateProxy, Int32 objID, Int32 argCount = 0, HandleProxy** args = null);
        public delegate HandleProxy* CreateInstanceFromFunctionTemplate_ImportFuncType(NativeFunctionTemplateProxy* functionTemplateProxy, Int32 objID, Int32 argCount = 0, HandleProxy** args = null);
        public static CreateInstanceFromFunctionTemplate_ImportFuncType CreateInstanceFromFunctionTemplate = (Environment.Is64BitProcess ? (CreateInstanceFromFunctionTemplate_ImportFuncType)CreateInstanceFromFunctionTemplate32 : CreateInstanceFromFunctionTemplate32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern HandleProxy*  CreateBoolean32(NativeV8EngineProxy* engine, bool b);
        public delegate HandleProxy* CreateBoolean_ImportFuncType(NativeV8EngineProxy* engine, bool b);
        public static CreateBoolean_ImportFuncType CreateBoolean = (Environment.Is64BitProcess ? (CreateBoolean_ImportFuncType)CreateBoolean32 : CreateBoolean32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern HandleProxy*  CreateInteger32(NativeV8EngineProxy* engine, Int32 num);
        public delegate HandleProxy* CreateInteger_ImportFuncType(NativeV8EngineProxy* engine, Int32 num);
        public static CreateInteger_ImportFuncType CreateInteger = (Environment.Is64BitProcess ? (CreateInteger_ImportFuncType)CreateInteger32 : CreateInteger32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern HandleProxy*  CreateNumber32(NativeV8EngineProxy* engine, double num);
        public delegate HandleProxy* CreateNumber_ImportFuncType(NativeV8EngineProxy* engine, double num);
        public static CreateNumber_ImportFuncType CreateNumber = (Environment.Is64BitProcess ? (CreateNumber_ImportFuncType)CreateNumber32 : CreateNumber32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern HandleProxy*  CreateDate32(NativeV8EngineProxy* engine, double ms);
        public delegate HandleProxy* CreateDate_ImportFuncType(NativeV8EngineProxy* engine, double ms);
        public static CreateDate_ImportFuncType CreateDate = (Environment.Is64BitProcess ? (CreateDate_ImportFuncType)CreateDate32 : CreateDate32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern HandleProxy*  CreateObject32(NativeV8EngineProxy* engine, Int32 managedObjectID);
        public delegate HandleProxy* CreateObject_ImportFuncType(NativeV8EngineProxy* engine, Int32 managedObjectID);
        public static CreateObject_ImportFuncType CreateObject = (Environment.Is64BitProcess ? (CreateObject_ImportFuncType)CreateObject32 : CreateObject32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern HandleProxy*  CreateArray32(NativeV8EngineProxy* engine, HandleProxy** items = null, Int32 length = 0);
        public delegate HandleProxy* CreateArray_ImportFuncType(NativeV8EngineProxy* engine, HandleProxy** items = null, Int32 length = 0);
        public static CreateArray_ImportFuncType CreateArray = (Environment.Is64BitProcess ? (CreateArray_ImportFuncType)CreateArray32 : CreateArray32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  MakeWeakHandle32(HandleProxy* handleProxy);
        public delegate void MakeWeakHandle_ImportFuncType(HandleProxy* handleProxy);
        public static MakeWeakHandle_ImportFuncType MakeWeakHandle = (Environment.Is64BitProcess ? (MakeWeakHandle_ImportFuncType)MakeWeakHandle32 : MakeWeakHandle32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  MakeStrongHandle32(HandleProxy* handleProxy);
        public delegate void MakeStrongHandle_ImportFuncType(HandleProxy* handleProxy);
        public static MakeStrongHandle_ImportFuncType MakeStrongHandle = (Environment.Is64BitProcess ? (MakeStrongHandle_ImportFuncType)MakeStrongHandle32 : MakeStrongHandle32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  DisposeHandleProxy32(HandleProxy* handle);
        public delegate void DisposeHandleProxy_ImportFuncType(HandleProxy* handle);
        public static DisposeHandleProxy_ImportFuncType DisposeHandleProxy = (Environment.Is64BitProcess ? (DisposeHandleProxy_ImportFuncType)DisposeHandleProxy32 : DisposeHandleProxy32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  UpdateHandleValue32(HandleProxy* handle);
        public delegate void UpdateHandleValue_ImportFuncType(HandleProxy* handle);
        public static UpdateHandleValue_ImportFuncType UpdateHandleValue = (Environment.Is64BitProcess ? (UpdateHandleValue_ImportFuncType)UpdateHandleValue32 : UpdateHandleValue32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern int  GetHandleManagedObjectID32(HandleProxy* handle);
        public delegate int GetHandleManagedObjectID_ImportFuncType(HandleProxy* handle);
        public static GetHandleManagedObjectID_ImportFuncType GetHandleManagedObjectID = (Environment.Is64BitProcess ? (GetHandleManagedObjectID_ImportFuncType)GetHandleManagedObjectID32 : GetHandleManagedObjectID32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern HandleProxy*  CreateHandleProxyTest32();
        public delegate HandleProxy* CreateHandleProxyTest_ImportFuncType();
        public static CreateHandleProxyTest_ImportFuncType CreateHandleProxyTest = (Environment.Is64BitProcess ? (CreateHandleProxyTest_ImportFuncType)CreateHandleProxyTest32 : CreateHandleProxyTest32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern NativeV8EngineProxy*  CreateV8EngineProxyTest32();
        public delegate NativeV8EngineProxy* CreateV8EngineProxyTest_ImportFuncType();
        public static CreateV8EngineProxyTest_ImportFuncType CreateV8EngineProxyTest = (Environment.Is64BitProcess ? (CreateV8EngineProxyTest_ImportFuncType)CreateV8EngineProxyTest32 : CreateV8EngineProxyTest32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern NativeObjectTemplateProxy*  CreateObjectTemplateProxyTest32();
        public delegate NativeObjectTemplateProxy* CreateObjectTemplateProxyTest_ImportFuncType();
        public static CreateObjectTemplateProxyTest_ImportFuncType CreateObjectTemplateProxyTest = (Environment.Is64BitProcess ? (CreateObjectTemplateProxyTest_ImportFuncType)CreateObjectTemplateProxyTest32 : CreateObjectTemplateProxyTest32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern NativeFunctionTemplateProxy*  CreateFunctionTemplateProxyTest32();
        public delegate NativeFunctionTemplateProxy* CreateFunctionTemplateProxyTest_ImportFuncType();
        public static CreateFunctionTemplateProxyTest_ImportFuncType CreateFunctionTemplateProxyTest = (Environment.Is64BitProcess ? (CreateFunctionTemplateProxyTest_ImportFuncType)CreateFunctionTemplateProxyTest32 : CreateFunctionTemplateProxyTest32);

        [DllImport("V8_Net_Proxy_x86")]
        public static extern void  DeleteTestData32(void* data);
        public delegate void DeleteTestData_ImportFuncType(void* data);
        public static DeleteTestData_ImportFuncType DeleteTestData = (Environment.Is64BitProcess ? (DeleteTestData_ImportFuncType)DeleteTestData32 : DeleteTestData32);

    }
}
