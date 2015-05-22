#include "ProxyTypes.h"

// ------------------------------------------------------------------------------------------------------------------------

v8::Handle<Value> HandleProxy::Handle() { return _Handle; }
v8::Handle<Script> HandleProxy::Script() { return _Script; }

// ------------------------------------------------------------------------------------------------------------------------

HandleProxy::HandleProxy(V8EngineProxy* engineProxy, int32_t id)
	: ProxyBase(HandleProxyClass), _Type((JSValueType)-1), _ID(id), _ManagedReference(0), _ObjectID(-1), _CLRTypeID(-1), __EngineProxy(0)
{
	_EngineProxy = engineProxy;
	_EngineID = _EngineProxy->_EngineID;
}

// ------------------------------------------------------------------------------------------------------------------------

HandleProxy::~HandleProxy()
{
	if (Type != 0) // (type is 0 if this class was wiped with 0's {if used in a marshalling test})
	{
		_ClearHandleValue();
		_ObjectID = -1;
		_Disposed = 3;
		_ManagedReference = 0;
	}
}

// Sets the state if this instance to disposed (for safety, the handle is NOT deleted, only cached).
// (registerDisposal is false when called within 'V8EngineProxy.DisposeHandleProxy()' (to prevent a cyclical loop), or by the engine's destructor)
bool HandleProxy::_Dispose(bool registerDisposal)
{
	std::lock_guard<std::recursive_mutex>(_EngineProxy->_HandleSystemMutex); // NO V8 HANDLE ACCESS HERE BECAUSE OF THE MANAGED GC

	if (V8EngineProxy::IsDisposed(_EngineID))
		delete this; // (the engine is gone, so just destroy the memory [the managed side owns UNDISPOSED proxy handles - they are not deleted with the engine)
	else
		if (_Disposed == 1 || _Disposed == 2)
		{
			if (registerDisposal)
			{
				_EngineProxy->DisposeHandleProxy(this); // (REQUIRES '_ID' and '_ObjectID', so don't clear before this)
				return true;
			}

			_ClearHandleValue();

			_ObjectID = -1;
			_CLRTypeID = -1;
			_Disposed = 3;
			_ManagedReference = 0;
			_Type = JSV_Uninitialized;

			return true;
		};;

	return false; // (already disposed, or engine is gone)
}

bool HandleProxy::Dispose()
{
	return _Dispose(true);
}

bool HandleProxy::TryDispose()
{
	if (_Disposed == 0 && _ManagedReference < 2)
	{
		_Disposed = 1;
		return Dispose();
	}
	return false; // (already disposed, or the managed side has cloned it)
}

// ------------------------------------------------------------------------------------------------------------------------

HandleProxy* HandleProxy::Initialize(v8::Handle<Value> handle)
{
	if (_Disposed > 0) _Dispose(false); // (just resets whatever is needed)

	_Disposed = 0; // (MUST do this FIRST in order for any associated managed object ID to be pulled, otherwise it will remain -1)

	SetHandle(handle);

	return this;
}

// ------------------------------------------------------------------------------------------------------------------------

void HandleProxy::_ClearHandleValue()
{
	if (!_Handle.IsEmpty())
	{
		_Handle.Reset();
	}
	if (!_Script.IsEmpty())
	{
		_Script.Reset();
	}
	_Value.Dispose();
	_Type = JSV_Uninitialized;
	if (_Handle.IsWeak())
		throw exception("Still weak!");
}

HandleProxy* HandleProxy::SetHandle(v8::Handle<v8::Script> handle)
{
	_ClearHandleValue();

	_Script = CopyablePersistent<v8::Script>(handle);
	_Type = JSV_Script;

	return this;
}

HandleProxy* HandleProxy::SetHandle(v8::Handle<Value> handle)
{
	_ClearHandleValue();

	_Handle = CopyablePersistent<Value>(handle);

	if (_Handle.IsEmpty())
	{
		_Type = JSV_Undefined;
	}
	else if (_Handle->IsBoolean())
	{
		_Type = JSV_Bool;
	}
	else if (_Handle->IsBooleanObject()) // TODO: Validate this is correct.
	{
		_Type = JSV_BoolObject;
		GetManagedObjectID(); // (best to call this now for objects to prevent calling back into the native side again [also, prevents debugger errors when inspecting in a GC finalizer])
	}
	else if (_Handle->IsInt32())
	{
		_Type = JSV_Int32;
	}
	else if (_Handle->IsNumber())
	{
		_Type = JSV_Number;
	}
	else if (_Handle->IsNumberObject()) // TODO: Validate this is correct.
	{
		_Type = JSV_NumberObject;
		GetManagedObjectID(); // (best to call this now for objects to prevent calling back into the native side again [also, prevents debugger errors when inspecting in a GC finalizer])
	}
	else if (_Handle->IsString())
	{
		_Type = JSV_String;
	}
	else if (_Handle->IsStringObject())// TODO: Validate this is correct.
	{
		_Type = JSV_StringObject;
		GetManagedObjectID(); // (best to call this now for objects to prevent calling back into the native side again [also, prevents debugger errors when inspecting in a GC finalizer])
	}
	else if (_Handle->IsDate())
	{
		_Type = JSV_Date;
		GetManagedObjectID(); // (best to call this now for objects to prevent calling back into the native side again [also, prevents debugger errors when inspecting in a GC finalizer])
	}
	else if (_Handle->IsArray())
	{
		_Type = JSV_Array;
		GetManagedObjectID(); // (best to call this now for objects to prevent calling back into the native side again [also, prevents debugger errors when inspecting in a GC finalizer])
	}
	else if (_Handle->IsRegExp())
	{
		_Type = JSV_RegExp;
		GetManagedObjectID(); // (best to call this now for objects to prevent calling back into the native side again [also, prevents debugger errors when inspecting in a GC finalizer])
	}
	else if (_Handle->IsNull())
	{
		_Type = JSV_Null;
	}
	else if (_Handle->IsFunction())
	{
		_Type = JSV_Function;
		GetManagedObjectID(); // (best to call this now for objects to prevent calling back into the native side again [also, prevents debugger errors when inspecting in a GC finalizer])
	}
	else if (_Handle->IsExternal())
	{
		_Type = JSV_Undefined;
	}
	else if (_Handle->IsNativeError())
	{
		_Type = JSV_Undefined;
	}
	else if (_Handle->IsUndefined())
	{
		_Type = JSV_Undefined;
	}
	else if (_Handle->IsObject()) // WARNING: Do this AFTER any possible object type checks (example: creating functions makes this return true as well!!!)
	{
		_Type = JSV_Object;
		GetManagedObjectID(); // (best to call this now for objects to prevent calling back into the native side again [also, prevents debugger errors when inspecting in a GC finalizer])
	}
	else if (_Handle->IsFalse()) // TODO: Validate this is correct.
	{
		_Type = JSV_Bool;
	}
	else if (_Handle->IsTrue()) // TODO: Validate this is correct.
	{
		_Type = JSV_Bool;
	}
	else
	{
		_Type = JSV_Undefined;
	}

	return this;
}

void HandleProxy::_DisposeCallback(const WeakCallbackData<Value, HandleProxy>& data)
{
	//auto engineProxy = (V8EngineProxy*)isolate->GetData();
	//auto handleProxy = parameter;
	//?object.Reset();
}

// ------------------------------------------------------------------------------------------------------------------------

int32_t HandleProxy::SetManagedObjectID(int32_t id)
{
	// ... first, nullify any exiting mappings for the managed object ID ...
	if (_ObjectID >= 0 && _ObjectID < _EngineProxy->_Objects.size())
		_EngineProxy->_Objects[_ObjectID] = nullptr;

	_ObjectID = id;

	if (_ObjectID >= 0)
	{
		// ... store a mapping from managed object ID to this handle proxy ...
		if (_ObjectID >= _EngineProxy->_Objects.size())
			_EngineProxy->_Objects.resize((_ObjectID + 100) * 2, nullptr);

		_EngineProxy->_Objects[_ObjectID] = this;
	}
	else if (_ObjectID == -1)
		_ObjectID = _EngineProxy->GetNextNonTemplateObjectID(); // (must return something to associate accessor delegates, etc.)

	// ... detect if this is a special "type" object ...
	if (_ObjectID < -2 && _Handle->IsObject())
	{
		// ... use "duck typing" to determine if the handle is a valid TypeInfo object ...
		auto obj = _Handle.As<Object>();
		auto hTypeID = obj->Get(NewString("$__TypeID"));
		if (!hTypeID.IsEmpty() && hTypeID->IsInt32())
		{
			int32_t typeID = hTypeID->Int32Value();
			if (obj->Has(NewString("$__Value")))
			{
				_CLRTypeID = typeID;
			}
		}
	}

	return _ObjectID;
}

// Should be called once to attempt to pull the ID.
// If there's no ID, then the managed object ID will be set to -2 to prevent checking again.
// To force a re-check, simply set the value back to -1.
int32_t HandleProxy::GetManagedObjectID()
{
	if (_Disposed >= 3)
		return -1; // (no longer in use!)
	else if (_ObjectID < -1 || _ObjectID >= 0)
		return _ObjectID;
	else
		SetManagedObjectID(HandleProxy::GetManagedObjectID(_Handle));
}


// If the given handle is an object, this will attempt to pull the managed side object ID, or -1 otherwise.
int32_t HandleProxy::GetManagedObjectID(v8::Handle<Value> h)
{
	auto id = -1;

	if (!h.IsEmpty() && h->IsObject())
	{
		// ... if this was created by a template then there will be at least 2 fields set, so assume the second is a managed ID value, 
		// but if not, then check for a hidden property for objects not created by templates ...

		auto obj = h.As<Object>();

		if (obj->InternalFieldCount() > 1)
		{
			auto field = obj->GetInternalField(1); // (may be faster than hidden values)
			if (field->IsExternal())
				id = (int32_t)field.As<External>()->Value();
		}
		else
		{
			auto handle = obj->GetHiddenValue(NewString("ManagedObjectID"));
			if (!handle.IsEmpty() && handle->IsInt32())
				id = (int32_t)handle->Int32Value();
		}
	}

	return id;
}

// ------------------------------------------------------------------------------------------------------------------------

// This is called when the managed side is ready to destroy the V8 handle.
void HandleProxy::MakeWeak()
{
	if (GetManagedObjectID() >= 0 && _Disposed == 1)
	{
		_Handle.Value.SetWeak<HandleProxy>(this, _RevivableCallback);
		//?_Handle.Value.MarkIndependent();
		_Disposed = 2;
	}
}

// This is called when the managed side is no longer ready to destroy this V8 handle.
void HandleProxy::MakeStrong()
{
	if (_Disposed == 2)
	{
		_Handle.Value.ClearWeak();
		_Disposed = 1; // (roll back to managed-side "dispose ready" status; note: the managed side worker currently doesn't track this yet, so it's not supported)
	}
}

// ------------------------------------------------------------------------------------------------------------------------
// When the managed side is ready to destroy a handle, it first marks it as weak.  When the V8 engine's garbage collector finally calls back, the managed side
// object information is finally destroyed.

void HandleProxy::_RevivableCallback(const WeakCallbackData<Value, HandleProxy>& data)
{
	auto engineProxy = (V8EngineProxy*)data.GetIsolate()->GetData(0);
	auto handleProxy = data.GetParameter();

	auto dispose = true;

	if (engineProxy->_ManagedV8GarbageCollectionRequestCallback != nullptr)
	{
		if (handleProxy->_ObjectID >= 0)
			dispose = engineProxy->_ManagedV8GarbageCollectionRequestCallback(handleProxy);
	}

	if (dispose) // (Note: the managed callback may have already cached the handle, but the handle *value* will not be disposed yet)
	{
		handleProxy->_ClearHandleValue();
		// (V8 handle is no longer tracked on the managed side, so let it go within this GC request [better here while idle])
	}
}

// ------------------------------------------------------------------------------------------------------------------------

void HandleProxy::UpdateValue()
{
	if (_Type == JSV_Script) return;

	_Value.Dispose();

	switch (_Type)
	{
		// (note: ';' is prepended to prevent Visual Studio from formatting "switch..case" statements in a retarded manner (at least in VS2012!) )
		; case JSV_Null:
		{
			_Value.V8Number = 0;
			break;
		}
		case JSV_Bool:
		{
			_Value.V8Boolean = _Handle->BooleanValue();
			break;
		}
		case JSV_BoolObject:
		{
			_Value.V8Boolean = _Handle->BooleanValue();
			break;
		}
		case JSV_Int32:
		{
			_Value.V8Integer = _Handle->Int32Value();
			break;
		}
		case JSV_Number:
		{
			_Value.V8Number = _Handle->NumberValue();
			break;
		}
		case JSV_NumberObject:
		{
			_Value.V8Number = _Handle->NumberValue();
			break;
		}
		case JSV_String:
		{
			_Value.V8String = _StringItem(_EngineProxy, *_Handle.As<String>()).String; // (note: string is not disposed by struct object and becomes owned by this proxy!)
			break;
		}
		case JSV_StringObject:
		{
			_Value.V8String = _StringItem(_EngineProxy, *_Handle.As<String>()).String;
			break;
		}
		case JSV_Date:
		{
			_Value.V8Number = _Handle->NumberValue();
			_Value.V8String = _StringItem(_EngineProxy, *_Handle.As<String>()).String;
			break;
		}
		case JSV_Undefined:
		case JSV_Uninitialized:
		{
			_Value.V8Number = 0; // (make sure this is cleared just in case...)
			break;
		}
		default: // (by default, an "object" type is assumed (warning: this includes functions); however, we can't translate it (obviously), so we just return a reference to this handle proxy instead)
		{
			if (!_Handle.IsEmpty())
				_Value.V8String = _StringItem(_EngineProxy, *_Handle->ToString()).String;
			break;
		}
	}
}

// ------------------------------------------------------------------------------------------------------------------------
