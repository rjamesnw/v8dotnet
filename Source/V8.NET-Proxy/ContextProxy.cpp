#include "ProxyTypes.h"

// ------------------------------------------------------------------------------------------------------------------------

ContextProxy::ContextProxy(V8EngineProxy* engineProxy, CopyablePersistent<Context> context)
	:ProxyBase(ContextProxyClass), _EngineProxy(engineProxy), _EngineID(engineProxy->_EngineID), _Context(context)
{
}

ContextProxy::~ContextProxy()
{
	if (Type != 0 && !_Context.IsEmpty()) // (type is 0 if this class was wiped with 0's {if used in a marshalling test})
	{
		if (!V8EngineProxy::IsDisposed(_EngineID))
		{
			BEGIN_ISOLATE_SCOPE(_EngineProxy);
			//BEGIN_CONTEXT_SCOPE(_EngineProxy);

			_Context.Reset();

			//END_CONTEXT_SCOPE;
			END_ISOLATE_SCOPE;
		}

		_EngineProxy = nullptr;
	}
}

// ------------------------------------------------------------------------------------------------------------------------
