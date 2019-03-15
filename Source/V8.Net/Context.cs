using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;
using System.Dynamic;

namespace V8.Net
{
// ========================================================================================================================

    //public unsafe partial class V8Engine
    //{
    //x    internal readonly Dictionary<Int32, Dictionary<string, Delegate>> _Accessors = new Dictionary<Int32, Dictionary<string, Delegate>>();

    //    /// <summary>
    //    /// This is required in order prevent accessor delegates from getting garbage collected when used with P/Invoke related callbacks (a process called "thunking").
    //    /// </summary>
    //    /// <typeparam name="T">The type of delegate ('d') to store and return.</typeparam>
    //    /// <param name="key">A native pointer (usually a proxy object) to associated the delegate to.</param>
    //    /// <param name="d">The delegate to keep a strong reference to (expected to be of type 'T').</param>
    //    /// <returns>The same delegate passed in, cast to type of 'T'.</returns>
    //x    internal T _StoreAccessor<T>(Int32 id, string propertyName, T d) where T : class
    //    {
    //        Dictionary<string, Delegate> delegates;
    //        if (!_Accessors.TryGetValue(id, out delegates))
    //            _Accessors[id] = delegates = new Dictionary<string, Delegate>();
    //        delegates[propertyName] = (Delegate)(object)d;
    //        return d;
    //    }

    //    /// <summary>
    //    /// Returns true if there are any delegates associated with the given object reference.
    //    /// </summary>
    //x    internal bool _HasAccessors(Int32 id)
    //    {
    //        Dictionary<string, Delegate> delegates;
    //        return _Accessors.TryGetValue(id, out delegates) && delegates.Count > 0;
    //    }

    //    /// <summary>
    //    /// Clears any accessor delegates associated with the given object reference.
    //    /// </summary>
    //x    internal void _ClearAccessors(Int32 id)
    //    {
    //        Dictionary<string, Delegate> delegates;
    //        if (_Accessors.TryGetValue(id, out delegates))
    //            delegates.Clear();
    //    }
    //}

    // ========================================================================================================================

    /// <summary>
    ///     Represents a V8 context in which JavaScript is executed. You can call
    ///     <see cref="V8Engine.CreateContext(ObjectTemplate)"/> to create new executing contexts with a new default/custom
    ///     global object.
    /// </summary>
    /// <seealso cref="T:System.IDisposable"/>
    public unsafe class Context : IDisposable
    {
        internal NativeContext* _NativeContext;
        internal Context(NativeContext* nativeContext) { _NativeContext = nativeContext; }
        //~Context() { V8NetProxy.DeleteContext(_NativeContext); _NativeContext = null; }

        public void Dispose()
        {
            if (_NativeContext != null)
                V8NetProxy.DeleteContext(_NativeContext);
            _NativeContext = null;
        }

        public static implicit operator Context(NativeContext* ctx) => new Context(ctx);
        public static implicit operator NativeContext* (Context ctx) => ctx._NativeContext;
    }
}