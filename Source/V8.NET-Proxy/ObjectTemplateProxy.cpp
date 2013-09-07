#include "ProxyTypes.h"

// ------------------------------------------------------------------------------------------------------------------------

ObjectTemplateProxy::ObjectTemplateProxy(V8EngineProxy* engineProxy)
    :ProxyBase(ObjectTemplateProxyClass), _EngineProxy(engineProxy), _EngineID(engineProxy->_EngineID),
    NamedPropertyGetter(nullptr), NamedPropertySetter(nullptr), NamedPropertyQuery(nullptr), NamedPropertyDeleter(nullptr), NamedPropertyEnumerator(nullptr)
{
    _ObjectID = _EngineProxy->GetNextNonTemplateObjectID(); // ("ObjectTemplateProxy" will qualify as a non-template-created object in this case)
    _ObjectTemplate = Persistent<ObjectTemplate>::New(_EngineProxy->Isolate(), ObjectTemplate::New());
    _ObjectTemplate->SetInternalFieldCount(2); // (one for the associated proxy, and one for the associated managed object ID)
}

ObjectTemplateProxy::ObjectTemplateProxy(V8EngineProxy* engineProxy, Local<ObjectTemplate> objectTemplate)
    :ProxyBase(ObjectTemplateProxyClass), _EngineProxy(engineProxy), _EngineID(engineProxy->_EngineID),
    NamedPropertyGetter(nullptr), NamedPropertySetter(nullptr), NamedPropertyQuery(nullptr), NamedPropertyDeleter(nullptr), NamedPropertyEnumerator(nullptr)
{
    _ObjectID = _EngineProxy->GetNextNonTemplateObjectID(); // ("ObjectTemplateProxy" will qualify as a non-template-created object in this case)
    _ObjectTemplate = Persistent<ObjectTemplate>::New(_EngineProxy->Isolate(), objectTemplate);
    _ObjectTemplate->SetInternalFieldCount(2); // (one for the associated proxy, and one for the associated managed object ID)
}

ObjectTemplateProxy::~ObjectTemplateProxy()
{
    if (Type != 0) // (type is 0 if this class was wiped with 0's {if used in a marshalling test})
    {
        if (!V8EngineProxy::IsDisposed(_EngineID))
        {
            BEGIN_ISOLATE_SCOPE(_EngineProxy);
            BEGIN_CONTEXT_SCOPE(_EngineProxy);

            if (!_ObjectTemplate.IsEmpty())
                _ObjectTemplate.Dispose();

            END_CONTEXT_SCOPE;
            END_ISOLATE_SCOPE;
        }

        _EngineProxy = nullptr;
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

    _ObjectTemplate->SetNamedPropertyHandler(GetProperty, SetProperty, GetPropertyAttributes, DeleteProperty, GetPropertyNames);
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

    _ObjectTemplate->SetIndexedPropertyHandler(GetProperty, SetProperty, GetPropertyAttributes, DeleteProperty, GetPropertyIndices);
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::RegisterInvokeHandler(ManagedJSFunctionCallback callback)
{
    _ManagedCallback = callback;
    _ObjectTemplate->SetCallAsFunctionHandler(FunctionTemplateProxy::InvocationCallbackProxy, External::New(this));

}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::UnregisterNamedPropertyHandlers()
{
    _ObjectTemplate->SetNamedPropertyHandler((NamedPropertyGetterCallback)nullptr);
}

void ObjectTemplateProxy::UnregisterIndexedPropertyHandlers()
{
    _ObjectTemplate->SetIndexedPropertyHandler((IndexedPropertyGetterCallback)nullptr);
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::GetProperty(Local<String> hName, const PropertyCallbackInfo<Value>& info)
{
    auto obj = info.Holder();

    if (obj->InternalFieldCount() > 1)
    {
        auto field = obj->GetInternalField(0);
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->_EngineProxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                auto str = proxy->_EngineProxy->GetNativeString(*hName); // TODO: This can be faster - no need to allocate every time!
                auto result = proxy->NamedPropertyGetter(str.String, maInfo); // (assumes the 'str' memory will be released by the managed side)
                str.Dispose();
                if (result != nullptr) 
                    if (result->IsError())
                        info.GetReturnValue().Set(ThrowException(Exception::Error(result->Handle()->ToString())));
                    else
                        info.GetReturnValue().Set(result->Handle()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
                // (result == null == undefined [which means the managed side didn't return anything])
            }
        }
    }
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::SetProperty(Local<String> hName, Local<Value> value, const PropertyCallbackInfo<Value>& info)
{
    auto obj = info.Holder();

    if (obj->InternalFieldCount() > 1)
    {
        auto field = obj->GetInternalField(0);
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                auto str = proxy->_EngineProxy->GetNativeString(*hName);
                HandleProxy *val = proxy->_EngineProxy->GetHandleProxy(value);
                auto result = proxy->NamedPropertySetter(str.String, val, maInfo); // (assumes the 'str' memory will be released by the managed side)
                str.Dispose();
                if (result != nullptr)
                    if (result->IsError())
                        info.GetReturnValue().Set(ThrowException(Exception::Error(result->Handle()->ToString())));
                    else
                        info.GetReturnValue().Set(result->Handle()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
                // (result == null == undefined [which means the managed side didn't return anything])
            }
        }
    }
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::GetPropertyAttributes(Local<String> hName, const PropertyCallbackInfo<Integer>& info)
{
    auto obj = info.Holder();

    if (obj->InternalFieldCount() > 1)
    {
        auto field = obj->GetInternalField(0);
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                auto str = proxy->_EngineProxy->GetNativeString(*hName);
                int result = proxy->NamedPropertyQuery(str.String, maInfo); // (assumes the 'str' memory will be released by the managed side)
                str.Dispose();
                if (result >= 0)
                    info.GetReturnValue().Set(Handle<v8::Integer>(v8::Integer::New(result)));
            }
        }
    }
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::DeleteProperty(Local<String> hName, const PropertyCallbackInfo<Boolean>& info)
{
    auto obj = info.Holder();

    if (obj->InternalFieldCount() > 1)
    {
        auto field = obj->GetInternalField(0);
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                auto str = proxy->_EngineProxy->GetNativeString(*hName);
                int result = proxy->NamedPropertyDeleter(str.String, maInfo); // (assumes the 'str' memory will be released by the managed side)
                str.Dispose();

                // if 'result' is < 0, then this represents an "undefined" return value, otherwise 0 == false, and > 0 is true.

                if (result >= 0)
                    info.GetReturnValue().Set(Handle<v8::Boolean>(v8::Boolean::New(result != 0 ? true : false)));
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
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                auto result = proxy->NamedPropertyEnumerator(maInfo); // (assumes the 'str' memory will be released by the managed side)
                if (result != nullptr) 
                    if (result->IsError())
                    {
                        auto array = Array::New(1);
                        array->Set(0, ThrowException(Exception::Error(result->Handle()->ToString())));
                        info.GetReturnValue().Set(array);
                    }
                    else
                        info.GetReturnValue().Set(result->Handle().As<Array>()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
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
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                auto result = proxy->IndexedPropertyGetter(index, maInfo); // (assumes the 'str' memory will be released by the managed side)
                if (result != nullptr) 
                    if (result->IsError())
                        info.GetReturnValue().Set(ThrowException(Exception::Error(result->Handle()->ToString())));
                    else
                        info.GetReturnValue().Set(result->Handle()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
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
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                HandleProxy *val = proxy->_EngineProxy->GetHandleProxy(value);
                auto result = proxy->IndexedPropertySetter(index, val, maInfo); // (assumes the 'str' memory will be released by the managed side)
                if (result != nullptr) 
                    if (result->IsError())
                        info.GetReturnValue().Set(ThrowException(Exception::Error(result->Handle()->ToString())));
                    else
                        info.GetReturnValue().Set(result->Handle()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
                // (result == null == undefined [which means the managed side didn't return anything])
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
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                int result = proxy->IndexedPropertyQuery(index, maInfo); // (assumes the 'str' memory will be released by the managed side)
                if (result >= 0)
                    info.GetReturnValue().Set(Handle<v8::Integer>(v8::Integer::New(result)));
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
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                int result = proxy->IndexedPropertyDeleter(index, maInfo); // (assumes the 'str' memory will be released by the managed side)

                // if 'result' is < 0, then this represents an "undefined" return value, otherwise 0 == false, and > 0 is true.

                if (result >= 0)
                    info.GetReturnValue().Set(Handle<v8::Boolean>(v8::Boolean::New(result != 0 ? true : false)));
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
        if (!field->IsUndefined())
        {
            auto proxy = reinterpret_cast<ObjectTemplateProxy*>(obj->GetAlignedPointerFromInternalField(0));

            if (proxy != nullptr && proxy->Type == ObjectTemplateProxyClass)
            {
                auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value();
                ManagedAccessorInfo maInfo(proxy, managedObjectID, info);
                auto result = proxy->IndexedPropertyEnumerator(maInfo); // (assumes the 'str' memory will be released by the managed side)
                if (result != nullptr) 
                    if (result->IsError())
                    {
                        auto array = Array::New(1);
                        array->Set(0, ThrowException(Exception::Error(result->Handle()->ToString())));
                        info.GetReturnValue().Set(array);
                    }
                    else
                        info.GetReturnValue().Set(result->Handle().As<Array>()); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
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

    auto obj = _ObjectTemplate->NewInstance();
    auto proxyVal = _EngineProxy->GetHandleProxy(obj);
    ConnectObject(proxyVal, managedObjectID, this);
    return proxyVal;
}

// ------------------------------------------------------------------------------------------------------------------------

void ObjectTemplateProxy::AccessorGetterCallbackProxy(Local<String> property, const PropertyCallbackInfo<Value>& info)
{
    auto obj = info.Holder();
    auto ret = info.GetReturnValue();

    if (!obj.IsEmpty())
    {
        auto hAccessors = info.Data().As<Array>(); // [0] = ManagedObjectID, [1] == getter, [2] == setter
        if (hAccessors->Length() == 3)
        {
            auto hID= hAccessors->Get(0).As<Integer>(); // ManagedObjectID
            auto managedObjectID = (int32_t)hID->Int32Value();
            //??if (managedObjectID < 0 && obj->InternalFieldCount() >= 2) // (if the object ID is < 0 [usually -1] then try to detect the ID in other ways)
            //    auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value(); // (for ObjectTemplate objects)
            //else
            //{
            //    auto hHiddenObjID = obj->GetHiddenValue(String::New("ManagedObjectID")); // (for non-ObjectTemplate objects)
            //    if (!hHiddenObjID.IsEmpty() && hHiddenObjID->IsInt32())
            //        managedObjectID = (int32_t)hHiddenObjID->Int32Value();
            //}

            auto engine = (V8EngineProxy*)info.GetIsolate()->GetData();
            ManagedAccessorGetter getter = (ManagedAccessorGetter)hAccessors->Get(1).As<External>()->Value();

            if (getter != nullptr)
            {
                auto _this = engine->GetHandleProxy(info.This());
                if (managedObjectID >= 0) _this->_ObjectID = managedObjectID; // (use any explicitly specified object ID)

                auto str = engine->GetNativeString(*property);

                auto result = getter(_this, str.String); // (assumes the 'str' memory will be released by the managed side)

                str.Dispose();

                Handle<Value> hResult;

                if (result != nullptr)  
                    if (result->IsError())
                        hResult = ThrowException(Exception::Error(result->Handle()->ToString())); // TODO: Look into associating the returned error type as well (very low priority)
                    else
                        hResult = result->Handle(); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
                // (result == null == undefined [which means the managed side didn't return anything])

                ret.Set(hResult);
                return;
            }
        }
    }

    ret.SetUndefined();
}

void ObjectTemplateProxy::AccessorSetterCallbackProxy(Local<String> property, Local<Value> value, const PropertyCallbackInfo<void>& info)
{
    auto obj = info.Holder();
    auto ret = info.GetReturnValue();

    if (!obj.IsEmpty())
    {
        auto hAccessors = info.Data().As<Array>(); // [0] = ManagedObjectID, [1] == getter, [2] == setter
        if (hAccessors->Length() == 3)
        {
            auto hID= hAccessors->Get(0).As<Integer>(); // ManagedObjectID
            auto managedObjectID = (int32_t)hID->Int32Value();
            //??if (managedObjectID < 0 && obj->InternalFieldCount() >= 2) // (if the object ID is < 0 [usually -1] then try to detect the ID in other ways)
            //    auto managedObjectID = (int32_t)obj->GetInternalField(1).As<External>()->Value(); // (for ObjectTemplate objects)
            //else
            //{
            //    auto hHiddenObjID = obj->GetHiddenValue(String::New("ManagedObjectID")); // (for non-ObjectTemplate objects)
            //    if (!hHiddenObjID.IsEmpty() && hHiddenObjID->IsInt32())
            //        managedObjectID = (int32_t)hHiddenObjID->Int32Value();
            //}

            auto engine = (V8EngineProxy*)info.GetIsolate()->GetData();
            ManagedAccessorSetter setter = (ManagedAccessorSetter)hAccessors->Get(2).As<External>()->Value();

            if (setter != nullptr)
            {
                auto _this = engine->GetHandleProxy(info.This());
                if (managedObjectID >= 0) _this->_ObjectID = managedObjectID; // (use any explicitly specified object ID)

                auto str = engine->GetNativeString(*property);
                auto _value = engine->GetHandleProxy(value);

                auto result = setter(_this, str.String, _value); // (assumes the 'str' memory will be released by the managed side)

                str.Dispose();

                Handle<Value> hResult;

                if (result != nullptr)  
                    if (result->IsError())
                        hResult = ThrowException(Exception::Error(result->Handle()->ToString())); // TODO: Look into associating the returned error type as well (very low priority)
                    else
                        hResult = result->Handle(); // (the result was create via p/invoke calls, but is expected to be tracked and freed on the managed side)
                // (result == null == undefined [which means the managed side didn't return anything])

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
    auto accessors = Array::New(3); // [0] == ManagedObjectID, [1] == getter, [2] == setter
    accessors->Set(0, Integer::New(managedObjectID));
    accessors->Set(1, External::New(getter));
    accessors->Set(2, External::New(setter));
    _ObjectTemplate->SetAccessor(String::New(name), AccessorGetterCallbackProxy, AccessorSetterCallbackProxy, accessors, access, attributes);  // TODO: Check how this affects objects created from templates!
}

void ObjectTemplateProxy::Set(const uint16_t *name, HandleProxy *value, v8::PropertyAttribute attributes)
{
    _ObjectTemplate->Set(String::New(name), value->Handle(), attributes);  // TODO: Check how this affects objects created from templates!
}

// ------------------------------------------------------------------------------------------------------------------------
