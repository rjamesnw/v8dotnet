using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace V8.Net
{
    // ========================================================================================================================

    /// <summary>
    /// Keeps track of native V8 handles (C++ native side).
    /// When all managed references are garbage collected, or disposed, then the native side is called to release the memory.
    /// </summary>
    public interface IHandle
    {
        /// <summary>
        /// The engine that owns the handle.
        /// </summary>
        V8Engine Engine { get; }

        /// <summary>
        /// The native V8 handle type.
        /// </summary>
        JSValueType ValueType { get; }

        /// <summary>
        /// Queries the native V8 array for its length.
        /// </summary>
        Int32 ArrayLength { get; }

        /// <summary>
        /// The ID of the managed object associated with this handle (if any).
        /// </summary>
        Int32 ManagedObjectID { get; }

        /// <summary>
        /// A reference to the managed object associated with this handle.
        /// Upon reading this property, if one is not found, a new basic 'V8NativeObject' instance will be created.
        /// <para>Instead of checking for 'null' (which should never work), query 'HasManagedObject' instead.</para>
        /// </summary>
        IV8NativeObject ManagedObject { get; }

        /// <summary>
        /// Returns true if this handle is associated with a managed object.
        /// </summary>
        bool HasManagedObject { get; }

        bool IsUndefined { get; }
        bool IsEmpty { get; }

        bool IsBoolean { get; }
        bool IsBooleanObject { get; }
        bool IsInt32 { get; }
        bool IsNumber { get; }
        bool IsNumberObject { get; }
        bool IsString { get; }
        bool IsStringObject { get; }
        bool IsObject { get; }
        bool IsFunction { get; }
        bool IsDate { get; }
        bool IsArray { get; }
        bool IsRegExp { get; }

        /// <summary>
        /// Returns true if the type is any one of the object types.
        /// </summary>
        bool IsObjectType { get; }

        /// <summary>
        /// Reading from this property causes a native call to fetch the current V8 value associated with this handle.
        /// </summary>
        object Value { get; }

        /// <summary>
        /// Once "Value" is accessed to retrieve the JavaScript value in real time, there's no need to keep using it.  Just call this property
        /// instead (a small bit faster).
        /// </summary>
        object LastValue { get; }

        /// <summary>
        /// Returns the 'Value' property type cast to the expected type.
        /// Warning: No conversion is made between different value types.
        /// </summary>
        DerivedType As<DerivedType>();

        /// <summary>
        /// Returns the 'LastValue' property type cast to the expected type.
        /// Warning: No conversion is made between different value types.
        /// </summary>
        DerivedType LastAs<DerivedType>();

        /// <summary>
        /// Returns the underlying value converted if necessary to a Boolean type.
        /// </summary>
        bool AsBoolean { get; }

        /// <summary>
        /// Returns the underlying value converted if necessary to an Int32 type.
        /// </summary>
        Int32 AsInt32 { get; }

        /// <summary>
        /// Returns the underlying value converted if necessary to a double type.
        /// </summary>
        double AsDouble { get; }

        /// <summary>
        /// Returns the underlying value converted if necessary to a string type.
        /// </summary>
        String AsString { get; }

        /// <summary>
        /// Returns the underlying value converted if necessary to a DateTime type.
        /// </summary>
        DateTime AsDate { get; }

        /// <summary>
        /// Returns this handle as a new JSProperty instance with default property attributes.
        /// Every call creates a new property object, which is why this is a method and not a property.
        /// </summary>
        IJSProperty AsJSProperty();

    }

    // ========================================================================================================================

    /// <summary>
    /// Keeps track of native V8 handles (C++ native side).
    /// When all managed references are garbage collected, or disposed, then the native side is called to release the memory.
    /// </summary>
    public interface IHandle<out T> : IHandle
    {
        new T Value { get; }
    }

    // ========================================================================================================================

    /// <summary>
    /// Assigned to handles to detect when they are no longer in use.
    /// </summary>
    internal class _ReferenceTag { }

    internal class _WeakReferenceTag : _ReferenceTag
    {
        public WeakReference<_ReferenceTag> WeakRefTag;
        public _WeakReferenceTag(_ReferenceTag refTag) { WeakRefTag = new WeakReference<_ReferenceTag>(refTag); }

        /// <summary>
        /// Returns true if the reference tag wrapped in this object was collected.
        /// </summary>
        public volatile bool Collected { get { _ReferenceTag tag; return WeakRefTag.TryGetTarget(out tag); } }
    }

    /// <summary>
    /// Keeps track of native V8 handles (C++ native side).
    /// When no more handles are in use, the native handle can be disposed when the V8.NET system is ready.
    /// If the handle is a value, the native handle side is disposed immediately - but if the handle represents a managed object, it waits until the managed
    /// object is also no longer in use.
    /// <para>Handles are very small values that can be passed around quickly on the stack, and as a result, the garbage collector is not involved as much.
    /// This helps prevent the GC from kicking in and slowing down applications when a lot of processing is in effect.
    /// Another benefit is that thread locking is required for heap memory allocation (for obvious reasons), so stack allocation is faster within a
    /// multi-threaded context.</para>
    /// </summary>
    public unsafe struct _Handle : IHandle // TODO: *** CAN THIS WORK BETTER ***
    {
        // --------------------------------------------------------------------------------------------------------------------

        internal _ReferenceTag _RefTag; // (this is '_WeakReferenceTag' if this is the root struct)
        internal HandleProxy* _HandleProxy;

        // --------------------------------------------------------------------------------------------------------------------

        internal ValueProxy* _ValueProxy { get { return _HandleProxy != null ? _HandleProxy->ValueProxy : null; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The JavaScript type this handle represents.
        /// </summary>
        public JSValueType ValueType { get { return _HandleProxy != null ? _HandleProxy->ValueType : JSValueType.Undefined; } }

        /// <summary>
        /// The ID of the managed object represented by this handle.
        /// This ID is expected when handles are passed to 'V8ManagedObject.GetObject()'.
        /// If this value is less than 0 (usually -1), then there is no associated managed object.
        /// </summary>
        public Int32 ManagedObjectID
        {
            get { return _HandleProxy != null ? _HandleProxy->ManagedObjectID : -1; }
            private set { if (_HandleProxy != null) _HandleProxy->ManagedHandleID = value; }
        }

        /// <summary>
        /// The ID of the engine represented by this handle. The ID is also the index into 'V8Engine.Engines[]'.
        /// </summary>
        public Int32 EngineID
        {
            get { return _HandleProxy != null ? _HandleProxy->EngineID : -1; }
        }

        // --------------------------------------------------------------------------------------------------------------------

        public _Handle(HandleProxy* handleProxy, _ReferenceTag refTag = null)
        {
            _HandleProxy = handleProxy;
            _RefTag = refTag ?? new _ReferenceTag();
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a copy of this handle with a strong reference tag to keep track of it.
        /// </summary>
        internal _Handle Clone()
        {
            _ReferenceTag refTag = null;
            if (_RefTag is _WeakReferenceTag)
                ((_WeakReferenceTag)_RefTag).WeakRefTag.TryGetTarget(out refTag);
            else
                refTag = _RefTag;
            return new _Handle(_HandleProxy, refTag);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Converts the strong reference tag into a weak one and also returns it (to prevent garbage collection, the returned reference must be used/stored).
        /// This method is called within '_ObjectInfo' to make the original structure a weak reference to allow garbage collection when the other handle copies are no longer in use.
        /// <para>If called more than once, the subsequent calls will return null, which means the reference tag was already made weak.</para>
        /// </summary>
        internal _ReferenceTag _MakeWeak()
        {
            if (!(_RefTag is _WeakReferenceTag))
            {
                var currentTag = _RefTag;
                _RefTag = new _WeakReferenceTag(_RefTag);
                return currentTag;
            }
            return null;
        }

        /// <summary>
        /// Makes the managed handle weak so that the native handle can be disposed when the value or object is no longer referenced by other strong handles.
        /// </summary>
        public bool MakeWeak() { return _MakeWeak() != null; }

        /// <summary>
        /// Returns true if the managed handle was collected and is no longer valid, and false otherwise.
        /// <para>Note: This only returns true if the managed handle is no longer in use; however, if a managed object (such as 'V8NativeObject') is still
        /// associated, the managed object will keep the native handle alive, even though the managed handle is not.</para>
        /// </summary>
        public bool IsCollected { get { return (_RefTag is _WeakReferenceTag) && ((_WeakReferenceTag)_RefTag).Collected; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A reference to the managed object associated with this handle. This property is only valid for object handles, and will return null otherwise.
        /// Upon reading this property, if the managed object has been garbage collected (because no more handles or references exist), then a new basic 'V8NativeObject' instance will be created.
        /// <para>Instead of checking for 'null' (which may not work as expected), query 'HasManagedObject' instead.</para>
        /// </summary>
        public IV8NativeObject ManagedObject
        {
            get { return HasManagedObject ? V8Engine._Engines[_HandleProxy->EngineID].GetObjectByID(_HandleProxy->ManagedObjectID) : null; }
        }

        /// <summary>
        /// Returns true if this handle is associated with a managed object.
        /// <para>Note: This can be false even though 'IsObjectType' may be true.  A handle can represent a native V8 object handle without requiring a managed object.</para>
        /// </summary>
        public bool HasManagedObject
        {
            get { return ManagedObjectID >= 0; }
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    /// <summary>
    /// Keeps track of native V8 handles (C++ native side).
    /// When all managed references are garbage collected, or disposed, then the native side is called to release the memory.
    /// The generic version should be used when creating new handles.
    /// </summary>
    public unsafe abstract class Handle : IHandle
    {
        // --------------------------------------------------------------------------------------------------------------------

        public static readonly Handle Empty = new Handle<object>(null);

        // --------------------------------------------------------------------------------------------------------------------

        public int ID { get { return _ID; } }
        internal int _ID;

        public V8Engine Engine { get { return _Engine; } }
        internal V8Engine _Engine; // (the engine this handle belongs to)

        internal HandleProxy* _HandleProxy;
        internal ValueProxy* _ValueProxy { get { return _HandleProxy != null ? _HandleProxy->ValueProxy : null; } }

        internal V8Engine._ObjectInfo _ManagedObjectInfo;

        /// <summary>
        /// The ID of the managed object represented by this handle.
        /// This ID is expected when handles are passed to 'V8ManagedObject.GetObject()'.
        /// If this value is less than 0 (usually -1), then there is no associated managed object.
        /// </summary>
        public Int32 ManagedObjectID
        {
            get { return _HandleProxy != null ? _HandleProxy->ManagedObjectID : -1; }
            private set { if (_HandleProxy != null) _HandleProxy->ManagedHandleID = value; }
        }

        // --------------------------------------------------------------------------------------------------------------------

        object _Value { get { return V8NetProxy.GetValueFromValueProxy(_ValueProxy); } }

        /// <summary>
        /// Reading from this property causes a native call to fetch the current V8 value associated with this handle.
        /// </summary>
        public virtual object Value
        {
            get
            {
                // ... get value from V8 ...

                if (!IsEmpty)
                {
                    V8NetProxy.UpdateValueProxy(_HandleProxy);
                    return _ValueProxy != null ? _Value : null;
                }
                else return null;
            }
        }

        /// <summary>
        /// Once "Value" is accessed to retrieve the JavaScript value in real time, there's no need to keep using it.  Just call this property
        /// instead (a small bit faster).
        /// </summary>
        public virtual object LastValue
        {
            get
            {
                return _ValueProxy != null ? _Value : null;
            }
        }

        /// <summary>
        /// Returns the array length for handles that represent arrays. For all other types, this returns 0.
        /// </summary>
        public Int32 ArrayLength { get { return IsArray ? V8NetProxy.GetArrayLength(_HandleProxy) : 0; } }

        // --------------------------------------------------------------------------------------------------------------------

        internal Handle(V8Engine engine)
        {
            _Engine = engine;
            _ID = -1;
        }

        internal bool _IsInPendingDisposalQueue;

        ~Handle()
        {
            if (_Engine != null)
                lock (_Engine._Handles) // (just in case something is updating the whole list)
                {
                    lock (this) // (some cases only require a lock on the current handle only, and not all handles)
                    {
                        _Engine._Handles[_ID].DoFinalize(this);

                        _TrySetPendingDisposal();
                    }
                }
        }

        /// <summary>
        /// Adds this handle to the pending disposal queue when both the handle and any associated managed object no longer have any references.
        /// <para>Note: This is also called from within '_ManagedObjectInfo' if one exists.</para>
        /// </summary>
        internal void _TrySetPendingDisposal()
        {
            if (!_IsInPendingDisposalQueue && _IsDisposeReady)
                lock (_Engine._HandlesPendingDisposal)
                {
                    _Engine._HandlesPendingDisposal.Add(this);
                    _IsInPendingDisposalQueue = true;
                }
        }

        internal void _UndoPendingDisposal()
        {
            if (_IsInPendingDisposalQueue)
                lock (_Engine._HandlesPendingDisposal)
                {
                    _Engine._HandlesPendingDisposal.Remove(this);
                    _IsInPendingDisposalQueue = false;
                }
        }

        /// <summary>
        /// Initializes the handle, linking it permanently with a specified handle proxy.
        /// </summary>
        internal Handle _Initialize(HandleProxy* hp)
        {
            _UndoPendingDisposal();

            _HandleProxy = hp;

            _ID = -1;
            _HandleProxy->ManagedHandleID = -1;

            return _Reset();
        }

        /// <summary>
        /// Resets the handle for reuse.
        /// </summary>
        /// <returns></returns>
        internal Handle _Reset()
        {
            _ManagedObjectInfo = null;
            //??_NativeHandleIsWeak = false;
            var owr = _Engine._Handles[_ID];
            if (owr != null)
                lock (owr) // (only need to update this one instance)
                {
                    owr.Reset();
                }

            GC.AddMemoryPressure((sizeof(HandleProxy) + sizeof(ValueProxy)));

            return this;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns true if this handle has no other references and is ready to be disposed.
        /// </summary>
        internal bool _IsWeakHandle { get { return _Engine._Handles[_ID].IsGCReady; } }

        /// <summary>
        /// Returns true if this handle is associated with a managed object that has no other references and is ready to be disposed.
        /// </summary>
        internal bool _IsWeakManagedObject { get { return _ManagedObjectInfo == null || _ManagedObjectInfo.IsManagedObjectWeak; } }

        /// <summary>
        /// Returns true when this handle is ready to be disposed (cached) on the native side (both '_IsWeakHandle' and '_IsWeakManagedObject' are true).
        /// </summary>
        internal bool _IsDisposeReady { get { return _IsWeakHandle && _IsWeakManagedObject; } }

        /// <summary>
        /// Returns true if this handle is disposed (no longer in use).  Disposed handles are kept in a cache for performance reasons.
        /// </summary>
        public bool IsDisposed { get { return _HandleProxy == null || _HandleProxy->IsDisposed; } }

        ///// <summary>
        ///// Called when this handle has no more managed references, and the native garbage collector wants to collect this instance.
        ///// <para>Note: Handle disposal process only completes when there are no more managed references to the associated managed object as well.</para>
        ///// </summary>
        //??internal bool _OnNativeGCRequested()
        //{
        //    // ... the native V8 engine is requesting garbage collection ...
        //    var dispose = _IsDisposeReady;
        //    if (dispose && _ManagedObjectInfo != null)
        //        lock (_Engine._Objects)
        //        {
        //            _Engine._Objects.Remove(_ManagedObjectInfo._ID);
        //        }
        //    return dispose;
        //}

        ///// <summary>
        ///// If this is true, then the native handle was made weak, and this handle instance is pending disposal.
        ///// </summary>
        //??internal bool _NativeHandleIsWeak;

        //??internal void _MakeWeakNativeHandle()
        //{
        //    if (!_NativeHandleIsWeak)
        //    {
        //        V8NetProxy.MakeWeakHandle(_HandleProxy);
        //        _NativeHandleIsWeak = true;
        //    }
        //}

        //??internal void _MakeStrongNativeHandle()
        //{
        //    if (_NativeHandleIsWeak)
        //    {
        //        V8NetProxy.MakeStrongHandle(_HandleProxy);
        //        _NativeHandleIsWeak = false;
        //    }
        //}

        /// <summary>
        /// Completes the disposal of the native handle.
        /// <para>Note: A disposed native handle is simply cached for reuse, and always points back to the same managed handle.</para>
        /// </summary>
        internal void _ForceDisposal()
        {
            if (!_IsWeakManagedObject)
                throw new InvalidOperationException("A managed object is still associated with this handle");

            lock (this)
            {
                if (!IsDisposed)
                {
                    //??if (_NativeHandleIsWeak)
                    //    _MakeStrongNativeHandle(); // (prevent any GC callback just in case)

                    V8NetProxy.DisposeHandleProxy(_HandleProxy);
                    // (note: '_HandleProxy' is NOT set to null here because the connection has to be maintained for cache purposes)

                    GC.RemoveMemoryPressure((sizeof(HandleProxy) + sizeof(ValueProxy)));
                }

                if (_ManagedObjectInfo != null)
                {
                    _ManagedObjectInfo.Dispose();
                    _ManagedObjectInfo = null;
                    ManagedObjectID = -1;
                }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        public static implicit operator HandleProxy*(Handle handle)
        {
            return handle != null ? handle._HandleProxy : null;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns true if this handle contains an error message (the string value is the message).
        /// If you have exception catching in place, you can simply call 'ThrowOnError()' instead.
        /// </summary>
        public bool IsError
        {
            get
            {
                return _HandleProxy != null &&
                    (_HandleProxy->ValueType == JSValueType.InternalError
                    || _HandleProxy->ValueType == JSValueType.CompilerError
                    || _HandleProxy->ValueType == JSValueType.ExecutionError);
            }
        }

        /// <summary>
        /// Checks if the handle represents an error, and if so, throws one of the corresponding derived V8Exception exceptions.
        /// See 'JSValueType' for possible exception states.  You can check the 'IsError' property to see if this handle represents an error.
        /// <para>Exceptions thrown: V8InternalErrorException, V8CompilerErrorException, V8ExecutionErrorException, and V8Exception (for any general V8-related exceptions).</para>
        /// </summary>
        public void ThrowOnError()
        {
            if (IsError)
                switch (_HandleProxy->ValueType)
                {
                    case JSValueType.InternalError: throw new V8InternalErrorException(this);
                    case JSValueType.CompilerError: throw new V8CompilerErrorException(this);
                    case JSValueType.ExecutionError: throw new V8ExecutionErrorException(this);
                    default: throw new V8Exception(this); // (this will only happen if 'IsError' contains a type check that doesn't have any corresponding exception object)
                }
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The JavaScript type this handle represents.
        /// </summary>
        public JSValueType ValueType { get { return _HandleProxy->ValueType; } }

        /// <summary>
        /// A reference to the managed object associated with this handle. This property is only valid for object handles, and will return null otherwise.
        /// Upon reading this property, if the managed object has been garbage collected (because no more handles or references exist), then a new basic 'V8NativeObject' instance will be created.
        /// <para>Instead of checking for 'null' (which may not work as expected), query 'HasManagedObject' instead.</para>
        /// </summary>
        public IV8NativeObject ManagedObject
        {
            get { return HasManagedObject ? _ManagedObjectInfo.ManagedObject : null; }
        }

        /// <summary>
        /// Returns true if this handle is associated with a managed object.
        /// <para>Note: This can be false even though 'IsObjectType' may be true.  A handle can represent a native V8 object handle without requiring a managed object.</para>
        /// </summary>
        public bool HasManagedObject
        {
            get
            {
                if (_ManagedObjectInfo == null && IsObjectType && ManagedObjectID >= 0)
                    _ManagedObjectInfo = _Engine._Objects[ManagedObjectID];

                return _ManagedObjectInfo != null;
            }
        }

        /// <summary>
        /// Returns true if this handle is undefined or empty (empty is when this handle is an instance of 'Handle.Empty').
        /// <para>"Undefined" does not mean "null".  A variable (handle) can be defined and set to "null".</para>
        /// </summary>
        public bool IsUndefined { get { return IsEmpty || _HandleProxy->ValueType == JSValueType.Undefined; } }

        /// <summary>
        /// Returns true if this handle is empty (that is, equal to 'Handle.Empty'), and false if a valid handle exists.
        /// <para>An empty state is when a handle is set to 'Handle.Empty' and has no valid native V8 handle assigned.
        /// This is similar to "undefined"; however, this property will be true if a valid native V8 handle exists that is set to "undefined".</para>
        /// </summary>
        public bool IsEmpty { get { return this == (Handle)Empty; } }

        public bool IsBoolean { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.Bool : false; } }
        public bool IsBooleanObject { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.BoolObject : false; } }
        public bool IsInt32 { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.Int32 : false; } }
        public bool IsNumber { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.Number : false; } }
        public bool IsNumberObject { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.NumberObject : false; } }
        public bool IsString { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.String : false; } }
        public bool IsStringObject { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.StringObject : false; } }
        public bool IsObject { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.Object : false; } }
        public bool IsFunction { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.Function : false; } }
        public bool IsDate { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.Date : false; } }
        public bool IsArray { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.Array : false; } }
        public bool IsRegExp { get { return _HandleProxy != null ? _HandleProxy->ValueType == JSValueType.RegExp : false; } }

        public bool IsObjectType
        {
            get
            {
                return _HandleProxy != null &&
                    (_HandleProxy->ValueType == JSValueType.BoolObject
                    || _HandleProxy->ValueType == JSValueType.NumberObject
                    || _HandleProxy->ValueType == JSValueType.StringObject
                    || _HandleProxy->ValueType == JSValueType.Function
                    || _HandleProxy->ValueType == JSValueType.Object);
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        public static implicit operator bool(Handle vpHandle)
        {
            return (bool)Types.ChangeType(vpHandle.Value, typeof(bool));
        }

        public static implicit operator Int32(Handle vpHandle)
        {
            return (Int32)Types.ChangeType(vpHandle.Value, typeof(Int32));
        }

        public static implicit operator double(Handle vpHandle)
        {
            return (double)Types.ChangeType(vpHandle.Value, typeof(double));
        }

        public static implicit operator string(Handle vpHandle)
        {
            var val = vpHandle.Value;
            if (val == null) return "";
            return (string)Types.ChangeType(val, typeof(string));
        }

        public static implicit operator DateTime(Handle vpHandle)
        {
            var ms = (double)Types.ChangeType(vpHandle.Value, typeof(double));
            return new DateTime(1970, 1, 1) + TimeSpan.FromMilliseconds(ms);
        }

        public static implicit operator JSProperty(Handle vpHandle)
        {
            return new JSProperty(vpHandle);
        }

        /// <summary>
        /// Returns the 'Value' property type cast to the expected type.
        /// Warning: No conversion is made between different value types.
        /// </summary>
        public DerivedType As<DerivedType>()
        {
            return _ValueProxy != null ? (DerivedType)_Value : default(DerivedType);
        }

        /// Returns the 'LastValue' property type cast to the expected type.
        /// Warning: No conversion is made between different value types.
        public DerivedType LastAs<DerivedType>()
        {
            return _ValueProxy != null ? (DerivedType)_Value : default(DerivedType);
        }

        /// <summary>
        /// Returns the underlying value converted if necessary to a Boolean type.
        /// </summary>
        public bool AsBoolean { get { return (bool)this; } }

        /// <summary>
        /// Returns the underlying value converted if necessary to an Int32 type.
        /// </summary>
        public Int32 AsInt32 { get { return (Int32)this; } }

        /// <summary>
        /// Returns the underlying value converted if necessary to a double type.
        /// </summary>
        public double AsDouble { get { return (double)this; } }

        /// <summary>
        /// Returns the underlying value converted if necessary to a string type.
        /// </summary>
        public String AsString { get { return (String)this; } }

        /// <summary>
        /// Returns the underlying value converted if necessary to a DateTime type.
        /// </summary>
        public DateTime AsDate { get { return (DateTime)this; } }

        /// <summary>
        /// Returns this handle as a new JSProperty instance with default property attributes.
        /// </summary>
        public IJSProperty AsJSProperty() { return (JSProperty)this; }

        // --------------------------------------------------------------------------------------------------------------------

        public override string ToString()
        {
            if (IsUndefined) return "undefined";
            if (IsObjectType)
            {
                string managedType = "";
                if (_ManagedObjectInfo != null && _ManagedObjectInfo._ManagedObject != null)
                {
                    var mo = _ManagedObjectInfo._ManagedObject.Object;
                    managedType = " (" + (mo != null ? mo.GetType().Name : "associated managed object is null") + ")";
                }
                return "<object: " + Enum.GetName(typeof(JSValueType), _HandleProxy->ValueType) + managedType + ">";
            }
            var val = Value;
            return val != null ? val.ToString() : "null";
        }

        /// <summary>
        /// Checks if the wrapped handle reference is the same as the one compared with. This DOES NOT compare the underlying JavaScript values for equality.
        /// To test for JavaScript value equality, convert to a desired value-type instead by first casting as needed (i.e. (int)jsv1 == (int)jsv2).
        /// </summary>
        public override bool Equals(object obj)
        {
            return (object)this == obj || obj is Handle && _HandleProxy == ((Handle)obj)._HandleProxy;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        // --------------------------------------------------------------------------------------------------------------------

        public static bool operator ==(Handle h1, Handle h2)
        {
            return (object)h1 == (object)h2 || (object)h1 != null && h1.Equals(h2);
        }

        public static bool operator !=(Handle h1, Handle h2)
        {
            return !(h1 == h2);
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    /// <summary>
    /// Keeps track of native V8 handles (C++ native side).
    /// When all managed references are garbage collected, or disposed, then the native side is made weak for disposal.
    /// <para>Note: This only helps developers to strongly type a value they are expecting.  It's possible for an "int" type to wrap a native handle of a 
    /// different type.  Reading the strongly typed value will attempt to convert the value if possible (or generate an error trying).</para>
    /// </summary>
    /// <typeparam name="T">A value type, or just "object" for any type.</typeparam>
    public unsafe class Handle<T> : Handle, IHandle<T>
    {
        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Reading from this property causes a native call to fetch the current V8 value associated with this handle.
        /// </summary>
        new public T Value
        {
            get
            {
                var value = base.Value;
                return (T)value;
            }
        }

        /// <summary>
        /// Once "Value" is accessed to retrieve the JavaScript value in real time, there's no need to keep using it.  Just call this property
        /// instead (a small bit faster).
        /// </summary>
        new public T LastValue
        {
            get
            {
                var value = base.LastValue;
                return (T)value;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        internal Handle(V8Engine engine)
            : base(engine)
        {
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
