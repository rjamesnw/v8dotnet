using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace V8.Net
{
    // ========================================================================================================================

    public class V8Exception : Exception
    {
        InternalHandle Handle { get { return _Handle; } }
        InternalHandle _Handle;

        public V8Exception(InternalHandle handle, Exception innerException)
            : base(handle.AsString, innerException)
        {
            _Handle.Set(handle);
        }

        public V8Exception(InternalHandle handle)
            : base(handle.AsString)
        {
            _Handle.Set(handle);
        }

        ~V8Exception() { _Handle.Dispose(); }
    }

    public class V8InternalErrorException : V8Exception
    {
        public V8InternalErrorException(InternalHandle handle) : base(handle) { }
        public V8InternalErrorException(InternalHandle handle, Exception innerException) : base(handle) { }
    }

    public class V8CompilerErrorException : V8Exception
    {
        public V8CompilerErrorException(InternalHandle handle) : base(handle) { }
        public V8CompilerErrorException(InternalHandle handle, Exception innerException) : base(handle) { }
    }

    public class V8ExecutionErrorException : V8Exception
    {
        public V8ExecutionErrorException(InternalHandle handle) : base(handle) { }
        public V8ExecutionErrorException(InternalHandle handle, Exception innerException) : base(handle) { }
    }

    // ========================================================================================================================
}
