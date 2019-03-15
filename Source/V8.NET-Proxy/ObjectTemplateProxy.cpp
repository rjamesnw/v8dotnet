#include "ProxyTypes.h"

// ------------------------------------------------------------------------------------------------------------------------

ObjectTemplateProxy::ObjectTemplateProxy(V8EngineProxy* engineProxy)
	:ProxyBase(ObjectTemplateProxyClass), _EngineProxy(engineProxy), _EngineID(engineProxy->_EngineID)
{
	_ObjectID = _EngineProxy->GetNextNonTemplateObjectID(); // ("ObjectTemplateProxy" will qualify as a non-template-created object in this case)
	auto obj = NewObjectTemplate();
	obj->SetInternalFieldCount(2); // (one for the associated proxy, and one for the associated managed object ID)
	_ObjectTemplate = CopyablePersistent<ObjectTemplate>(obj);
}

ObjectTemplateProxy::ObjectTemplateProxy(V8EngineProxy* engineProxy, Local<ObjectTemplate> objectTemplate)
	:ProxyBase(ObjectTemplateProxyClass), _EngineProxy(engineProxy), _EngineID(engineProxy->_EngineID)
{
	_ObjectID = _EngineProxy->GetNextNonTemplateObjectID(); // ("ObjectTemplateProxy" will qualify as a non-template-created object in this case)
	objectTemplate->SetInternalFieldCount(2); // (one for the associated proxy, and one for the associated managed object ID)
	_ObjectTemplate = CopyablePersistent<ObjectTemplate>(objectTemplate);
}

ObjectTemplateProxy::~ObjectTemplateProxy()
{
	if (Type != 0) // (type is 0 if this class was wiped with 0's {if used in a marshalling test})
	{
		if (!V8EngineProxy::IsDisposed(_EngineID))
		{
			BEGIN_ISOLATE_SCOPE(_EngineProxy);
			BEGIN_CONTEXT_SCOPE(_EngineProxy);

			UnregisterNamedPropertyHandlers();

			if (!_ObjectTemplate.IsEmpty())
				_ObjectTemplate.Reset();

			END_CONTEXT_SCOPE;
			END_ISOLATE_SCOPE;
		}

		_EngineProxy = nullptr;
		_ManagedCallback = nullptr;
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::RegisterNamedPropertyHandlers(
	ManagedNamedPropertyGetter getter,
	ManagedNamedPropertySetter setter,
	ManagedNamedPropertyQuery query,
	ManagedNamedPropertyDeleter deleter,
	ManagedNamedPropertyEnumerator enumerator)
{
	NamedPropertyGetter = getter;
	NamedPropertySetter = setter;
	NamedPropertyQuery = query;
	NamedPropertyDeleter = deleter;
	NamedPropertyEnumerator = enumerator;

	NamedPropertyHandlerConfiguration config(GetProperty, SetProperty, GetPropertyAttributes, DeleteProperty, GetPropertyNames);
	_ObjectTemplate->SetHandler(config);
}

void ObjectTemplateProxy::RegisterIndexedPropertyHandlers(
	ManagedIndexedPropertyGetter getter,
	ManagedIndexedPropertySetter setter,
	ManagedIndexedPropertyQuery query,
	ManagedIndexedPropertyDeleter deleter,
	ManagedIndexedPropertyEnumerator enumerator)
{
	IndexedPropertyGetter = getter;
	IndexedPropertySetter = setter;
	IndexedPropertyQuery = query;
	IndexedPropertyDeleter = deleter;
	IndexedPropertyEnumerator = enumerator;

	IndexedPropertyHandlerConfiguration config(GetProperty, SetProperty, GetPropertyAttributes, DeleteProperty, GetPropertyIndices);
	_ObjectTemplate->SetHandler(config);
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::SetCallAsFunctionHandler(ManagedJSFunctionCallback callback)
{
	_ManagedCallback = callback;
	_ObjectTemplate->SetCallAsFunctionHandler(FunctionTemplateProxy::InvocationCallbackProxy, NewExternal(this));

}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::UnregisterNamedPropertyHandlers()
{
	//if (!_WasUsed) //?throw exception("ObjectTemplateProxy::UnregisterNamedPropertyHandlers(): You cannot change the object template named handlers after an object was already created from it.");
	//	_ObjectTemplate->SetHandler(NamedPropertyHandlerConfiguration());
	//	^ This cannot work since a getter is REQUIRED.  Instead we will just nullify the callback references ...
	NamedPropertyGetter = nullptr;
	NamedPropertySetter = nullptr;
	NamedPropertyQuery = nullptr;
	NamedPropertyDeleter = nullptr;
	NamedPropertyEnumerator = nullptr;
}

void ObjectTemplateProxy::UnregisterIndexedPropertyHandlers()
{
	//if (!_WasUsed) //?throw exception("ObjectTemplateProxy::UnregisterIndexedPropertyHandlers(): You cannot change the object template index handlers after an object was already created from it.");
	//	_ObjectTemplate->SetHandler(IndexedPropertyHandlerConfiguration());
		//	^ This cannot work since a getter is REQUIRED.  Instead we will just nullify the callback references ...
	IndexedPropertyGetter = nullptr;
	IndexedPropertySetter = nullptr;
	IndexedPropertyQuery = nullptr;
	IndexedPropertyDeleter = nullptr;
	IndexedPropertyEnumerator = nullptr;
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::GetProperty(Local<Name> hName, const PropertyCallbackInfo<Value>& info)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->NamedPropertyGetter != nullptr)
		{
			if (proxy != nullptr && proxy->_EngineProxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				auto engine = proxy->_EngineProxy;
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				auto hNameStr = hName->IsSymbol() ? hName.As<Symbol>()->Name().As<String>() : hName.As<String>();
				auto str = engine->GetNativeString(*hNameStr); // TODO: This can be faster - no need to allocate every time!
				engine->_InCallbackScope++;
				HandleProxy* result = nullptr;
				try {
					result = proxy->NamedPropertyGetter(str.String, maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'NamedPropertyGetter' no longer exists - perhaps the GC collected it.")); }
				engine->_InCallbackScope--;
				str.Dispose();
				if (result != nullptr)
				{
					if (result->IsError())
						info.GetReturnValue().Set(ThrowException(Exception::Error(result->Handle()->ToString(info.GetIsolate()))));
					else
						info.GetReturnValue().Set(result->Handle()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)

					result->TryDispose();
				}
				// (result == null == undefined [which means the managed side didn't return anything])
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::SetProperty(Local<Name> hName, Local<Value> value, const PropertyCallbackInfo<Value>& info)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->NamedPropertySetter != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				auto engine = proxy->_EngineProxy;
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				auto hNameStr = hName->IsSymbol() ? hName.As<Symbol>()->Name().As<String>() : hName.As<String>();
				auto str = engine->GetNativeString(*hNameStr);
				HandleProxy *val = engine->GetHandleProxy(value);
				engine->_InCallbackScope++;
				HandleProxy* result = nullptr;
				try {
					result = proxy->NamedPropertySetter(str.String, val, maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'NamedPropertySetter' no longer exists - perhaps the GC collected it.")); }
				engine->_InCallbackScope--;
				engine->ProcessHandleQueues(); // (since setting properties may dispose another, do this at least once)
				str.Dispose();
				if (result != nullptr)
				{
					if (result->IsError())
						info.GetReturnValue().Set(ThrowException(Exception::Error(result->Handle()->ToString(info.GetIsolate()))));
					else
						info.GetReturnValue().Set(result->Handle()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)

					result->TryDispose();
				}
				// (result == null == undefined [which means the managed side didn't return anything])

				// ... do this LAST, as the result may be one of the arguments passed in ...
				val->TryDispose();
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::GetPropertyAttributes(Local<Name> hName, const PropertyCallbackInfo<Integer>& info)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->NamedPropertyQuery != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				auto hNameStr = hName->IsSymbol() ? hName.As<Symbol>()->Name().As<String>() : hName.As<String>();
				auto str = proxy->_EngineProxy->GetNativeString(*hNameStr);
				proxy->_EngineProxy->_InCallbackScope++;
				int result = -1;
				try {
					result = proxy->NamedPropertyQuery(str.String, maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'NamedPropertyQuery' no longer exists - perhaps the GC collected it.")); }
				proxy->_EngineProxy->_InCallbackScope--;
				str.Dispose();
				if (result >= 0)
					info.GetReturnValue().Set(Handle<v8::Integer>(NewInteger(result)));
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::DeleteProperty(Local<Name> hName, const PropertyCallbackInfo<Boolean>& info)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->NamedPropertyDeleter != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				auto hNameStr = hName->IsSymbol() ? hName.As<Symbol>()->Name().As<String>() : hName.As<String>();
				auto str = proxy->_EngineProxy->GetNativeString(*hNameStr);
				proxy->_EngineProxy->_InCallbackScope++;
				int result = 0;
				try {
					result = proxy->NamedPropertyDeleter(str.String, maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'NamedPropertyDeleter' no longer exists - perhaps the GC collected it.")); }
				proxy->_EngineProxy->_InCallbackScope--;
				str.Dispose();

				// if 'result' is < 0, then this represents an "undefined" return value, otherwise 0 == false, and > 0 is true.

				if (result >= 0)
					info.GetReturnValue().Set(Handle<v8::Boolean>(NewBool(result != 0 ? true : false)));
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::GetPropertyNames(const PropertyCallbackInfo<Array>& info) // (Note: consider HasOwnProperty)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->NamedPropertyEnumerator != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				proxy->_EngineProxy->_InCallbackScope++;
				HandleProxy* result = nullptr;
				try {
					result = proxy->NamedPropertyEnumerator(maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'NamedPropertyEnumerator' no longer exists - perhaps the GC collected it.")); }
				proxy->_EngineProxy->_InCallbackScope--;
				if (result != nullptr)
				{
					if (result->IsError())
					{
						auto array = NewArray(1);
						array->Set(0, ThrowException(Exception::Error(result->Handle()->ToString(info.GetIsolate()))));
						info.GetReturnValue().Set(array);
					}
					else
						info.GetReturnValue().Set(result->Handle().As<Array>()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)

					result->TryDispose();
				}
				// (result == null == undefined [which means the managed side didn't return anything])
			}
		}
	}
}

// . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . . .

void ObjectTemplateProxy::GetProperty(uint32_t index, const PropertyCallbackInfo<Value>& info)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->IndexedPropertyGetter != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				proxy->_EngineProxy->_InCallbackScope++;
				HandleProxy* result = nullptr;
				try {
					result = proxy->IndexedPropertyGetter(index, maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'IndexedPropertyGetter' no longer exists - perhaps the GC collected it.")); }
				proxy->_EngineProxy->_InCallbackScope--;
				if (result != nullptr)
				{
					if (result->IsError())
						info.GetReturnValue().Set(ThrowException(Exception::Error(result->Handle()->ToString(info.GetIsolate()))));
					else
						info.GetReturnValue().Set(result->Handle()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)

					result->TryDispose();
				}
				// (result == null == undefined [which means the managed side didn't return anything])
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::SetProperty(uint32_t index, Local<Value> value, const PropertyCallbackInfo<Value>& info)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->IndexedPropertySetter != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				HandleProxy *val = proxy->_EngineProxy->GetHandleProxy(value);
				proxy->_EngineProxy->_InCallbackScope++;
				HandleProxy* result = nullptr;
				try {
					result = proxy->IndexedPropertySetter(index, val, maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'IndexedPropertySetter' no longer exists - perhaps the GC collected it.")); }
				proxy->_EngineProxy->_InCallbackScope--;
				proxy->_EngineProxy->ProcessHandleQueues(); // (since setting properties may dispose another, do this at least once)
				if (result != nullptr)
				{
					if (result->IsError())
						info.GetReturnValue().Set(ThrowException(Exception::Error(result->Handle()->ToString(info.GetIsolate()))));
					else
						info.GetReturnValue().Set(result->Handle()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)

					result->TryDispose();
				}
				// (result == null == undefined [which means the managed side didn't return anything])

				// ... do this LAST, as the result may be one of the arguments passed in ...
				val->TryDispose();
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::GetPropertyAttributes(uint32_t index, const PropertyCallbackInfo<Integer>& info)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->IndexedPropertyQuery != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				proxy->_EngineProxy->_InCallbackScope++;
				int result = -1;
				try {
					result = proxy->IndexedPropertyQuery(index, maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'IndexedPropertyQuery' no longer exists - perhaps the GC collected it.")); }
				proxy->_EngineProxy->_InCallbackScope--;
				if (result >= 0)
					info.GetReturnValue().Set(Handle<v8::Integer>(NewInteger(result)));
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::DeleteProperty(uint32_t index, const PropertyCallbackInfo<Boolean>& info)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->IndexedPropertyDeleter != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				proxy->_EngineProxy->_InCallbackScope++;
				int result = 0;
				try {
					result = proxy->IndexedPropertyDeleter(index, maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'IndexedPropertyDeleter' no longer exists - perhaps the GC collected it.")); }
				proxy->_EngineProxy->_InCallbackScope--;

				// if 'result' is < 0, then this represents an "undefined" return value, otherwise 0 == false, and > 0 is true.

				if (result >= 0)
					info.GetReturnValue().Set(Handle<v8::Boolean>(NewBool(result != 0 ? true : false)));
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::GetPropertyIndices(const PropertyCallbackInfo<Array>& info) // (Note: consider HasOwnProperty)
{
	auto obj = info.Holder();

	if (obj->InternalFieldCount() > 1)
	{
		auto field = obj->GetInternalField(0);
		auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

		if (!field->IsUndefined() && proxy->IndexedPropertyEnumerator != nullptr)
		{
			if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
			{
				auto managedObjectID = (int32_t)(int64_t)obj->GetInternalField(1).As<External>()->Value();
				ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
				proxy->_EngineProxy->_InCallbackScope++;
				HandleProxy* result = nullptr;
				try {
					result = proxy->IndexedPropertyEnumerator(maInfo); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'IndexedPropertyEnumerator' no longer exists - perhaps the GC collected it.")); }
				proxy->_EngineProxy->_InCallbackScope--;
				if (result != nullptr)
				{
					if (result->IsError())
					{
						auto array = NewArray(1);
						array->Set(0, ThrowException(Exception::Error(result->Handle()->ToString(info.GetIsolate()))));
						info.GetReturnValue().Set(array);
					}
					else
						info.GetReturnValue().Set(result->Handle().As<Array>()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)

					result->TryDispose();
				}
				// (result == null == undefined [which means the managed side didn't return anything])
			}
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------

HandleProxy* ObjectTemplateProxy::CreateObject(int32_t managedObjectID)
{
	if (managedObjectID == -1)
		managedObjectID = _EngineProxy->GetNextNonTemplateObjectID();

	auto obj = _ObjectTemplate->NewInstance(_EngineProxy->Context()).ToLocalChecked();
	_WasUsed = true;
	auto proxyVal = _EngineProxy->GetHandleProxy(obj);
	ConnectObject(proxyVal, managedObjectID, this);
	return proxyVal;
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::AccessorGetterCallbackProxy(Local<Name> property, const PropertyCallbackInfo<Value>& info)
{
	auto obj = info.Holder();
	auto ret = info.GetReturnValue();

	if (!obj.IsEmpty())
	{
		auto hAccessors = info.Data().As<Array>(); // [0] = ManagedObjectID, [1] == getter, [2] == setter
		if (hAccessors->Length() == 3)
		{
			auto engine = (V8EngineProxy*)info.GetIsolate()->GetData(0);
			auto ctx = engine->Context(); // Context
			auto hID = hAccessors->Get(0).As<Integer>(); // ManagedObjectID
			auto managedObjectID = (int32_t)hID->Int32Value(ctx).ToChecked();
			//??if (managedObjectID < 0 && obj->InternalFieldCount() >= 2) // (if the object ID is < 0 [usually -1] then try to detect the ID in other ways)
			//    auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value(); // (for ObjectTemplate objects)
			//else
			//{
			//    auto hHiddenObjID = obj->GetHiddenValue(String::New("ManagedObjectID")); // (for non-ObjectTemplate objects)
			//    if (!hHiddenObjID.IsEmpty() && hHiddenObjID->IsInt32())
			//        managedObjectID = (int32_t)hHiddenObjID->Int32Value();
			//}

			ManagedAccessorGetter getter = (ManagedAccessorGetter)hAccessors->Get(1).As<External>()->Value();

			if (getter != nullptr)
			{
				auto _this = engine->GetHandleProxy(info.This());
				if (managedObjectID >= 0) _this->_ObjectID = managedObjectID; // (use any explicitly specified object ID)

				auto str = engine->GetNativeString(*property->ToString(info.GetIsolate()));

				engine->_InCallbackScope++;
				HandleProxy* result = nullptr;
				try {
					result = getter(_this, str.String); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'AccessorGetterCallbackProxy' caused an error - perhaps the GC collected the delegate?")); }
				engine->_InCallbackScope--;

				str.Dispose();

				Handle<Value> hResult;

				if (result != nullptr)
				{
					if (result->IsError())
						hResult = ThrowException(Exception::Error(result->Handle()->ToString(info.GetIsolate()))); // TODO: Look into associating the returned error type as well (very low priority)
					else
						hResult = result->Handle(); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
					// (result == null == undefined [which means the managed side didn't return anything])

					result->TryDispose();
				}

				// ... do the following disposal LAST, as the result may be one of the arguments passed in ...
				_this->TryDispose();

				ret.Set(hResult);
				return;
			}
		}
	}

	ret.SetUndefined();
}

void ObjectTemplateProxy::AccessorSetterCallbackProxy(Local<Name> property, Local<Value> value, const PropertyCallbackInfo<void>& info)
{
	auto obj = info.Holder();
	auto ret = info.GetReturnValue();

	if (!obj.IsEmpty())
	{
		auto hAccessors = info.Data().As<Array>(); // [0] = ManagedObjectID, [1] == getter, [2] == setter
		if (hAccessors->Length() == 3)
		{
			auto engine = (V8EngineProxy*)info.GetIsolate()->GetData(0);
			auto ctx = engine->Context(); // Context
			auto hID = hAccessors->Get(0).As<Integer>(); // ManagedObjectID
			auto managedObjectID = (int32_t)hID->Int32Value(ctx).ToChecked();
			//??if (managedObjectID < 0 && obj->InternalFieldCount() >= 2) // (if the object ID is < 0 [usually -1] then try to detect the ID in other ways)
			//    auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value(); // (for ObjectTemplate objects)
			//else
			//{
			//    auto hHiddenObjID = obj->GetHiddenValue(String::New("ManagedObjectID")); // (for non-ObjectTemplate objects)
			//    if (!hHiddenObjID.IsEmpty() && hHiddenObjID->IsInt32())
			//        managedObjectID = (int32_t)hHiddenObjID->Int32Value();
			//}

			ManagedAccessorSetter setter = (ManagedAccessorSetter)hAccessors->Get(1).As<External>()->Value();

			if (setter != nullptr)
			{
				auto _this = engine->GetHandleProxy(info.This());
				if (managedObjectID >= 0) _this->_ObjectID = managedObjectID; // (use any explicitly specified object ID)

				auto str = engine->GetNativeString(*property->ToString(info.GetIsolate()));
				auto _value = engine->GetHandleProxy(value);

				engine->_InCallbackScope++;
				HandleProxy* result = nullptr;
				try {
					result = setter(_this, str.String, _value); // (assumes the 'str' memory will be released by the managed side)
				}
				catch (...) { ThrowException(NewString("'AccessorSetterCallbackProxy' caused an error - perhaps the GC collected the delegate?")); }
				engine->_InCallbackScope--;

				str.Dispose();

				Handle<Value> hResult;

				if (result != nullptr)
				{
					if (result->IsError())
						hResult = ThrowException(Exception::Error(result->Handle()->ToString(info.GetIsolate()))); // TODO: Look into associating the returned error type as well (very low priority)
					else
						hResult = result->Handle(); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
					// (result == null == undefined [which means the managed side didn't return anything])

					result->TryDispose();
				}
				// ... do the following disposal LAST, as the result may be one of the arguments passed in ...
				_this->TryDispose();
				_value->TryDispose();

				ret.Set(hResult);
				return;
			}
		}
	}

	ret.SetUndefined();
}

void ObjectTemplateProxy::SetAccessor(int32_t managedObjectID, const uint16_t *name,
	ManagedAccessorGetter getter, ManagedAccessorSetter setter,
	v8::AccessControl access, v8::PropertyAttribute attributes)
{
	auto accessors = NewArray(3); // [0] == ManagedObjectID, [1] == getter, [2] == setter
	accessors->Set(0, NewInteger(managedObjectID));
	accessors->Set(1, NewExternal(getter));
	accessors->Set(2, NewExternal(setter));
	_ObjectTemplate->SetAccessor(NewUString(name), AccessorGetterCallbackProxy, AccessorSetterCallbackProxy, accessors, access, attributes);  // TODO: Check how this affects objects created from templates!
}

void ObjectTemplateProxy::Set(const uint16_t *name, HandleProxy *value, v8::PropertyAttribute attributes)
{
	if (value != nullptr)
		_ObjectTemplate->Set(NewUString(name), value->Handle(), attributes);  // TODO: Check how this affects objects created from templates!
}

// ------------------------------------------------------------------------------------------------------------------------
