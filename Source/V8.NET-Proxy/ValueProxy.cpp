#include "ProxyTypes.h"

// ------------------------------------------------------------------------------------------------------------------------

HandleValue::HandleValue() : V8Number(0), _V8String(0)  { }

// ------------------------------------------------------------------------------------------------------------------------

HandleValue::~HandleValue()
{
    Dispose();
}

void HandleValue::Dispose()
{
    // This struct is only used within the 'HandleProxy' struct to marshal values to the managed side.  The struct is embedded as a part of the handle proxy instance.
    // 'Dispose()' is called to reset values and release memory when no longer needed.

    if (V8String != nullptr)
    {
        FREE_MANAGED_MEM(V8String)
            V8String = nullptr;
    }

    V8Number = 0;
}


// ------------------------------------------------------------------------------------------------------------------------
