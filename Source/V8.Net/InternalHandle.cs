using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;

#if !(V1_1 || V2 || V3 || V3_5)
using System.Dynamic;
#endif

namespace V8.Net
{
    // ========================================================================================================================

    /// <summary>
    /// Keeps track of native V8 handles (C++ native side).
    /// <para>DO NOT STORE THIS HANDLE. Use "Handle" instead (i.e. "Handle h = someInternalHandle;"), or use the value with the "using(someInternalHandle){}" statement.</para>
    /// </summary>
    public unsafe struct InternalHandle :
        IHandle, IHandleBased,
        IV8Object,
        IBasicHandle, // ('IDisposable' will not box in a "using" statement: http://stackoverflow.com/questions/2412981/if-my-struct-implements-idisposable-will-it-be-boxed-when-used-in-a-using-statem)
        IDynamicMetaObjectProvider
    {
        // --------------------------------------------------------------------------------------------------------------------

        public static readonly InternalHandle Empty = new InternalHandle((HandleProxy*)null);

        // --------------------------------------------------------------------------------------------------------------------

        internal HandleProxy* _HandleProxy; // (the native proxy struct wrapped by this instance)
        internal bool _First; // (this is true if this is the FIRST handle to wrap the proxy [first handles may become automatically disposed internally if another handle is not created from it])

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Wraps a given native handle proxy to provide methods to operate on it.
        /// </summary>
        internal InternalHandle(HandleProxy* hp, bool checkIfFirst = true)
        {
            _HandleProxy = null;
            _First = false;
            _Set(hp, checkIfFirst);
        }

        /// <summary>
        /// Sets this instance to the same specified handle value.
        /// </summary>
        public InternalHandle(InternalHandle handle)
        {
            _HandleProxy = null;
            _First = false;
            Set(handle);
        }

        /// <summary>
        /// Creates an internal handle that only wraps the given handle proxy and does not increment the reference counter.
        /// </summary>
        internal static InternalHandle _WrapOnly(HandleProxy* hp)
        {
            var h = InternalHandle.Empty;
            h._HandleProxy = hp;
            return h;
        }

        /// <summary>
        /// Disposes of the current handle proxy reference (if not empty, and different) and replaces it with the specified new reference.
        /// <para>Note: This IS REQUIRED when setting handles, otherwise memory leaks may occur (the native V8 handles will never make it back into the cache).
        /// NEVER use the "=" operator to set a handle.  If using 'InternalHandle' handles, ALWAYS call "Dispose()" when they are no longer needed.
        /// To be safe, use the "using(SomeInternalHandle){}" statement (with 'InternalHandle' handles), or use "Handle refHandle = SomeInternalHandle;", to
        /// to convert it to a handle object that will dispose itself.</para>
        /// </summary>
        public InternalHandle Set(InternalHandle handle)
        {
            if (handle._First)
            {
                var h = _Set(handle._HandleProxy);
                handle.Dispose(); // Disposes the handle if it is the first one (the first one is disposed automatically when passed back into the engine).
                return h;
            }
            else return _Set(handle._HandleProxy);
        }

        /// <summary>
        /// Disposes of the current handle proxy reference (if not empty, and different) and replaces it with the specified new reference.
        /// <para>Note: This IS REQUIRED when setting handles, otherwise memory leaks may occur (the native V8 handles will never make it back into the cache).
        /// NEVER use the "=" operator to set a handle.  If using 'InternalHandle' handles, ALWAYS call "Dispose()" when they are no longer needed.
        /// To be safe, use the "using(SomeInternalHandle){}" statement (with 'InternalHandle' handles), or use "Handle refHandle = SomeInternalHandle;", to
        /// to convert it to a handle object that will dispose itself.</para>
        /// </summary>
        public InternalHandle Set(Handle handle)
        {
            Set(handle._Handle);
            return this;
        }

        internal InternalHandle _Set(HandleProxy* hp, bool checkIfFirst = true)
        {
            if (_HandleProxy != hp)
            {
                if (_HandleProxy != null)
                    Dispose();

                _HandleProxy = hp;

                if (_HandleProxy != null)
                {
                    // ... verify the native handle proxy ID is within a valid range before storing it, and resize as needed ...

                    var engine = V8Engine._Engines[_HandleProxy->EngineID];
                    var handleID = _HandleProxy->ID;

                    if (handleID >= engine._HandleProxies.Length)
                    {
                        HandleProxy*[] handleProxies = new HandleProxy*[(100 + handleID) * 2];
                        for (var i = 0; i < engine._HandleProxies.Length; i++)
                            handleProxies[i] = engine._HandleProxies[i];
                        engine._HandleProxies = handleProxies;
                    }

                    engine._HandleProxies[handleID] = _HandleProxy;

                    if (checkIfFirst)
                        _First = (_HandleProxy->ManagedReferenceCount == 0);

                    _HandleProxy->ManagedReferenceCount++;

                    GC.AddMemoryPressure((Marshal.SizeOf(typeof(HandleProxy))));
                }
            }

            return this;
        }

        /// <summary>
        /// Handles should be set in either two ways: 1. by using the "Set()" method on the left side handle, or 2. using the "Clone()' method on the right side.
        /// Using the "=" operator to set a handle may cause memory leaks if not used correctly.
        /// See also: <seealso cref="Set(InternalHandle)"/>
        /// </summary>
        public InternalHandle Clone()
        {
            return new InternalHandle().Set(this);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Internal handle values are a bit faster than having to create handle objects, but they MUST be explicitly disposed
        /// when no longer needed.  One rule of thumb is to always call "Set()" to set/update an internal value, and call
        /// "Dispose()" either within a class's finalization (destructor), or use it in a "using(){}" statement. Both of these
        /// will dispose of the handle in case exceptions occur. You may also use "try..finally", but that is not preferred
        /// best practice for V8.NET handles.
        /// </summary>
        public void Dispose()
        {
            _Dispose(false);
        }

        internal void _Dispose(bool inFinalizer)
        {
            if (_HandleProxy != null)
            {
                if (_HandleProxy->ManagedReferenceCount > 0)
                {
                    // (if this handle directly references a managed object, then notify the object info that it is weak if the handle ref count is 1)
                    if (_HandleProxy->ManagedReferenceCount == 1 && _HandleProxy->_ObjectID >= 0 && !IsInPendingDisposalQueue)
                    {
                        var weakRef = Engine._GetObjectWeakReference(_HandleProxy->_ObjectID);
                        if (weakRef != null)
                            weakRef.Object._TryDisposeNativeHandle();
                    }
                    else
                    {
                        if (_HandleProxy->ManagedReferenceCount > 0)
                            _HandleProxy->ManagedReferenceCount--;

                        if (_HandleProxy->ManagedReferenceCount == 0)
                        {
                            __TryDispose();
                            _First = false;
                            _HandleProxy = null;
                        }

                    }
                }
                else
                {
                    __TryDispose(); // (no other references, so try to dispose now)
                    _First = false;
                    _HandleProxy = null;
                }
            }
        }

        /// <summary>
        /// Disposes this handle if it is the first one (the first one is disposed automatically when passed back into the engine).
        /// </summary>
        internal void _DisposeIfFirst()
        {
            if (_First) Dispose();
        }

        /// <summary>
        /// Returns true if this handle is disposed (no longer in use).  Disposed native proxy handles are kept in a cache for performance reasons.
        /// </summary>
        public bool IsDisposed { get { return _HandleProxy == null || _HandleProxy->IsDisposed; } }

        // --------------------------------------------------------------------------------------------------------------------

        public static implicit operator Handle(InternalHandle handle)
        {
            return handle._HandleProxy == null ? Handle.Empty : handle.IsObjectType ? new ObjectHandle(handle) : new Handle(handle);
        }

        public static implicit operator ObjectHandle(InternalHandle handle)
        {
            if (!handle.IsEmpty && !handle.IsObjectType) // (note: an empty handle is ok)
                throw new InvalidCastException(string.Format(_VALUE_NOT_AN_OBJECT_ERRORMSG, handle));
            return handle._HandleProxy != null ? new ObjectHandle(handle) : ObjectHandle.Empty;
        }

        public static implicit operator V8NativeObject(InternalHandle handle)
        {
            return handle.Object as V8NativeObject;
        }

        public static implicit operator HandleProxy*(InternalHandle handle)
        {
            return handle._HandleProxy;
        }

        public static implicit operator InternalHandle(HandleProxy* handleProxy)
        {
            return handleProxy != null ? new InternalHandle(handleProxy) : InternalHandle.Empty;
        }

        // --------------------------------------------------------------------------------------------------------------------

        public static bool operator ==(InternalHandle h1, InternalHandle h2)
        {
            return h1._HandleProxy == h2._HandleProxy;
        }

        public static bool operator !=(InternalHandle h1, InternalHandle h2)
        {
            return !(h1 == h2);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public static implicit operator bool(InternalHandle handle)
        {
            return (bool)Types.ChangeType(handle.Value, typeof(bool));
        }

        public static implicit operator Int32(InternalHandle handle)
        {
            return (Int32)Types.ChangeType(handle.Value, typeof(Int32));
        }

        public static implicit operator double(InternalHandle handle)
        {
            return (double)Types.ChangeType(handle.Value, typeof(double));
        }

        public static implicit operator string(InternalHandle handle)
        {
            return handle.ToString();
        }

        public static implicit operator DateTime(InternalHandle handle)
        {
            var ms = (double)Types.ChangeType(handle.Value, typeof(double));
            return new DateTime(1970, 1, 1) + TimeSpan.FromMilliseconds(ms);
        }

        public static implicit operator JSProperty(InternalHandle handle)
        {
            return new JSProperty(handle);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// This is used internally to pass on the internal handle values to internally called methods.
        /// This is required because the last method in a call chain is responsible to dispose first-time handles.
        /// (first-time handles are handles created internally immediately after native proxy calls)
        /// </summary>
        public InternalHandle PassOn()
        {
            InternalHandle h = this;
            _First = false; // ("first" is normally only true if the system created handle is not set to another handle)
            return h;
        }

        // --------------------------------------------------------------------------------------------------------------------
        #region ### SHARED HANDLE CODE START ###

        /// <summary>
        /// The ID (index) of this handle on both the native and managed sides.
        /// </summary>
        public int ID { get { return _HandleProxy != null ? _HandleProxy->ID : -1; } }

        /// <summary>
        /// The JavaScript type this handle represents.
        /// </summary>
        public JSValueType ValueType { get { return _HandleProxy != null ? _HandleProxy->_ValueType : JSValueType.Undefined; } }

        /// <summary>
        /// Used internally to determine the number of references to a handle.
        /// </summary>
        public Int64 ReferenceCount { get { return _HandleProxy != null ? _HandleProxy->ManagedReferenceCount : 0; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A reference to the V8Engine instance that owns this handle.
        /// </summary>
        public V8Engine Engine { get { return _HandleProxy != null ? V8Engine._Engines[_HandleProxy->EngineID] : null; } }

        public Handle AsHandle()
        {
            return (Handle)this;
        }

        public InternalHandle AsInternalHandle
        {
            get { return this; }
        }

        // --------------------------------------------------------------------------------------------------------------------
        // Managed Object Properties and References

        /// <summary>
        /// The ID of the managed object represented by this handle.
        /// This ID is expected when handles are passed to 'V8ManagedObject.GetObject()'.
        /// If this value is less than 0, then there is no associated 'V8NativeObject' object (and the 'Object' property will be null).
        /// </summary>
        public Int32 ObjectID
        {
            get
            {
                return _HandleProxy == null ? -1
                    : _HandleProxy->_ObjectID < -1 || _HandleProxy->_ObjectID >= 0 ? _HandleProxy->_ObjectID
                    : IsObjectType ? V8NetProxy.GetHandleManagedObjectID(_HandleProxy) : -1; // TODO: V8NetProxy.GetHandleManagedObjectID() is not really relevant anymore...but verify first.
            }
            internal set { if (_HandleProxy != null) _HandleProxy->_ObjectID = value; }
        }

        /// <summary>
        /// Returns the managed object ID "as is".
        /// </summary>
        internal Int32 _CurrentObjectID
        {
            get { return _HandleProxy != null ? _HandleProxy->_ObjectID : -1; }
            set { if (_HandleProxy != null) _HandleProxy->_ObjectID = value; }
        }

        /// <summary>
        /// A reference to the managed object associated with this handle. This property is only valid for object handles, and will return null otherwise.
        /// Upon reading this property, if the managed object has been garbage collected (because no more handles or references exist), then a new basic 'V8NativeObject' instance will be created.
        /// <para>Instead of checking for 'null' (which may not work as expected), query 'HasManagedObject' instead.</para>
        /// </summary>
        public V8NativeObject Object
        {
            get
            {
                if (_HandleProxy->_ObjectID >= 0 || _HandleProxy->_ObjectID == -1 && HasObject)
                {
                    var weakRef = Engine._GetObjectWeakReference(_HandleProxy->_ObjectID);
                    return weakRef != null ? weakRef.Reset() : null;
                }
                else
                    return null;
            }
        }

        /// <summary>
        /// If this handle represents an object instance binder, then this returns the bound object.
        /// Bound objects are usually custom user objects (non-V8.NET objects) wrapped in ObjectBinder instances.
        /// </summary>
        public object BoundObject { get { return BindingMode == BindingMode.Instance ? ((ObjectBinder)Object).Object : null; } }

        object IBasicHandle.Object { get { return BoundObject ?? Object; } }

        /// <summary>
        /// Returns the registered type ID for objects that represent registered CLR types.
        /// </summary>
        public Int32 CLRTypeID { get { return _HandleProxy != null ? _HandleProxy->_CLRTypeID : -1; } }

        /// <summary>
        /// If this handle represents a type binder, then this returns the associated 'TypeBinder' instance.
        /// <para>Bound types are usually non-V8.NET types that are wrapped and exposed in the JavaScript environment for use with the 'new' operator.</para>
        /// </summary>
        public TypeBinder TypeBinder
        {
            get
            {
                return BindingMode == BindingMode.Static ? ((TypeBinderFunction)Object).TypeBinder
                    : BindingMode == BindingMode.Instance ? ((ObjectBinder)Object).TypeBinder : null;
            }
        }

        /// <summary>
        /// Returns true if this handle is associated with a managed object.
        /// <para>Note: This can be false even though 'IsObjectType' may be true.
        /// A handle can represent a native V8 object handle without requiring an associated managed object.</para>
        /// </summary>
        public bool HasObject
        {
            get
            {
                if (_HandleProxy->_ObjectID >= -1 && IsObjectType && ObjectID >= 0)
                {
                    var weakRef = Engine._GetObjectWeakReference(_CurrentObjectID);
                    return weakRef != null;
                }
                else return false;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Reading from this property causes a native call to fetch the current V8 value associated with this handle.
        /// <param>For objects, this returns the in-script type text as a string - unless this handle represents an object binder, in which case this will return the bound object instead.</param>
        /// </summary>
        public object Value
        {
            get
            {
                if (IsBinder)
                    return BoundObject;
                else if (CLRTypeID >= 0)
                {
                    var argInfo = new ArgInfo(this);
                    return argInfo.ValueOrDefault; // (this object represents a ArgInfo object, so return its value)
                }

                if (_HandleProxy != null)
                {
                    V8NetProxy.UpdateHandleValue(_HandleProxy);
                    return _HandleProxy->Value;
                }
                else return null;
            }
        }

        /// <summary>
        /// Once "Value" is accessed to retrieve the JavaScript value in real time, there's no need to keep accessing it.  Just call this property
        /// instead (a small bit faster). Note: If the value changes again within the engine (i.e. another scripts executes), you may need to call
        /// 'Value' again to make sure any changes are reflected.
        /// </summary>
        public object LastValue
        {
            get
            {
                return _HandleProxy == null ? null : ((int)_HandleProxy->_ValueType) >= 0 ? _HandleProxy->Value : Value;
            }
        }

        /// <summary>
        /// Returns the array length for handles that represent arrays. For all other types, this returns 0.
        /// Note: To get the items of the array, use 'GetProperty(#)'.
        /// </summary>
        public Int32 ArrayLength { get { return IsArray ? V8NetProxy.GetArrayLength(_HandleProxy) : 0; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns true if this handle is associated with a managed object that has no other references and is ready to be disposed.
        /// </summary>
        public bool IsWeakManagedObject
        {
            get
            {
                var id = _CurrentObjectID;
                if (id >= 0)
                {
                    var owr = Engine._GetObjectWeakReference(_CurrentObjectID);
                    if (owr != null)
                        return owr.IsGCReady;
                    else
                        _CurrentObjectID = id = -1; // (this ID is no longer valid)
                }
                return id == -1;
            }
        }

        /// <summary>
        /// Returns true if the handle is weak and ready to be disposed.
        /// </summary>
        public bool IsWeakHandle { get { return _HandleProxy != null && _HandleProxy->ManagedReferenceCount <= 1; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// True if this handle is place into a queue to be made weak and eventually disposed (cached) on the native side.
        /// </summary>
        public bool IsInPendingDisposalQueue
        {
            get { return _HandleProxy != null && _HandleProxy->IsDisposeReady; }
            internal set { if (_HandleProxy != null) _HandleProxy->IsDisposeReady = value; }
        }

        /// <summary>
        /// True if this handle was made weak on the native side (for object handles only).  Once a handle is weak, the V8 garbage collector can collect the
        /// handle (and any associated managed object) at any time.
        /// </summary>
        public bool IsNativelyWeak
        {
            get { return _HandleProxy != null && _HandleProxy->IsWeak; }
        }

        /// <summary>
        /// Returns true if this handle has no references (usually a primitive type), or is weak AND is associated with a weak managed object reference.
        /// When a handle is ready to be disposed, then calling "Dispose()" will succeed and cause the handle to be placed back into the cache on the native side.
        /// </summary>
        public bool IsDisposeReady { get { return _HandleProxy != null && _HandleProxy->ManagedReferenceCount == 0 || IsWeakHandle && IsWeakManagedObject; } }

        /// <summary>
        /// Attempts to dispose of this handle (add it back into the native proxy cache for reuse).  If the handle represents a managed object with strong
        /// references, then the dispose request is placed into a "pending disposal" queue monitored by a worker thread. When the associated managed object
        /// no longer has any references, this method will be call.
        /// <para>*** NOTE: This is called by Dispose() when the reference count becomes zero and should not be called directly. ***</para>
        /// </summary>
        internal bool __TryDispose()
        {
            if (IsDisposeReady)
            {
                _HandleProxy->IsDisposeReady = true;
                _CompleteDisposal(); // (no need to wait! there's no managed object.)
                return true;
            }
            return false;
        }

        /// <summary>
        /// Completes the disposal of the native handle.
        /// <para>Note: A disposed native handle is simply cached for reuse, and always points back to the same managed handle.</para>
        /// </summary>
        internal void _CompleteDisposal()
        {
            if (!IsDisposed)
            {
                _HandleProxy->ManagedReferenceCount = 0;

                V8NetProxy.DisposeHandleProxy(_HandleProxy);

                _CurrentObjectID = -1;

                _HandleProxy = null;

                GC.RemoveMemoryPressure((Marshal.SizeOf(typeof(HandleProxy))));
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Forces the underlying object, if any, to separate from the handle.  This is done by swapping the object with a
        /// place holder object to keep the ID (index) for the current object alive until the native V8 engine's GC can remove
        /// any associated handles later.  The released object is returned, or null if there is no object.
        /// </summary>
        /// <returns>The object released.</returns>
        public V8NativeObject ReleaseManagedObject()
        {
            if (IsObjectType && ObjectID >= 0)
                using (Engine._ObjectsLocker.ReadLock())
                {
                    var weakRef = Engine._GetObjectWeakReference(ObjectID);
                    if (weakRef != null)
                    {
                        var obj = weakRef.Object;
                        var placeHolder = new V8NativeObject();
                        placeHolder._Engine = obj._Engine;
                        placeHolder.Template = obj.Template;
                        weakRef.SetTarget(placeHolder); // (this must be done first before moving the handle to the new object!)
                        placeHolder.Handle = obj.Handle;
                        obj.Template = null;
                        obj._ID = null;
                        obj._Handle.Set(InternalHandle.Empty);
                        return obj;
                    }
                }
            return null;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns true if this handle is empty (that is, equal to 'Handle.Empty'), and false if a valid handle exists.
        /// <para>An empty state is when a handle is set to 'InternalHandle.Empty' and has no valid native V8 handle assigned.
        /// This is similar to "undefined"; however, this property will be true if a valid native V8 handle exists that is set to "undefined".</para>
        /// </summary>
        public bool IsEmpty { get { return _HandleProxy == null; } }

        /// <summary>
        /// Returns true if this handle is undefined or empty (empty is when this handle is an instance of 'Handle.Empty').
        /// <para>"Undefined" does not mean "null".  A variable (handle) can be defined and set to "null".</para>
        /// </summary>
        public bool IsUndefined { get { return IsEmpty || ValueType == JSValueType.Undefined; } }

        /// <summary>
        /// Returns 'true' if this handle represents a ''ull' value (that is, an explicitly defined 'null' value).
        /// This will return 'false' if 'IsEmpty' or 'IsUndefined' is true.
        /// </summary>
        public bool IsNull { get { return ValueType == JSValueType.Null; } }

        public bool IsBoolean { get { return ValueType == JSValueType.Bool; } }
        public bool IsBooleanObject { get { return ValueType == JSValueType.BoolObject; } }
        public bool IsInt32 { get { return ValueType == JSValueType.Int32; } }
        public bool IsNumber { get { return ValueType == JSValueType.Number; } }
        public bool IsNumberObject { get { return ValueType == JSValueType.NumberObject; } }
        public bool IsString { get { return ValueType == JSValueType.String; } }
        public bool IsStringObject { get { return ValueType == JSValueType.StringObject; } }
        public bool IsObject { get { return ValueType == JSValueType.Object; } }
        public bool IsFunction { get { return ValueType == JSValueType.Function; } }
        public bool IsDate { get { return ValueType == JSValueType.Date; } }
        public bool IsArray { get { return ValueType == JSValueType.Array; } }
        public bool IsRegExp { get { return ValueType == JSValueType.RegExp; } }

        /// <summary>
        /// Returns true of the handle represents ANY object type.
        /// </summary>
        public bool IsObjectType
        {
            get
            {
                return ValueType == JSValueType.BoolObject
                    || ValueType == JSValueType.NumberObject
                    || ValueType == JSValueType.StringObject
                    || ValueType == JSValueType.Function
                    || ValueType == JSValueType.Date
                    || ValueType == JSValueType.RegExp
                    || ValueType == JSValueType.Array
                    || ValueType == JSValueType.Object;
            }
        }

        /// <summary>
        /// Used internally to quickly determine when an instance represents a binder object type (faster than reflection!).
        /// </summary>
        public bool IsBinder { get { return IsObjectType && HasObject && Object._BindingMode != BindingMode.None; } }

        /// <summary>
        /// Returns the binding mode (Instance, Static, or None) represented by this handle.  The return is 'None' (0) if not applicable.
        /// </summary>
        public BindingMode BindingMode { get { return IsBinder ? Object._BindingMode : BindingMode.None; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the 'Value' property type cast to the expected type.
        /// Warning: No conversion is made between different value types.
        /// </summary>
        public DerivedType As<DerivedType>()
        {
            return _HandleProxy != null ? (DerivedType)Value : default(DerivedType);
        }

        /// Returns the 'LastValue' property type cast to the expected type.
        /// Warning: No conversion is made between different value types.
        public DerivedType LastAs<DerivedType>()
        {
            return _HandleProxy != null ? (DerivedType)LastValue : default(DerivedType);
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
            try
            {
                if (IsEmpty) return "empty";
                if (IsUndefined) return "undefined";

                if (IsBinder)
                {
                    if (BindingMode == BindingMode.Static)
                    {
                        var typeBinder = ((TypeBinderFunction)Object).TypeBinder;
                        return "(CLR Type: " + typeBinder.BoundType.FullName + ")";
                    }
                    else
                    {
                        var obj = BoundObject;
                        if (obj != null)
                            return "(" + obj.ToString() + ")";
                        else
                            throw new InvalidOperationException("Object binder does not have an object instance.");
                    }
                }
                else if (IsObjectType)
                {
                    string managedType = "";
                    string disposal = "";

                    switch (_HandleProxy->Disposed)
                    {
                        case 0: break;
                        case 1: disposal = " - Dispose Ready"; break;
                        case 2: disposal = " - Weak (waiting on V8 GC)"; break;
                        case 3: disposal = " - Disposed"; break;
                    }

                    if (HasObject)
                    {
                        var mo = Engine._GetObjectAsIs(ObjectID);
                        managedType = " (" + (mo != null ? mo.GetType().Name + " [" + mo.ID + "]" : "associated managed object is null") + ")";
                    }

                    return "<object: " + Enum.GetName(typeof(JSValueType), ValueType) + managedType + disposal + ">";
                }

                var val = Value;

                return val != null ? (string)Types.ChangeType(val, typeof(string)) : "null";
            }
            catch (Exception ex)
            {
                return Exceptions.GetFullErrorMessage(ex);
            }
        }

        /// <summary>
        /// Checks if the wrapped handle reference is the same as the one compared with. This DOES NOT compare the underlying JavaScript values for equality.
        /// To test for JavaScript value equality, convert to a desired value-type instead by first casting as needed (i.e. (int)jsv1 == (int)jsv2).
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is IHandleBased && _HandleProxy == ((IHandleBased)obj).AsInternalHandle._HandleProxy;
        }

        public override int GetHashCode()
        {
            return (int)ID;
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
                return ValueType == JSValueType.InternalError
                    || ValueType == JSValueType.CompilerError
                    || ValueType == JSValueType.ExecutionError;
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
                switch (ValueType)
                {
                    case JSValueType.InternalError: throw new V8InternalErrorException(this);
                    case JSValueType.CompilerError: throw new V8CompilerErrorException(this);
                    case JSValueType.ExecutionError: throw new V8ExecutionErrorException(this);
                    default: throw new V8Exception(this); // (this will only happen if 'IsError' contains a type check that doesn't have any corresponding exception object)
                }
        }

        // --------------------------------------------------------------------------------------------------------------------
        // DynamicObject support is in .NET 4.0 and higher

#if !(V1_1 || V2 || V3 || V3_5)
        #region IDynamicMetaObjectProvider Members

        public DynamicMetaObject GetMetaObject(Expression parameter)
        {
            return new DynamicHandle(this, parameter);
        }

        #endregion
#endif

        // --------------------------------------------------------------------------------------------------------------------
        // IConvertable

        public TypeCode GetTypeCode()
        {
            switch (ValueType)
            {
                case JSValueType.Array:
                case JSValueType.Date:
                case JSValueType.Function:
                case JSValueType.Object:
                case JSValueType.RegExp:
                    return TypeCode.Object;
                case JSValueType.Bool:
                case JSValueType.BoolObject:
                    return TypeCode.Boolean;
                case JSValueType.Int32:
                    return TypeCode.Int32;
                case JSValueType.Number:
                case JSValueType.NumberObject:
                    return TypeCode.Double;
                case JSValueType.String:
                case JSValueType.CompilerError:
                case JSValueType.ExecutionError:
                case JSValueType.InternalError:
                    return TypeCode.String;
            }
            return TypeCode.Empty;
        }

        public bool ToBoolean(IFormatProvider provider) { return Types.ChangeType<bool>(Value, provider); }
        public byte ToByte(IFormatProvider provider) { return Types.ChangeType<byte>(Value, provider); }
        public char ToChar(IFormatProvider provider) { return Types.ChangeType<char>(Value, provider); }
        public DateTime ToDateTime(IFormatProvider provider) { return Types.ChangeType<DateTime>(Value, provider); }
        public decimal ToDecimal(IFormatProvider provider) { return Types.ChangeType<decimal>(Value, provider); }
        public double ToDouble(IFormatProvider provider) { return Types.ChangeType<double>(Value, provider); }
        public short ToInt16(IFormatProvider provider) { return Types.ChangeType<Int16>(Value, provider); }
        public int ToInt32(IFormatProvider provider) { return Types.ChangeType<Int32>(Value, provider); }
        public long ToInt64(IFormatProvider provider) { return Types.ChangeType<Int64>(Value, provider); }
        public sbyte ToSByte(IFormatProvider provider) { return Types.ChangeType<sbyte>(Value, provider); }
        public float ToSingle(IFormatProvider provider) { return Types.ChangeType<Single>(Value, provider); }
        public string ToString(IFormatProvider provider) { return Types.ChangeType<string>(Value, provider); }
        public object ToType(Type conversionType, IFormatProvider provider) { return Types.ChangeType(Value, conversionType, provider); }
        public ushort ToUInt16(IFormatProvider provider) { return Types.ChangeType<UInt16>(Value, provider); }
        public uint ToUInt32(IFormatProvider provider) { return Types.ChangeType<UInt32>(Value, provider); }
        public ulong ToUInt64(IFormatProvider provider) { return Types.ChangeType<UInt64>(Value, provider); }

        #endregion ### SHARED HANDLE CODE END ###
        // --------------------------------------------------------------------------------------------------------------------

        internal const string _NOT_AN_OBJECT_ERRORMSG = "The handle does not represent a JavaScript object.";
        internal const string _VALUE_NOT_AN_OBJECT_ERRORMSG = "The handle {0} does not represent a JavaScript object.";

        /// <summary>
        /// Calls the V8 'Set()' function on the underlying native object.
        /// Returns true if successful.
        /// </summary>
        /// <param name="attributes">Flags that describe the property behavior.  They must be 'OR'd together as needed.</param>
        public bool SetProperty(string name, InternalHandle value, V8PropertyAttributes attributes = V8PropertyAttributes.None)
        {
            try
            {
                if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

                if (!IsObjectType)
                    throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

                return V8NetProxy.SetObjectPropertyByName(this, name, value, attributes);
            }
            finally
            {
                value._DisposeIfFirst();
            }
        }

        /// <summary>
        /// Calls the V8 'Set()' function on the underlying native object.
        /// Returns true if successful.
        /// </summary>
        public bool SetProperty(Int32 index, InternalHandle value)
        {
            try
            {
                if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

                return V8NetProxy.SetObjectPropertyByIndex(this, index, value);
            }
            finally
            {
                value._DisposeIfFirst();
            }
        }

        /// <summary>
        /// Sets a property to a given object. If the object is not V8.NET related, then the system will attempt to bind the instance and all public members to
        /// the specified property name.
        /// Returns true if successful.
        /// </summary>
        /// <param name="name">The property name. If 'null', then the name of the object type is assumed.</param>
        /// <param name="obj">Some value or object instance. 'Engine.CreateValue()' will be used to convert value types, unless the object is already a handle, in which case it is set directly.</param>
        /// <param name="className">A custom in-script function name for the specified object type, or 'null' to use either the type name as is (the default) or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">For object instances, if true, then object reference members are included, otherwise only the object itself is bound and returned.
        /// For security reasons, public members that point to object instances will be ignored. This must be true to included those as well, effectively allowing
        /// in-script traversal of the object reference tree (so make sure this doesn't expose sensitive methods/properties/fields).</param>
        /// <param name="memberSecurity">For object instances, these are default flags that describe JavaScript properties for all object instance members that
        /// don't have any 'ScriptMember' attribute.  The flags should be 'OR'd together as needed.</param>
        public bool SetProperty(string name, object obj, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            if (name.IsNullOrWhiteSpace())
                if (obj == null) throw new InvalidOperationException("You cannot pass 'null' without a valid property name.");
                else
                    name = obj.GetType().Name;

            if (obj is IHandleBased)
                return SetProperty(name, ((IHandleBased)obj).AsInternalHandle, (V8PropertyAttributes)(memberSecurity ?? ScriptMemberSecurity.ReadWrite));

            if (obj == null || obj is string || obj.GetType().IsValueType) // TODO: Check enum support.
                return SetProperty(name, Engine.CreateValue(obj), (V8PropertyAttributes)(memberSecurity ?? ScriptMemberSecurity.ReadWrite));

            var nObj = Engine.CreateBinding(obj, className, recursive, memberSecurity);

            if (memberSecurity != null)
                return SetProperty(name, nObj, (V8PropertyAttributes)memberSecurity);
            else
                return SetProperty(name, nObj);
        }

        /// <summary>
        /// Binds a 'V8Function' object to the specified type and associates the type name (or custom script name) with the underlying object.
        /// Returns true if successful.
        /// </summary>
        /// <param name="type">The type to wrap.</param>
        /// <param name="propertyAttributes">Flags that describe the property behavior.  They must be 'OR'd together as needed.</param>
        /// <param name="className">A custom in-script function name for the specified type, or 'null' to use either the type name as is (the default) or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">For object types, if true, then object reference members are included, otherwise only the object itself is bound and returned.
        /// For security reasons, public members that point to object instances will be ignored. This must be true to included those as well, effectively allowing
        /// in-script traversal of the object reference tree (so make sure this doesn't expose sensitive methods/properties/fields).</param>
        /// <param name="memberSecurity">For object instances, these are default flags that describe JavaScript properties for all object instance members that
        /// don't have any 'ScriptMember' attribute.  The flags should be 'OR'd together as needed.</param>
        public bool SetProperty(Type type, V8PropertyAttributes propertyAttributes = V8PropertyAttributes.None, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            var func = (V8Function)Engine.CreateBinding(type, className, recursive, memberSecurity).Object;

            return SetProperty(func.FunctionTemplate.ClassName, func, propertyAttributes);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the V8 'Get()' function on the underlying native object.
        /// If the property doesn't exist, the 'IsUndefined' property will be true.
        /// </summary>
        public InternalHandle GetProperty(string name)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.GetObjectPropertyByName(this, name);
        }

        /// <summary>
        /// Calls the V8 'Get()' function on the underlying native object.
        /// If the property doesn't exist, the 'IsUndefined' property will be true.
        /// </summary>
        public InternalHandle GetProperty(Int32 index)
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.GetObjectPropertyByIndex(this, index);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the V8 'Delete()' function on the underlying native object.
        /// Returns true if the property was deleted.
        /// </summary>
        public bool DeleteProperty(string name)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.DeleteObjectPropertyByName(this, name);
        }

        /// <summary>
        /// Calls the V8 'Delete()' function on the underlying native object.
        /// Returns true if the property was deleted.
        /// </summary>
        public bool DeleteProperty(Int32 index)
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.DeleteObjectPropertyByIndex(this, index);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the V8 'SetAccessor()' function on the underlying native object to create a property that is controlled by "getter" and "setter" callbacks.
        /// </summary>
        public void SetAccessor(string name,
            V8NativeObjectPropertyGetter getter, V8NativeObjectPropertySetter setter,
            V8PropertyAttributes attributes = V8PropertyAttributes.None, V8AccessControl access = V8AccessControl.Default)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            var engine = Engine;
            // TODO: Need a different native ID to track this.
            V8NetProxy.SetObjectAccessor(this, ObjectID, name,
                   Engine._StoreAccessor<ManagedAccessorGetter>(ObjectID, "get_" + name, (HandleProxy* _this, string propertyName) =>
                   {
                       try
                       {
                           using (InternalHandle hThis = _this) { return getter != null ? getter(hThis, propertyName) : null; }
                       }
                       catch (Exception ex)
                       {
                           return engine.CreateError(Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
                       }
                   }),
                   Engine._StoreAccessor<ManagedAccessorSetter>(ObjectID, "set_" + name, (HandleProxy* _this, string propertyName, HandleProxy* value) =>
                   {
                       try
                       {
                           using (InternalHandle hThis = _this) { return setter != null ? setter(hThis, propertyName, value) : null; }
                       }
                       catch (Exception ex)
                       {
                           return engine.CreateError(Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
                       }
                   }),
                   access, attributes);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns a list of all property names for this object (including all objects in the prototype chain).
        /// </summary>
        public string[] GetPropertyNames()
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            using (InternalHandle v8array = V8NetProxy.GetPropertyNames(this))
            {
                var length = V8NetProxy.GetArrayLength(v8array);

                var names = new string[length];

                InternalHandle itemHandle;

                for (var i = 0; i < length; i++)
                    using (itemHandle = V8NetProxy.GetObjectPropertyByIndex(v8array, i))
                    {
                        names[i] = itemHandle;
                    }

                return names;
            }
        }

        /// <summary>
        /// Returns a list of all property names for this object (excluding the prototype chain).
        /// </summary>
        public string[] GetOwnPropertyNames()
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            using (InternalHandle v8array = V8NetProxy.GetOwnPropertyNames(this))
            {
                var length = V8NetProxy.GetArrayLength(v8array);

                var names = new string[length];

                InternalHandle itemHandle;

                for (var i = 0; i < length; i++)
                    using (itemHandle = V8NetProxy.GetObjectPropertyByIndex(v8array, i))
                    {
                        names[i] = itemHandle;
                    }

                return names;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Get the attribute flags for a property of this object.
        /// If a property doesn't exist, then 'V8PropertyAttributes.None' is returned
        /// (Note: only V8 returns 'None'. The value 'Undefined' has an internal proxy meaning for property interception).</para>
        /// </summary>
        public V8PropertyAttributes GetPropertyAttributes(string name)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.GetPropertyAttributes(this, name);
        }

        // --------------------------------------------------------------------------------------------------------------------

        internal InternalHandle _Call(string functionName, InternalHandle _this, params InternalHandle[] args)
        {
            try
            {
                if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

                HandleProxy** nativeArrayMem = Utilities.MakeHandleProxyArray(args);

                var result = V8NetProxy.Call(this, functionName, _this, args.Length, nativeArrayMem);

                Utilities.FreeNativeMemory((IntPtr)nativeArrayMem);

                return result;
            }
            finally
            {
                _this._DisposeIfFirst();
                for (var i = 0; i < args.Length; i++)
                    args[i]._DisposeIfFirst();
            }
        }

        /// <summary>
        /// Calls the specified function property on the underlying object.
        /// The '_this' parameter is the "this" reference within the function when called.
        /// </summary>
        public InternalHandle Call(string functionName, InternalHandle _this, params InternalHandle[] args)
        {
            if (functionName.IsNullOrWhiteSpace()) throw new ArgumentNullException("functionName (cannot be null, empty, or only whitespace)");

            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return _Call(functionName, _this, args);
        }

        /// <summary>
        /// Calls the specified function property on the underlying object.
        /// The 'this' property will not be specified, which will default to the global scope as expected.
        /// </summary>
        public InternalHandle Call(string functionName, params InternalHandle[] args)
        {
            if (functionName.IsNullOrWhiteSpace()) throw new ArgumentNullException("functionName (cannot be null, empty, or only whitespace)");

            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return _Call(functionName, InternalHandle.Empty, args);
        }

        /// <summary>
        /// Calls the underlying object as a function.
        /// The '_this' parameter is the "this" reference within the function when called.
        /// </summary>
        public InternalHandle Call(InternalHandle _this, params InternalHandle[] args)
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return _Call(null, _this, args);
        }

        /// <summary>
        /// Calls the underlying object as a function.
        /// The 'this' property will not be specified, which will default to the global scope as expected.
        /// </summary>
        public InternalHandle Call(params InternalHandle[] args)
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return _Call(null, InternalHandle.Empty, args);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The prototype of the object (every JavaScript object implicitly has a prototype).
        /// </summary>
        public InternalHandle Prototype
        {
            get
            {
                if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);
                return V8NetProxy.GetObjectPrototype(_HandleProxy);
            }
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    /// <summary>
    /// Intercepts JavaScript access for properties on the associated JavaScript object for retrieving a value.
    /// <para>To allow the V8 engine to perform the default get action, return "Handle.Empty".</para>
    /// </summary>
    public delegate InternalHandle V8NativeObjectPropertyGetter(InternalHandle _this, string propertyName);

    /// <summary>
    /// Intercepts JavaScript access for properties on the associated JavaScript object for setting values.
    /// <para>To allow the V8 engine to perform the default set action, return "Handle.Empty".</para>
    /// </summary>
    public delegate InternalHandle V8NativeObjectPropertySetter(InternalHandle _this, string propertyName, InternalHandle value);

    // ========================================================================================================================

    public unsafe partial class V8Engine
    {
        internal readonly Dictionary<Int32, Dictionary<string, Delegate>> _Accessors = new Dictionary<Int32, Dictionary<string, Delegate>>();

        /// <summary>
        /// This is required in order prevent accessor delegates from getting garbage collected when used with P/Invoke related callbacks (a process called "thunking").
        /// </summary>
        /// <typeparam name="T">The type of delegate ('d') to store and return.</typeparam>
        /// <param name="key">A native pointer (usually a proxy object) to associated the delegate to.</param>
        /// <param name="d">The delegate to keep a strong reference to (expected to be of type 'T').</param>
        /// <returns>The same delegate passed in, cast to type of 'T'.</returns>
        internal T _StoreAccessor<T>(Int32 id, string propertyName, T d) where T : class
        {
            Dictionary<string, Delegate> delegates;
            if (!_Accessors.TryGetValue(id, out delegates))
                _Accessors[id] = delegates = new Dictionary<string, Delegate>();
            delegates[propertyName] = (Delegate)(object)d;
            return d;
        }

        /// <summary>
        /// Returns true if there are any delegates associated with the given object reference.
        /// </summary>
        internal bool _HasAccessors(Int32 id)
        {
            Dictionary<string, Delegate> delegates;
            return _Accessors.TryGetValue(id, out delegates) && delegates.Count > 0;
        }

        /// <summary>
        /// Clears any accessor delegates associated with the given object reference.
        /// </summary>
        internal void _ClearAccessors(Int32 id)
        {
            Dictionary<string, Delegate> delegates;
            if (_Accessors.TryGetValue(id, out delegates))
                delegates.Clear();
        }
    }

    // ========================================================================================================================
}

