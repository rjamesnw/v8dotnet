// https://thlorenz.github.io/v8-dox/build/v8-3.25.30/html/index.html (more up to date)

#include "ProxyTypes.h"

// ------------------------------------------------------------------------------------------------------------------------

_StringItem::_StringItem() : String(nullptr), Length(0) { }
_StringItem::_StringItem(V8EngineProxy *engine, size_t length)
{
	Engine = engine;
	Length = length;
	String = (uint16_t*)ALLOC_MANAGED_MEM(sizeof(uint16_t) * (length + 1));
}
_StringItem::_StringItem(V8EngineProxy *engine, v8::String* str)
{
	Engine = engine;
	Length = str->Length();
	String = (uint16_t*)ALLOC_MANAGED_MEM(sizeof(uint16_t) * (Length + 1));
	str->Write(String);
}

void _StringItem::Free() { if (String != nullptr) { FREE_MANAGED_MEM(String); String = nullptr; } }

_StringItem _StringItem::ResizeIfNeeded(size_t newLength)
{
	if (newLength > Length)
	{
		Length = newLength;
		String = (uint16_t*)REALLOC_MANAGED_MEM(String, sizeof(uint16_t) * (Length + 1));
	}
	return *this;
}
void _StringItem::Dispose() { if (Engine != nullptr) Engine->DisposeNativeString(*this); }
void _StringItem::Clear() { String = nullptr; Length = 0; }

// ------------------------------------------------------------------------------------------------------------------------

static bool _V8Initialized = false;

vector<bool> V8EngineProxy::_DisposedEngines(100, false);

int32_t V8EngineProxy::_NextEngineID = 0;

// ------------------------------------------------------------------------------------------------------------------------

bool V8EngineProxy::IsDisposed(int32_t engineID)
{
	return _DisposedEngines[engineID];
}

// ------------------------------------------------------------------------------------------------------------------------

Isolate* V8EngineProxy::Isolate() { return _Isolate; }

Handle<v8::Context> V8EngineProxy::Context() { return _Context; }

// ------------------------------------------------------------------------------------------------------------------------

V8EngineProxy::V8EngineProxy(bool enableDebugging, DebugMessageDispatcher* debugMessageDispatcher, int debugPort)
	:ProxyBase(V8EngineProxyClass), _GlobalObjectTemplateProxy(nullptr),
	_Strings(1000, _StringItem()), _Handles(1000, nullptr), _DisposedHandles(1000, -1), _NextNonTemplateObjectID(-2)
{
	if (!_V8Initialized) // (the API changed: https://groups.google.com/forum/#!topic/v8-users/wjMwflJkfso)
	{
		v8::V8::InitializePlatform(v8::platform::CreateDefaultPlatform());
		v8::V8::InitializeICU();
		v8::V8::Initialize();
		_V8Initialized = true;
	}

	_Isolate = Isolate::New();

	BEGIN_ISOLATE_SCOPE(this);

	_Handles.clear();
	_DisposedHandles.clear();
	_Strings.clear();

	_ManagedV8GarbageCollectionRequestCallback = nullptr;

	_Isolate->SetData(0, this); // (sets a reference in the isolate to the proxy [useful within callbacks])

	if ((vector<bool>::size_type)_NextEngineID >= _DisposedEngines.capacity())
		_DisposedEngines.resize(_DisposedEngines.capacity() + 32);

	if (_NextEngineID == 0)
		_DisposedEngines.clear(); // (need to clear the pre-allocated vector on first use)
	_DisposedEngines.push_back(false);
	_EngineID = _NextEngineID++;

	END_ISOLATE_SCOPE;
}

// ------------------------------------------------------------------------------------------------------------------------

V8EngineProxy::~V8EngineProxy()
{
	if (Type != 0) // (type is 0 if this class was wiped with 0's {if used in a marshalling test})
	{
		lock_guard<recursive_mutex> handleSection(_HandleSystemMutex);

		BEGIN_ISOLATE_SCOPE(this);

		// ... empty all handles to be sure they won't be accessed ...

		for (size_t i = 0; i < _Handles.size(); i++)
			_Handles[i]->_ClearHandleValue();

		// ... flag engine as disposed ...

		_DisposedEngines[_EngineID] = true; // (this supports cases where the engine may be deleted while proxy objects are still in memory)
		// (note: once this flag is set, disposing handles causes the proxy instances to be deleted immediately [instead of caching])

		// ... deleted disposed proxy handles ...

		// At this point the *disposed* (and hence, *cached*) proxy handles are no longer associated with managed handles, so the engine is now responsible to delete them)
		for (size_t i = 0; i < _DisposedHandles.size(); i++)
			_Handles[_DisposedHandles[i]]->_Dispose(false); // (engine is flagged as disposed, so this call will only delete the instance)

		// Note: the '_GlobalObjectTemplateProxy' instance is not deleted because the managed GC will do that later (if not before this).
		_GlobalObjectTemplateProxy = nullptr;

		_GlobalObject.Reset();
		_Context.Reset();

		END_ISOLATE_SCOPE;

		_Isolate->Dispose();
		_Isolate = nullptr;

		// ... free the string cache ...

		for (size_t i = 0; i < _Strings.size(); i++)
			_Strings[i].Free();
	}
}

// ------------------------------------------------------------------------------------------------------------------------

/**
* Converts a given V8 string into a uint16_t* string using ALLOC_MANAGED_MEM().
* The string is expected to be freed by calling FREE_MANAGED_MEM(), or within a managed assembly.
*/
_StringItem V8EngineProxy::GetNativeString(v8::String* str)
{
	_StringItem _str;

	auto size = _Strings.size();

	if (size > 0)
	{
		_str = _Strings[size - 1].ResizeIfNeeded(str->Length());
		_Strings[size - 1].Clear();
		_Strings.pop_back();
	}
	else
	{
		_str = _StringItem(this, str->Length());
	}

	str->Write(_str.String);
	return _str;
}

/**
* Puts the string back into the cache for reuse.
*/
void V8EngineProxy::DisposeNativeString(_StringItem &item)
{
	_Strings.push_back(item);
}

// ------------------------------------------------------------------------------------------------------------------------
/*
 * Returns a proxy wrapper for the given handle to allow access via the managed side.
 */
HandleProxy* V8EngineProxy::GetHandleProxy(Handle<Value> handle)
{
	std::lock_guard<std::recursive_mutex> handleSection(_HandleSystemMutex);

	HandleProxy* handleProxy;

	if (_DisposedHandles.size() > 0)
	{
		auto id = _DisposedHandles.back();
		_DisposedHandles.pop_back();
		handleProxy = _Handles.at(id);
		handleProxy->Initialize(handle);
	}
	else
	{
		handleProxy = (new HandleProxy(this, (int32_t)_Handles.size()))->Initialize(handle);
		_Handles.push_back(handleProxy); // (keep a record of all handles created)

		//_Isolate->IdleNotification(100); // (handles should not have to be created all the time, so this helps to free them up)
	}

	return handleProxy;
}

void V8EngineProxy::DisposeHandleProxy(HandleProxy *handleProxy)
{
	std::lock_guard<std::recursive_mutex> handleSection(_HandleSystemMutex); // NO V8 HANDLE ACCESS HERE BECAUSE OF THE MANAGED GC

	if (handleProxy->_Dispose(false))
		_DisposedHandles.push_back(handleProxy->_ID); // (this is a queue of disposed handles to use for recycling; Note: the persistent handles are NEVER disposed until they become reinitialized)
}

// ------------------------------------------------------------------------------------------------------------------------

void  V8EngineProxy::RegisterGCCallback(ManagedV8GarbageCollectionRequestCallback managedV8GarbageCollectionRequestCallback)
{
	_ManagedV8GarbageCollectionRequestCallback = managedV8GarbageCollectionRequestCallback;
}

// ------------------------------------------------------------------------------------------------------------------------

ObjectTemplateProxy* V8EngineProxy::CreateObjectTemplate()
{
	return new ObjectTemplateProxy(this);
}

FunctionTemplateProxy* V8EngineProxy::CreateFunctionTemplate(uint16_t *className, ManagedJSFunctionCallback callback)
{
	return new FunctionTemplateProxy(this, className, callback);
}

// ------------------------------------------------------------------------------------------------------------------------

HandleProxy* V8EngineProxy::SetGlobalObjectTemplate(ObjectTemplateProxy* proxy)
{
	if (!_Context.IsEmpty())
		_Context.Reset();

	if (_GlobalObjectTemplateProxy != nullptr)
		delete _GlobalObjectTemplateProxy;

	_GlobalObjectTemplateProxy = proxy;

	auto context = v8::Context::New(_Isolate, nullptr, Local<ObjectTemplate>::New(_Isolate, _GlobalObjectTemplateProxy->_ObjectTemplate));

	_Context = context;

	// ... the context auto creates the global object from the given template, BUT, we still need to update the internal fields with proper values expected
	// for callback into managed code ...

	auto globalObject = context->Global()->GetPrototype()->ToObject();
	globalObject->SetAlignedPointerInInternalField(0, _GlobalObjectTemplateProxy); // (proxy object reference)
	globalObject->SetInternalField(1, External::New(_Isolate, (void*)-1)); // (manage object ID, which is only applicable when tracking many created objects [and not a single engine or global scope])

	_GlobalObject = globalObject; // (keep a reference to the global object for faster reference)

	return GetHandleProxy(globalObject); // (the native side will own this, and is responsible to free it when done)
}

// ------------------------------------------------------------------------------------------------------------------------
// ??
//// To support the nature of V8, the managed side is required to select a scope to execute delegates in (Isolate, Context, or Handle based). This is why the macros only exist here. ;)
//void V8EngineProxy::WithIsolateScope(CallbackAction action)
//{
//    BEGIN_ISOLATE_SCOPE(this);
//    if (action != nullptr) action();
//    END_ISOLATE_SCOPE;
//}
//
//// ------------------------------------------------------------------------------------------------------------------------
// ??
//// To support the nature of V8, the managed side is required to select a scope to execute delegates in (Isolate, Context, or Handle based). This is why the macros only exist here. ;)
//void V8EngineProxy::WithContextScope(CallbackAction action)
//{
//    BEGIN_ISOLATE_SCOPE(this);
//    BEGIN_CONTEXT_SCOPE(this);
//    if (action != nullptr) action();
//    END_CONTEXT_SCOPE;
//    END_ISOLATE_SCOPE;
//}
//
//// ------------------------------------------------------------------------------------------------------------------------
// ??
//// To support the nature of V8, the managed side is required to select a scope to execute delegates in (Isolate, Context, or Handle based). This is why the macros only exist here. ;)
//void V8EngineProxy::WithHandleScope(CallbackAction action)
//{
//    BEGIN_HANDLE_SCOPE(this);
//    if (action != nullptr) action();
//    END_HANDLE_SCOPE;
//}

// ------------------------------------------------------------------------------------------------------------------------

Local<String> V8EngineProxy::GetErrorMessage(TryCatch &tryCatch)
{
	auto msg = tryCatch.Exception()->ToString();

	auto stack = tryCatch.StackTrace();
	bool showStackMsg = !stack.IsEmpty() && !stack->IsUndefined();
	Local<String> stackStr;

	if (showStackMsg)
	{
		stackStr = stack->ToString();

		// ... detect if the start of the stack message is the same as the exception message, then remove it (seems to happen when managed side returns an error) ...

		if (stackStr->Length() >= msg->Length())
		{
			uint16_t* ss = new uint16_t[stackStr->Length() + 1];
			stack->ToString()->Write(ss);
			auto subStackStr = NewSizedUString(ss, msg->Length());
			auto stackPartStr = NewSizedUString(ss + msg->Length(), stackStr->Length() - msg->Length());
			delete[] ss;

			if (msg->Equals(subStackStr))
				stackStr = stackPartStr;
		}
	}

	msg = msg->Concat(msg, NewString("\r\n"));

	msg = msg->Concat(msg, NewString("  Line: "));
	auto line = NewInteger(tryCatch.Message()->GetLineNumber())->ToString();
	msg = msg->Concat(msg, line);

	msg = msg->Concat(msg, NewString("  Column: "));
	auto col = NewInteger(tryCatch.Message()->GetStartColumn())->ToString();
	msg = msg->Concat(msg, col);
	msg = msg->Concat(msg, NewString("\r\n"));

	if (showStackMsg)
	{
		msg = msg->Concat(msg, NewString("  Stack: "));
		msg = msg->Concat(msg, stackStr);
		msg = msg->Concat(msg, NewString("\r\n"));
	}

	return msg;
}

HandleProxy* V8EngineProxy::Execute(const uint16_t* script, uint16_t* sourceName)
{
	HandleProxy *returnVal = nullptr;

	try
	{

		TryCatch __tryCatch;
		//__tryCatch.SetVerbose(true);

		if (sourceName == nullptr) sourceName = (uint16_t*)L"";

		auto compiledScript = Script::Compile(NewUString(script), NewUString(sourceName));

		if (__tryCatch.HasCaught())
		{
			returnVal = GetHandleProxy(GetErrorMessage(__tryCatch));
			returnVal->_Type = JSV_CompilerError;
		}
		else
			returnVal = Execute(compiledScript);
	}
	catch (exception ex)
	{
		returnVal = GetHandleProxy(NewString(ex.what()));
		returnVal->_Type = JSV_InternalError;
	}

	return returnVal;
}

HandleProxy* V8EngineProxy::Execute(Handle<Script> script)
{
	HandleProxy *returnVal = nullptr;

	try
	{
		TryCatch __tryCatch;
		//__tryCatch.SetVerbose(true);

		auto result = script->Run();

		if (__tryCatch.HasCaught())
		{
			returnVal = GetHandleProxy(GetErrorMessage(__tryCatch));
			returnVal->_Type = JSV_ExecutionError;
		}
		else  returnVal = GetHandleProxy(result);
	}
	catch (exception ex)
	{
		returnVal = GetHandleProxy(NewString(ex.what()));
		returnVal->_Type = JSV_InternalError;
	}

	return returnVal;
}

HandleProxy* V8EngineProxy::Compile(const uint16_t* script, uint16_t* sourceName)
{
	HandleProxy *returnVal = nullptr;

	try
	{
		TryCatch __tryCatch;
		//__tryCatch.SetVerbose(true);

		if (sourceName == nullptr) sourceName = (uint16_t*)L"";

		auto hScript = NewUString(script);

		auto compiledScript = Script::Compile(hScript, NewUString(sourceName));

		if (__tryCatch.HasCaught())
		{
			returnVal = GetHandleProxy(GetErrorMessage(__tryCatch));
			returnVal->_Type = JSV_CompilerError;
		}
		else
		{
			returnVal = GetHandleProxy(Handle<Value>());
			returnVal->SetHandle(compiledScript);
			returnVal->_Value.V8String = _StringItem(this, *hScript).String;
		}
	}
	catch (exception ex)
	{
		returnVal = GetHandleProxy(NewString(ex.what()));
		returnVal->_Type = JSV_InternalError;
	}

	return returnVal;
}

// ------------------------------------------------------------------------------------------------------------------------

HandleProxy* V8EngineProxy::Call(HandleProxy *subject, const uint16_t *functionName, HandleProxy *_this, uint16_t argCount, HandleProxy** args)
{
	if (_this == nullptr) _this = subject; // (assume the subject is also "this" if not given)

	auto hThis = _this->Handle();
	if (hThis.IsEmpty() || !hThis->IsObject())
		throw exception("Call: The target instance handle ('this') does not represent an object.");

	auto hSubject = subject->Handle();
	Handle<Function> hFunc;

	if (functionName != nullptr) // (if no name is given, assume the subject IS a function object, otherwise get the property as a function)
	{
		if (hSubject.IsEmpty() || !hSubject->IsObject())
			throw exception("Call: The subject handle does not represent an object.");

		auto hProp = hSubject.As<Object>()->Get(NewUString(functionName));

		if (hProp.IsEmpty() || !hProp->IsFunction())
			throw exception("Call: The specified property does not represent a function.");

		hFunc = hProp.As<Function>();
	}
	else if (hSubject.IsEmpty() || !hSubject->IsFunction())
		throw exception("Call: The subject handle does not represent a function.");
	else
		hFunc = hSubject.As<Function>();

	TryCatch __tryCatch;

	Handle<Value> result;

	if (argCount > 0)
	{
		Handle<Value>* _args = new Handle<Value>[argCount];
		for (auto i = 0; i < argCount; i++)
			_args[i] = args[i]->Handle();
		result = hFunc->Call(hThis.As<Object>(), argCount, _args);
		delete[] _args;
	}
	else result = hFunc->Call(hThis.As<Object>(), 0, nullptr);

	HandleProxy *returnVal;

	if (__tryCatch.HasCaught())
	{
		returnVal = GetHandleProxy(GetErrorMessage(__tryCatch));
		returnVal->_Type = JSV_ExecutionError;
	}
	else returnVal = result.IsEmpty() ? nullptr : GetHandleProxy(result);

	return returnVal;
}

// ------------------------------------------------------------------------------------------------------------------------

HandleProxy* V8EngineProxy::CreateNumber(double num)
{
	return GetHandleProxy(NewNumber(num));
}

HandleProxy* V8EngineProxy::CreateInteger(int32_t num)
{
	return GetHandleProxy(NewInteger(num));
}

HandleProxy* V8EngineProxy::CreateBoolean(bool b)
{
	return GetHandleProxy(NewBool(b));
}

HandleProxy* V8EngineProxy::CreateString(const uint16_t* str)
{
	return GetHandleProxy(NewUString(str));
}

HandleProxy* V8EngineProxy::CreateError(const uint16_t* message, JSValueType errorType)
{
	if (errorType >= 0) throw exception("Invalid error type.");
	auto h = GetHandleProxy(NewUString(message));
	h->_Type = errorType;
	return h;
}
HandleProxy* V8EngineProxy::CreateError(const char* message, JSValueType errorType)
{
	if (errorType >= 0) throw exception("Invalid error type.");
	auto h = GetHandleProxy(NewString(message));
	h->_Type = errorType;
	return h;
}


HandleProxy* V8EngineProxy::CreateDate(double ms)
{
	return GetHandleProxy(NewDate(ms));
}

HandleProxy* V8EngineProxy::CreateObject(int32_t managedObjectID)
{
	if (managedObjectID == -1)
		managedObjectID = GetNextNonTemplateObjectID();

	auto handle = GetHandleProxy(NewObject());
	ConnectObject(handle, managedObjectID, nullptr);
	return handle;
}

HandleProxy* V8EngineProxy::CreateArray(HandleProxy** items, uint16_t length)
{
	Local<Array> array = NewArray(length);

	if (items != nullptr && length > 0)
		for (auto i = 0; i < length; i++)
			array->Set(i, items[i]->_Handle);

	return GetHandleProxy(array);
}

HandleProxy* V8EngineProxy::CreateArray(uint16_t** items, uint16_t length)
{
	Local<Array> array = NewArray(length);

	if (items != nullptr && length > 0)
		for (auto i = 0; i < length; i++)
			array->Set(i, NewUString(items[i]));

	return GetHandleProxy(array);
}

HandleProxy* V8EngineProxy::CreateNullValue()
{
	return GetHandleProxy(V8Null);
}

// ------------------------------------------------------------------------------------------------------------------------
