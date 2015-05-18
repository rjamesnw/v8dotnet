#include "ProxyTypes.h"

// ############################################################################################################################
// Misc. Global Functions

// ...

// ############################################################################################################################
// DLL Exports

// Prevent name mangling for the interface functions. 
extern "C"
{
	// ------------------------------------------------------------------------------------------------------------------------
	// Engine Related

	EXPORT V8EngineProxy* STDCALL CreateV8EngineProxy(bool enableDebugging, DebugMessageDispatcher *debugMessageDispatcher, int debugPort)
	{
		return new V8EngineProxy(enableDebugging, debugMessageDispatcher, debugPort);
	}
	EXPORT void STDCALL DestroyV8EngineProxy(V8EngineProxy *engine)
	{
		delete engine;
	}

	EXPORT void STDCALL RegisterGCCallback(V8EngineProxy* engine, ManagedV8GarbageCollectionRequestCallback managedV8GarbageCollectionRequestCallback)
	{
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		engine->RegisterGCCallback(managedV8GarbageCollectionRequestCallback);
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL ForceGC(V8EngineProxy* engine)
	{
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		engine->Isolate()->LowMemoryNotification();
		while (!engine->Isolate()->IdleNotification(1000)) {}
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT bool STDCALL DoIdleNotification(V8EngineProxy* engine, int hint = 1000)
	{
		if (engine->IsExecutingScript()) return false;
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		return engine->Isolate()->IdleNotification(hint);
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT HandleProxy* STDCALL V8Execute(V8EngineProxy *engine, uint16_t *script, uint16_t *sourceName)
	{
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		return engine->Execute(script, sourceName);
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}
	EXPORT HandleProxy* STDCALL V8Compile(V8EngineProxy *engine, uint16_t *script, uint16_t *sourceName)
	{
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		return engine->Compile(script, sourceName);
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}
	EXPORT HandleProxy* STDCALL V8ExecuteCompiledScript(V8EngineProxy *engine, HandleProxy* script)
	{
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		if (!script->IsScript())
			return engine->CreateError("Not a valid script handle.", JSV_ExecutionError);
		return engine->Execute(script->Script());
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	// ------------------------------------------------------------------------------------------------------------------------
	// Object Template Related

	EXPORT ObjectTemplateProxy* STDCALL CreateObjectTemplateProxy(V8EngineProxy *engine)
	{
		BEGIN_ISOLATE_SCOPE(engine);
		return engine->CreateObjectTemplate();
		END_ISOLATE_SCOPE;
	}
	EXPORT void STDCALL DeleteObjectTemplateProxy(ObjectTemplateProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		delete proxy;
		END_ISOLATE_SCOPE;
	}
	EXPORT HandleProxy* STDCALL SetGlobalObjectTemplate(V8EngineProxy *engine, ObjectTemplateProxy *proxy)
	{
		BEGIN_ISOLATE_SCOPE(engine);
		return engine->SetGlobalObjectTemplate(proxy);
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL RegisterNamedPropertyHandlers(ObjectTemplateProxy *proxy,
		ManagedNamedPropertyGetter getter,
		ManagedNamedPropertySetter setter,
		ManagedNamedPropertyQuery query,
		ManagedNamedPropertyDeleter deleter,
		ManagedNamedPropertyEnumerator enumerator)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		proxy->RegisterNamedPropertyHandlers(getter, setter, query, deleter, enumerator);
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL RegisterIndexedPropertyHandlers(ObjectTemplateProxy *proxy,
		ManagedIndexedPropertyGetter getter,
		ManagedIndexedPropertySetter setter,
		ManagedIndexedPropertyQuery query,
		ManagedIndexedPropertyDeleter deleter,
		ManagedIndexedPropertyEnumerator enumerator)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		proxy->RegisterIndexedPropertyHandlers(getter, setter, query, deleter, enumerator);
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL UnregisterNamedPropertyHandlers(ObjectTemplateProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		proxy->UnregisterNamedPropertyHandlers();
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL UnregisterIndexedPropertyHandlers(ObjectTemplateProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		proxy->UnregisterIndexedPropertyHandlers();
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL SetCallAsFunctionHandler(ObjectTemplateProxy *proxy, ManagedJSFunctionCallback callback)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		proxy->SetCallAsFunctionHandler(callback);
		END_ISOLATE_SCOPE;
	}

	EXPORT HandleProxy* STDCALL CreateObjectFromTemplate(ObjectTemplateProxy *proxy, int32_t managedObjectID)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		return proxy->CreateObject(managedObjectID);
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	// This function connects objects that are created internally by V8, but are based on custom templates (such as new instances created by functions where V8
	// creates the object internally and passes it along).
	// 'templateProxy' should be null (for basic non-template objects), or a reference to one of the native proxy template classes.
	EXPORT void STDCALL ConnectObject(HandleProxy *handleProxy, int32_t managedObjectID, void* templateProxy)
	{
		auto engine = handleProxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		if (managedObjectID == -1)
			managedObjectID = handleProxy->EngineProxy()->GetNextNonTemplateObjectID();

		auto handle = handleProxy->Handle();
		if (!handle.IsEmpty() && handle->IsObject())
		{
			auto obj = handleProxy->Handle().As<Object>();
			if (obj->InternalFieldCount() > 1)
			{
				if (templateProxy != nullptr)
					obj->SetAlignedPointerInInternalField(0, templateProxy); // (stored a reference to the proxy instance for the call-back function(s))
				obj->SetInternalField(1, NewExternal((void*)managedObjectID));
			}
			obj->SetHiddenValue(NewString("ManagedObjectID"), NewInteger(managedObjectID)); // (won't be used on template created objects [fields are faster], but done anyhow for consistency)
		}
		handleProxy->SetManagedObjectID(managedObjectID);

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT HandleProxy* STDCALL GetObjectPrototype(HandleProxy *handleProxy)
	{
		auto engine = handleProxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = handleProxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");
		return handleProxy->EngineProxy()->GetHandleProxy(handle.As<Object>()->GetPrototype());

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT HandleProxy* STDCALL Call(HandleProxy *subject, const uint16_t *functionName, HandleProxy *_this, uint16_t argCount, HandleProxy** args)
	{
		auto engine = subject->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		return engine->Call(subject, functionName, _this, argCount, args);

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	// ------------------------------------------------------------------------------------------------------------------------

	EXPORT bool STDCALL SetObjectPropertyByName(HandleProxy *proxy, const uint16_t *name, HandleProxy *value, v8::PropertyAttribute attribs = v8::None)
	{
		auto engine = proxy->EngineProxy();

		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();

		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");

		// ... managed objects must have a clone of their handle set because it may be made weak by the worker if abandoned, and the handle lost ...
		Handle<Value> valueHandle = value == nullptr ? (Handle<Value>)V8Undefined : value->GetManagedObjectID() < 0 ? value->Handle() : value->Handle()->ToObject();
		//?Handle<Value> valueHandle = value != nullptr ? value->Handle() : V8Undefined;

		auto obj = handle.As<Object>();
		return obj->ForceSet(NewUString(name), valueHandle, attribs);

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT bool STDCALL SetObjectPropertyByIndex(HandleProxy *proxy, const uint16_t index, HandleProxy *value)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();

		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");

		auto obj = handle.As<Object>();

		//auto valueHandle = new CopyablePersistent<Value>(value != nullptr ? value->Handle() : V8Undefined);
		Handle<Value> valueHandle = value != nullptr ? value->Handle() : V8Undefined;

		return obj->Set(index, valueHandle);

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT HandleProxy* STDCALL GetObjectPropertyByName(HandleProxy *proxy, const uint16_t *name)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");
		auto obj = handle.As<Object>();
		return proxy->EngineProxy()->GetHandleProxy(obj->Get(NewUString(name)));

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT HandleProxy* STDCALL GetObjectPropertyByIndex(HandleProxy *proxy, const uint16_t index)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");
		auto obj = handle.As<Object>();
		return proxy->EngineProxy()->GetHandleProxy(obj->Get(index));

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT bool STDCALL DeleteObjectPropertyByName(HandleProxy *proxy, const uint16_t *name)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");
		auto obj = handle.As<Object>();
		return obj->Delete(NewUString(name));

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT bool STDCALL DeleteObjectPropertyByIndex(HandleProxy *proxy, const uint16_t index)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");
		auto obj = handle.As<Object>();
		return obj->Delete(index);

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL SetObjectAccessor(HandleProxy *proxy, int32_t managedObjectID, const uint16_t *name,
		ManagedAccessorGetter getter, ManagedAccessorSetter setter,
		v8::AccessControl access, v8::PropertyAttribute attributes)
	{
		if (attributes < 0) // (-1 is "No Access" on the managed side, but there is no native support in V8 for this)
			attributes = (PropertyAttribute)(ReadOnly | DontEnum);

		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");

		auto obj = handle.As<Object>();

		obj->SetHiddenValue(NewString("ManagedObjectID"), NewInteger(managedObjectID));

		auto accessors = NewArray(3); // [0] == ManagedObjectID, [1] == getter, [2] == setter
		accessors->Set(0, NewInteger(managedObjectID));
		accessors->Set(1, NewExternal(getter));
		accessors->Set(2, NewExternal(setter));

		obj->ForceDelete(NewUString(name));
		obj->SetAccessor(NewUString(name), ObjectTemplateProxy::AccessorGetterCallbackProxy, ObjectTemplateProxy::AccessorSetterCallbackProxy, accessors, access, attributes);  // TODO: Check how this affects objects created from templates!

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL SetObjectTemplateAccessor(ObjectTemplateProxy *proxy, int32_t managedObjectID, const uint16_t *name,
		ManagedAccessorGetter getter, ManagedAccessorSetter setter,
		v8::AccessControl access, v8::PropertyAttribute attributes)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		proxy->SetAccessor(managedObjectID, name, getter, setter, access, attributes);  // TODO: Check how this affects objects created from templates!

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL SetObjectTemplateProperty(ObjectTemplateProxy *proxy, const uint16_t *name, HandleProxy *value, v8::PropertyAttribute attributes)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		proxy->Set(name, value, attributes);  // TODO: Check how this affects objects created from templates!

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT HandleProxy* STDCALL GetPropertyNames(HandleProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");
		auto obj = handle.As<Object>();
		auto names = obj->GetPropertyNames();
		return proxy->EngineProxy()->GetHandleProxy(names);

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT HandleProxy* STDCALL GetOwnPropertyNames(HandleProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");
		auto obj = handle.As<Object>();
		auto names = obj->GetOwnPropertyNames();
		return proxy->EngineProxy()->GetHandleProxy(names);

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT PropertyAttribute STDCALL GetPropertyAttributes(HandleProxy *proxy, const uint16_t * name)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsObject())
			throw exception("The handle does not represent an object.");
		auto obj = handle.As<Object>();
		return obj->GetPropertyAttributes(NewUString(name));

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT int32_t STDCALL GetArrayLength(HandleProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);

		auto handle = proxy->Handle();
		if (handle.IsEmpty() || !handle->IsArray())
			throw exception("The handle does not represent an array object.");
		return handle.As<Array>()->Length();

		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	// ------------------------------------------------------------------------------------------------------------------------
	// Function Template Related
	EXPORT FunctionTemplateProxy* STDCALL CreateFunctionTemplateProxy(V8EngineProxy *engine, uint16_t *className, ManagedJSFunctionCallback callback)
	{
		BEGIN_ISOLATE_SCOPE(engine);
		return engine->CreateFunctionTemplate(className, callback);
		END_ISOLATE_SCOPE;
	}
	EXPORT void STDCALL DeleteFunctionTemplateProxy(FunctionTemplateProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		delete proxy;
		END_ISOLATE_SCOPE;
	}
	EXPORT ObjectTemplateProxy* STDCALL GetFunctionInstanceTemplateProxy(FunctionTemplateProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		return proxy->GetInstanceTemplateProxy();
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}
	EXPORT ObjectTemplateProxy* STDCALL GetFunctionPrototypeTemplateProxy(FunctionTemplateProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		return proxy->GetPrototypeTemplateProxy();
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}
	//??EXPORT void STDCALL SetManagedJSFunctionCallback(FunctionTemplateProxy *proxy, ManagedJSFunctionCallback callback)  { proxy->SetManagedCallback(callback); }

	EXPORT HandleProxy* STDCALL GetFunction(FunctionTemplateProxy *proxy)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		return proxy->GetFunction();
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}
	//??EXPORT HandleProxy* STDCALL GetFunctionPrototype(FunctionTemplateProxy *proxy, int32_t managedObjectID, ObjectTemplateProxy *objTemplate)
	//??{ return proxy->GetPrototype(managedObjectID, objTemplate); }
	EXPORT HandleProxy* STDCALL CreateFunctionInstance(FunctionTemplateProxy *proxy, int32_t managedObjectID, int32_t argCount = 0, HandleProxy** args = nullptr)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		return proxy->CreateInstance(managedObjectID, argCount, args);
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	EXPORT void STDCALL SetFunctionTemplateProperty(FunctionTemplateProxy *proxy, const uint16_t *name, HandleProxy *value, v8::PropertyAttribute attributes)
	{
		auto engine = proxy->EngineProxy();
		BEGIN_ISOLATE_SCOPE(engine);
		BEGIN_CONTEXT_SCOPE(engine);
		proxy->Set(name, value, attributes);  // TODO: Check how this affects objects created from templates!
		END_CONTEXT_SCOPE;
		END_ISOLATE_SCOPE;
	}

	// ------------------------------------------------------------------------------------------------------------------------
	// Value Creation 

	EXPORT HandleProxy* STDCALL CreateBoolean(V8EngineProxy *engine, bool b) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateBoolean(b); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }
	EXPORT HandleProxy* STDCALL CreateInteger(V8EngineProxy *engine, int32_t num) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateInteger(num); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }
	EXPORT HandleProxy* STDCALL CreateNumber(V8EngineProxy *engine, double num) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateNumber(num); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }
	EXPORT HandleProxy* STDCALL CreateString(V8EngineProxy *engine, uint16_t* str) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateString(str); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }
	EXPORT HandleProxy* STDCALL CreateDate(V8EngineProxy *engine, double ms) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateDate(ms); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }
	EXPORT HandleProxy* STDCALL CreateObject(V8EngineProxy *engine, int32_t managedObjectID) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateObject(managedObjectID); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }
	EXPORT HandleProxy* STDCALL CreateArray(V8EngineProxy *engine, HandleProxy** items, uint16_t length) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateArray(items, length); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }
	EXPORT HandleProxy* STDCALL CreateStringArray(V8EngineProxy *engine, uint16_t **items, uint16_t length) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateArray(items, length); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }

	EXPORT HandleProxy* STDCALL CreateNullValue(V8EngineProxy *engine) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateNullValue(); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }

	EXPORT HandleProxy* STDCALL CreateError(V8EngineProxy *engine, uint16_t* message, JSValueType errorType) { BEGIN_ISOLATE_SCOPE(engine); BEGIN_CONTEXT_SCOPE(engine); return engine->CreateError(message, errorType); END_CONTEXT_SCOPE; END_ISOLATE_SCOPE; }

	// ------------------------------------------------------------------------------------------------------------------------
	// Handle Related

	EXPORT void STDCALL MakeWeakHandle(HandleProxy *handleProxy)
	{
		if (handleProxy != nullptr)
		{
			auto engine = handleProxy->EngineProxy();

			if (engine->IsExecutingScript())
			{
				// ... a script is running, so have 'GetHandleProxy()' take some responsibility to check a queue ...
				engine->QueueMakeWeak(handleProxy);
			}
			else
			{
				BEGIN_ISOLATE_SCOPE(engine);
				BEGIN_CONTEXT_SCOPE(engine);
				handleProxy->MakeWeak();
				END_CONTEXT_SCOPE;
				END_ISOLATE_SCOPE;
			}
		}
	}
	EXPORT void STDCALL MakeStrongHandle(HandleProxy *handleProxy)
	{
		if (handleProxy != nullptr)
		{
			auto engine = handleProxy->EngineProxy();

			if (engine->IsExecutingScript())
			{
				// ... a script is running, so have 'GetHandleProxy()' take some responsibility to check a queue ...
				engine->QueueMakeStrong(handleProxy);
			}
			else
			{
				BEGIN_ISOLATE_SCOPE(engine);
				BEGIN_CONTEXT_SCOPE(engine);
				handleProxy->MakeStrong();
				END_CONTEXT_SCOPE;
				END_ISOLATE_SCOPE;
			}
		}
	}

	EXPORT void STDCALL DisposeHandleProxy(HandleProxy *handleProxy)
	{
		if (handleProxy != nullptr)
		{
			auto engine = handleProxy->EngineProxy();
			BEGIN_ISOLATE_SCOPE(engine);
			BEGIN_CONTEXT_SCOPE(engine);
			handleProxy->Dispose();
			END_CONTEXT_SCOPE;
			END_ISOLATE_SCOPE;
		}
	}

	EXPORT void STDCALL UpdateHandleValue(HandleProxy *handleProxy)
	{
		if (handleProxy != nullptr)
		{
			auto engine = handleProxy->EngineProxy();
			BEGIN_ISOLATE_SCOPE(engine);
			BEGIN_CONTEXT_SCOPE(engine);
			handleProxy->UpdateValue();
			END_CONTEXT_SCOPE;
			END_ISOLATE_SCOPE;
		}
	}
	EXPORT int STDCALL GetHandleManagedObjectID(HandleProxy *handleProxy)
	{
		if (handleProxy != nullptr)
		{
			auto engine = handleProxy->EngineProxy();
			BEGIN_ISOLATE_SCOPE(engine);
			BEGIN_CONTEXT_SCOPE(engine);
			return handleProxy->GetManagedObjectID();
			END_CONTEXT_SCOPE;
			END_ISOLATE_SCOPE;
		}
		else return -2;
	}

	// ------------------------------------------------------------------------------------------------------------------------

	EXPORT HandleProxy* STDCALL CreateHandleProxyTest()
	{
		byte* data = new byte[sizeof(HandleProxy)];
		for (byte i = 0; i < sizeof(HandleProxy); i++)
			data[i] = i;
		TProxyObjectType* pType = (TProxyObjectType*)data;
		*pType = HandleProxyClass;
		return reinterpret_cast<HandleProxy*>(data);
	}

	EXPORT V8EngineProxy* STDCALL CreateV8EngineProxyTest()
	{
		byte* data = new byte[sizeof(V8EngineProxy)];
		for (byte i = 0; i < sizeof(V8EngineProxy); i++)
			data[i] = i;
		TProxyObjectType* pType = (TProxyObjectType*)data;
		*pType = V8EngineProxyClass;
		return reinterpret_cast<V8EngineProxy*>(data);
	}

	EXPORT ObjectTemplateProxy* STDCALL CreateObjectTemplateProxyTest()
	{
		byte* data = new byte[sizeof(ObjectTemplateProxy)];
		for (byte i = 0; i < sizeof(ObjectTemplateProxy); i++)
			data[i] = i;
		TProxyObjectType* pType = (TProxyObjectType*)data;
		*pType = ObjectTemplateProxyClass;
		return reinterpret_cast<ObjectTemplateProxy*>(data);
	}

	EXPORT FunctionTemplateProxy* STDCALL CreateFunctionTemplateProxyTest()
	{
		byte* data = new byte[sizeof(FunctionTemplateProxy)];
		for (byte i = 0; i < sizeof(FunctionTemplateProxy); i++)
			data[i] = i;
		TProxyObjectType* pType = (TProxyObjectType*)data;
		*pType = FunctionTemplateProxyClass;
		return reinterpret_cast<FunctionTemplateProxy*>(data);
	}

	EXPORT void STDCALL DeleteTestData(byte* data)
	{
		ProxyBase* pBase = reinterpret_cast<ProxyBase*>(data);
		if (pBase->GetType() == ObjectTemplateProxyClass)
		{
			memset(data, 0, sizeof(ObjectTemplateProxy));
			delete[] data;
		}
		else if (pBase->GetType() == FunctionTemplateProxyClass)
		{
			memset(data, 0, sizeof(FunctionTemplateProxy));
			delete[] data;
		}
		else if (pBase->GetType() == V8EngineProxyClass)
		{
			memset(data, 0, sizeof(V8EngineProxy));
			delete[] data;
		}
		else if (pBase->GetType() == HandleProxyClass)
		{
			memset(data, 0, sizeof(HandleProxy));
			delete[] data;
		}
		else
		{
			throw exception("'Data' points to an invalid object reference and cannot be deleted.");
		}
	}

	// ------------------------------------------------------------------------------------------------------------------------
}

// ############################################################################################################################
