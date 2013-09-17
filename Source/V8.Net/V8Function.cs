using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#if !(V1_1 || V2 || V3 || V3_5)
using System.Dynamic;
using System.Linq.Expressions;
#endif

namespace V8.Net
{
    // ========================================================================================================================

    /// <summary>
    /// The 'V8Function' inherits from V8NativeObject, which implements 'DynamicObject' for you, but if dynamic objects are not required,
    /// feel free to implement the 'IV8Function' interface for your own classes instead.
    /// </summary>
    public interface IV8Function : IV8NativeObject
    {
        /// <summary>
        /// A reference to the FunctionTemplate that created this object (once set, this should never change!)
        /// <para>Note: This simply calls 'V8Engine.GetObjectTemplate()'.</para>
        FunctionTemplate FunctionTemplate { get; }

        /// <summary>
        /// A managed callback reference, which is called when the javaScript function is called.
        /// You can dynamically update this at any time, or even set it to null.
        /// </summary>
        JSFunction Callback { get; set; }

        /// <summary>
        /// Calls the native side to invoke the function associated with this managed function wrapper.
        /// <para>Note: This simply calls 'base.Call()' without a function name.</para>
        /// </summary>
        InternalHandle Call(params InternalHandle[] args);

        /// <summary>
        /// Calls the native side to invoke the function associated with this managed function wrapper.
        /// The '_this' property is the "this" object within the function when called.
        /// <para>Note: This simply calls 'base.Call()' without a function name.</para>
        /// </summary>
        InternalHandle Call(InternalHandle _this, params InternalHandle[] args);
    }

    /// <summary>
    /// Represents a basic JavaScript function object.  By default, this object is used for the global environment.
    /// </summary>
    public class V8Function : V8NativeObject, IV8Function
    {
        // --------------------------------------------------------------------------------------------------------------------

        public FunctionTemplate FunctionTemplate { get { return (FunctionTemplate)base.Template; } }

        public JSFunction Callback { get; set; }

        // --------------------------------------------------------------------------------------------------------------------

        public V8Function()
            : base()
        {
        }

        public V8Function(IV8Function proxy)
            : base(proxy)
        {
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the native side to invoke the function associated with this managed function wrapper.
        /// <para>Note: This method simply calls 'Handle.Call()' without a function name.</para>
        /// </summary>
        public override InternalHandle Call(params InternalHandle[] args) { return _Handle._Handle._Call(null, InternalHandle.Empty, args); }

        /// <summary>
        /// Calls the native side to invoke the function associated with this managed function wrapper.
        /// The '_this' property is the "this" object within the function when called.
        /// <para>Note: This method simply calls 'Handle.Call()' without a function name.</para>
        /// </summary>
        public override InternalHandle Call(InternalHandle _this, params InternalHandle[] args) { return _Handle._Handle._Call(null, _this, args); }

        /// <summary>
        /// If the function object has a function property in itself (usually considered a static property in theory), you can use this to invoke it.
        /// </summary>
        public override InternalHandle Call(string functionName, InternalHandle _this, params InternalHandle[] args)
        {
            if (functionName.IsNullOrWhiteSpace()) throw new ArgumentNullException("functionName (cannot be null, empty, or only whitespace)");
            return _Handle.Call(functionName, _this, args); // (if a function name exists, then it is a request to get a property name on the object as a function [and not to use this function object itself])
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    /// <summary>
    /// This generic version of 'V8ManagedObject' allows injecting your own class by implementing the 'IV8ManagedObject' interface.
    /// </summary>
    /// <typeparam name="T">Your own class, which implements the 'IV8ManagedObject' interface.  Don't use the generic version if you are able to inherit from 'V8ManagedObject' instead.</typeparam>
    public unsafe class V8Function<T> : V8Function
        where T : IV8Function, new()
    {
        // --------------------------------------------------------------------------------------------------------------------

        public V8Function()
            : base(new T())
        {
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
