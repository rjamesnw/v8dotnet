// https://thlorenz.github.io/v8-dox/build/v8-3.25.30/html/index.html (more up to date)

#include "ProxyTypes.h"
#include <experimental/filesystem>

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

bool V8EngineProxy::IsExecutingScript()
{
	return _IsExecutingScript;
}

// ------------------------------------------------------------------------------------------------------------------------

Isolate* V8EngineProxy::Isolate() { return _Isolate; }

Handle<v8::Context> V8EngineProxy::Context() { return _Context; }

// ------------------------------------------------------------------------------------------------------------------------

V8EngineProxy::V8EngineProxy(bool enableDebugging, DebugMessageDispatcher* debugMessageDispatcher, int debugPort)
	:ProxyBase(V8EngineProxyClass), _GlobalObjectTemplateProxy(nullptr), _NextNonTemplateObjectID(-2),
	_IsExecutingScript(false), _IsTerminatingScript(false), _Handles(1000, nullptr), _DisposedHandles(1000, -1), _HandlesToBeMadeWeak(1000, nullptr),
	_HandlesToBeMadeStrong(1000, nullptr), _Objects(1000, nullptr), _Strings(1000, _StringItem())
{
	if (!_V8Initialized) // (the API changed: https://groups.google.com/forum/#!topic/v8-users/wjMwflJkfso)
	{
		v8::V8::InitializeICU();
	
		v8::V8::InitializeExternalStartupData(PLATFORM_TARGET "\\");

		auto platform = v8::platform::CreateDefaultPlatform();
		v8::V8::InitializePlatform(platform);

		v8::V8::Initialize();

		_V8Initialized = true;
	}

	auto params = Isolate::CreateParams();
	params.array_buffer_allocator = ArrayBuffer::Allocator::NewDefaultAllocator();
	_Isolate = Isolate::New(params);

	BEGIN_ISOLATE_SCOPE(this);

	_Handles.clear();
	_DisposedHandles.clear();
	_HandlesToBeMadeWeak.clear();
	_HandlesToBeMadeStrong.clear();
	_Objects.clear();
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
	HandleProxy* handleProxy = nullptr;

	// ... first check if this handle is an object with an ID, and if so, try to pull an existing handle ...

	auto id = HandleProxy::GetManagedObjectID(handle);

	if (id >= 0 && id < _Objects.size())
		handleProxy = _Objects.at(id);

	if (handleProxy == nullptr)
	{
		std::lock_guard<std::recursive_mutex> handleSection(_HandleSystemMutex);

		ProcessWeakStrongHandleQueue();

		if (_DisposedHandles.size() == 0)
		{
			// (no handles are disposed/cached, which means a new one is required)
			// ... try to trigger disposal of weak handles ...
			//if (_HandlesToBeMadeWeak.size() > 1000)
			_Isolate->IdleNotification(100); // (handles should not have to be created all the time, so this helps to free them up if too many start adding up in weak state)
		}

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

			ProcessWeakStrongHandleQueue(); // (process one more time to make this twice as fast as long as new handles are being created)
		}
	}

	return handleProxy;
}

void V8EngineProxy::DisposeHandleProxy(HandleProxy *handleProxy)
{
	std::lock_guard<std::recursive_mutex> handleSection(_HandleSystemMutex); // NO V8 HANDLE ACCESS HERE BECAUSE OF THE MANAGED GC

	if (handleProxy->_ObjectID >= 0 && handleProxy->_ObjectID < _Objects.size())
		_Objects[handleProxy->_ObjectID] = nullptr;

	if (handleProxy->_Dispose(false))
		_DisposedHandles.push_back(handleProxy->_ID); // (this is a queue of disposed handles to use for recycling; Note: the persistent handles are NEVER disposed until they become reinitialized)
}

// ------------------------------------------------------------------------------------------------------------------------

void V8EngineProxy::QueueMakeWeak(HandleProxy *handleProxy)
{
	if (handleProxy->IsDisposingManagedSide())
	{
		std::lock_guard<std::recursive_mutex> makeWeakSection(_MakeWeakQueueMutex); // NO V8 HANDLE ACCESS HERE BECAUSE OF THE MANAGED GC
		_HandlesToBeMadeWeak.push_back(handleProxy);
	}
}

void V8EngineProxy::QueueMakeStrong(HandleProxy *handleProxy) // TODO: "MakeStrong" requests may no longer be needed.
{
	if (handleProxy->IsWeak())
	{
		std::lock_guard<std::recursive_mutex> makeStrongSection(_MakeStrongQueueMutex); // NO V8 HANDLE ACCESS HERE BECAUSE OF THE MANAGED GC
		_HandlesToBeMadeStrong.push_back(handleProxy);
	}
}

void V8EngineProxy::ProcessWeakStrongHandleQueue()
{
	// ... process one of each per call ...

	HandleProxy * h;

	if (_HandlesToBeMadeWeak.size() > 0)
	{
		std::lock_guard<std::recursive_mutex> makeWeakSection(_MakeWeakQueueMutex); // PROTECTS AGAINST THE WORKER THREAD
		h = _HandlesToBeMadeWeak.back();
		_HandlesToBeMadeWeak.pop_back();
		h->MakeWeak();
	}

	if (_HandlesToBeMadeStrong.size() > 0)
	{
		std::lock_guard<std::recursive_mutex> makeStrongSection(_MakeStrongQueueMutex); // PROTECTS AGAINST THE WORKER THREAD
		h = _HandlesToBeMadeStrong.back();
		_HandlesToBeMadeStrong.pop_back();
		h->MakeStrong(); // TODO: "MakeStrong" requests may no longer be needed.
	}
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

Local<String> V8EngineProxy::GetErrorMessage(TryCatch &tryCatch)
{
	auto msg = tryCatch.Message();
	auto messageExists = !msg.IsEmpty();
	auto exception = tryCatch.Exception();
	auto exceptionExists = !exception.IsEmpty();
	auto stack = tryCatch.StackTrace();
	bool stackExists = !stack.IsEmpty() && !stack->IsUndefined();

	Local<String> stackStr;

	if (stackExists && exceptionExists)
	{
		stackStr = stack->ToString();

		auto exceptionMsg = tryCatch.Exception()->ToString();

		// ... detect if the start of the stack message is the same as the exception message, then remove it (seems to happen when managed side returns an error) ...

		if (stackStr->Length() >= exceptionMsg->Length())
		{
			uint16_t* ss = new uint16_t[stackStr->Length() + 1];
			stack->ToString()->Write(ss); // (copied to a new array in order to offset the character pointer to extract a substring)

			// ... get the same number of characters from the stack message as the exception message length ...
			auto subStackStr = NewSizedUString(ss, exceptionMsg->Length());

			if (exceptionMsg->Equals(subStackStr))
			{
				// ... using the known exception message length, ...
				auto stackPartStr = NewSizedUString(ss + exceptionMsg->Length(), stackStr->Length() - exceptionMsg->Length());
				stackStr = stackPartStr;
			}

			delete[] ss;
		}
	}

	auto msgStr = messageExists ? msg->Get() : NewString("");

	if (tryCatch.HasTerminated())
	{
		if (msgStr->Length() > 0)
			msgStr = msgStr->Concat(msgStr, NewString("\r\n"));
		msgStr = msgStr->Concat(msgStr, NewString("Script execution aborted by request."));
	}

	if (messageExists)
	{
		msgStr = msgStr->Concat(msgStr, NewString("\r\n"));

		msgStr = msgStr->Concat(msgStr, NewString("  Line: "));
		auto line = NewInteger(msg->GetLineNumber())->ToString();
		msgStr = msgStr->Concat(msgStr, line);

		msgStr = msgStr->Concat(msgStr, NewString("  Column: "));
		auto col = NewInteger(msg->GetStartColumn())->ToString();
		msgStr = msgStr->Concat(msgStr, col);
	}

	if (stackExists)
	{
		msgStr = msgStr->Concat(msgStr, NewString("\r\n"));

		msgStr = msgStr->Concat(msgStr, NewString("  Stack: "));
		msgStr = msgStr->Concat(msgStr, stackStr);
	}

	msgStr = msgStr->Concat(msgStr, NewString("\r\n"));

	return msgStr;
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

		_IsExecutingScript = true;
		auto result = script->Run();
		_IsExecutingScript = false;

		if (__tryCatch.HasCaught())
		{
			returnVal = GetHandleProxy(GetErrorMessage(__tryCatch));
			returnVal->_Type = __tryCatch.HasTerminated() ? JSV_ExecutionTerminated : JSV_ExecutionError;
		}
		else  returnVal = GetHandleProxy(result);

		_IsTerminatingScript = false;
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

void V8EngineProxy::TerminateExecution()
{
	if (_IsExecutingScript)
	{
		_IsExecutingScript = false;
		_IsTerminatingScript = true;
		_Isolate->TerminateExecution();
	}
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

Local<Private> V8EngineProxy::CreatePrivateString(const char* value)
{
	return Private::ForApi(_Isolate, NewString(value)); // ('ForApi' is required, otherwise a new "virtual" symbol reference of some sort will be created with the same name on each request [duplicate names, but different symbols virtually])
}

void V8EngineProxy::SetObjectPrivateValue(Local<Object> obj, const char* name, Local<Value> value)
{
	obj->SetPrivate(_Context, CreatePrivateString("ManagedObjectID"), value);
}

Local<Value> V8EngineProxy::GetObjectPrivateValue(Local<Object> obj, const char* name)
{
	auto phandle = obj->GetPrivate(_Context, CreatePrivateString("ManagedObjectID"));
	if (phandle.IsEmpty()) return V8Undefined;
	return phandle.ToLocalChecked();
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
