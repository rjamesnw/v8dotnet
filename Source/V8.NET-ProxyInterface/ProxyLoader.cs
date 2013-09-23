using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace V8.Net
{
    // ########################################################################################################################
    // WARNING:

    public unsafe static class V8NetProxy
    {
        // --------------------------------------------------------------------------------------------------------------------
#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern NativeV8EngineProxy* CreateV8EngineProxy(bool enableDebugging, void* debugMessageDispatcher, int debugPort);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void DestroyV8EngineProxy(NativeV8EngineProxy* engine);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void WithV8IsolateScope(NativeV8EngineProxy* engine, Action action);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void WithV8ContextScope(NativeV8EngineProxy* engine, Action action);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void WithV8HandleScope(NativeV8EngineProxy* engine, Action action);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void RegisterGCCallback(NativeV8EngineProxy* engine, V8GarbageCollectionRequestCallback garbageCollectionRequestCallback);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void ForceGC(NativeV8EngineProxy* engine);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
#if V1_1 || V2 || V3 || V3_5
        public static extern bool DoIdleNotification(NativeV8EngineProxy* engine, int hint);
#else
        public static extern bool DoIdleNotification(NativeV8EngineProxy* engine, int hint = 1000);
#endif

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static extern HandleProxy* V8Execute(NativeV8EngineProxy* engine, string script, string sourceName = null);

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static extern HandleProxy* V8Compile(NativeV8EngineProxy* engine, string script, string sourceName = null);

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static extern HandleProxy* V8ExecuteCompiledScript(NativeV8EngineProxy* engine, HandleProxy* script);

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern NativeObjectTemplateProxy* CreateObjectTemplateProxy(NativeV8EngineProxy* engine);
        // Return: NativeObjectTemplateProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern unsafe void DeleteObjectTemplateProxy(NativeObjectTemplateProxy* objectTemplateProxy);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern HandleProxy* SetGlobalObjectTemplate(NativeV8EngineProxy* engine, NativeObjectTemplateProxy* proxy);
        // Return: HandleProxy*
        // (Note: returns a handle to the global object created by the context when the object template was set)

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void RegisterNamedPropertyHandlers(NativeObjectTemplateProxy* proxy,
            ManagedNamedPropertyGetter getter,
            ManagedNamedPropertySetter setter,
            ManagedNamedPropertyQuery query,
            ManagedNamedPropertyDeleter deleter,
            ManagedNamedPropertyEnumerator enumerator);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void RegisterIndexedPropertyHandlers(NativeObjectTemplateProxy* proxy,
            ManagedIndexedPropertyGetter getter,
            ManagedIndexedPropertySetter setter,
            ManagedIndexedPropertyQuery query,
            ManagedIndexedPropertyDeleter deleter,
            ManagedIndexedPropertyEnumerator enumerator);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void UnregisterNamedPropertyHandlers(NativeObjectTemplateProxy* proxy);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void UnregisterIndexedPropertyHandlers(NativeObjectTemplateProxy* proxy);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void RegisterInvokeHandler(NativeObjectTemplateProxy* proxy, ManagedJSFunctionCallback callback);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern HandleProxy* CreateObjectFromTemplate(NativeObjectTemplateProxy* objectTemplateProxy, Int32 objID);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
#if V1_1 || V2 || V3 || V3_5
        public static unsafe extern void ConnectObject(HandleProxy* handleProxy, Int32 objID, void* templateProxy);
#else
        public static unsafe extern void ConnectObject(HandleProxy* handleProxy, Int32 objID, void* templateProxy = null);
#endif

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern HandleProxy* GetObjectPrototype(HandleProxy* handleProxy);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        /// <summary>
        /// Calls a property with a given name on a specified object as a function and returns the result.
        /// If the function name is null, then the subject is assumed to be a function object.
        /// </summary>
        public static unsafe extern HandleProxy* Call(HandleProxy* subject, string functionName, HandleProxy* _this, Int32 argCount, HandleProxy** args);
        // Return: HandleProxy*

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
#if V1_1 || V2 || V3 || V3_5
        public static unsafe extern bool SetObjectPropertyByName(HandleProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes);
#else
        public static unsafe extern bool SetObjectPropertyByName(HandleProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);
#endif

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern bool SetObjectPropertyByIndex(HandleProxy* proxy, Int32 index, HandleProxy* value);

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static unsafe extern HandleProxy* GetObjectPropertyByName(HandleProxy* proxy, string name);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern HandleProxy* GetObjectPropertyByIndex(HandleProxy* proxy, Int32 index);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static unsafe extern bool DeleteObjectPropertyByName(HandleProxy* proxy, string name);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern bool DeleteObjectPropertyByIndex(HandleProxy* proxy, Int32 index);

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static unsafe extern void SetObjectAccessor(HandleProxy* proxy, Int32 managedObjectID, string name,
            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static unsafe extern void SetObjectTemplateAccessor(NativeObjectTemplateProxy* proxy, Int32 managedObjectID, string name,
            ManagedAccessorGetter getter, ManagedAccessorSetter setter,
            V8AccessControl access, V8PropertyAttributes attributes);

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static unsafe extern void SetObjectTemplateProperty(NativeObjectTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern HandleProxy* GetPropertyNames(HandleProxy* proxy);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern HandleProxy* GetOwnPropertyNames(HandleProxy* proxy);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static unsafe extern V8PropertyAttributes GetPropertyAttributes(HandleProxy* proxy, string name);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern Int32 GetArrayLength(HandleProxy* proxy);

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static unsafe extern NativeFunctionTemplateProxy* CreateFunctionTemplateProxy(NativeV8EngineProxy* engine, string className, ManagedJSFunctionCallback callback);
        // Return: NativeFunctionTemplateProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern unsafe void DeleteFunctionTemplateProxy(NativeFunctionTemplateProxy* functionTemplateProxy);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern NativeObjectTemplateProxy* GetFunctionInstanceTemplateProxy(NativeFunctionTemplateProxy* functionTemplateProxy);
        // Return: NativeObjectTemplateProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern void* GetFunctionPrototypeTemplateProxy(NativeFunctionTemplateProxy* functionTemplateProxy);
        // Return: NativeObjectTemplateProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static unsafe extern HandleProxy* GetFunction(NativeFunctionTemplateProxy* functionTemplateProxy);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
#if V1_1 || V2 || V3 || V3_5
        public static unsafe extern HandleProxy* CreateFunctionInstance(NativeFunctionTemplateProxy* functionTemplateProxy, Int32 objID, Int32 argCount, HandleProxy** args);
#else
        public static unsafe extern HandleProxy* CreateFunctionInstance(NativeFunctionTemplateProxy* functionTemplateProxy, Int32 objID, Int32 argCount = 0, HandleProxy** args = null);
        // Return: HandleProxy*
#endif

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static unsafe extern void SetFunctionTemplateProperty(NativeFunctionTemplateProxy* proxy, string name, HandleProxy* value, V8PropertyAttributes attributes = V8PropertyAttributes.None);


        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern HandleProxy* CreateBoolean(NativeV8EngineProxy* engine, bool b);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern HandleProxy* CreateInteger(NativeV8EngineProxy* engine, Int32 num);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern HandleProxy* CreateNumber(NativeV8EngineProxy* engine, double num);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static extern HandleProxy* CreateString(NativeV8EngineProxy* engine, string str);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static extern HandleProxy* CreateError(NativeV8EngineProxy* engine, string message, JSValueType errorType);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern HandleProxy* CreateDate(NativeV8EngineProxy* engine, double ms);
        // Return: HandleProxy*

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern HandleProxy* CreateObject(NativeV8EngineProxy* engine, Int32 managedObjectID);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
#if V1_1 || V2 || V3 || V3_5
        public static extern HandleProxy* CreateArray(NativeV8EngineProxy* engine, HandleProxy** items, Int32 length);
#else
        public static extern HandleProxy* CreateArray(NativeV8EngineProxy* engine, HandleProxy** items = null, Int32 length = 0);
#endif

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
#if V1_1 || V2 || V3 || V3_5
        public static extern HandleProxy* CreateStringArray(NativeV8EngineProxy* engine, char** items, Int32 length);
#else
        public static extern HandleProxy* CreateStringArray(NativeV8EngineProxy* engine, char** items, Int32 length = 0);
#endif

#if x86
        [DllImport("V8_Net_Proxy_x86", CharSet = CharSet.Unicode)]
#elif x64
        [DllImport("V8_Net_Proxy_x64", CharSet = CharSet.Unicode)]
#else
        [DllImport("V8_Net_Proxy", CharSet = CharSet.Unicode)]
#endif
        public static extern HandleProxy* CreateNullValue(NativeV8EngineProxy* engine);

        //  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . 

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void MakeWeakHandle(HandleProxy* handleProxy);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void MakeStrongHandle(HandleProxy* handleProxy);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void DisposeHandleProxy(HandleProxy* handle);
        // (required for disposing of the associated V8 handle marshalled in "_HandleProxy")

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void UpdateHandleValue(HandleProxy* handle);

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern int GetHandleManagedObjectID(HandleProxy* handle);

        // --------------------------------------------------------------------------------------------------------------------
        // Tests

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern HandleProxy* CreateHandleProxyTest();

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern NativeV8EngineProxy* CreateV8EngineProxyTest();

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern NativeObjectTemplateProxy* CreateObjectTemplateProxyTest();

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern NativeFunctionTemplateProxy* CreateFunctionTemplateProxyTest();

#if x86
        [DllImport("V8_Net_Proxy_x86")]
#elif x64
        [DllImport("V8_Net_Proxy_x64")]
#else
        [DllImport("V8_Net_Proxy")]
#endif
        public static extern void DeleteTestData(void* data);

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ########################################################################################################################
}
