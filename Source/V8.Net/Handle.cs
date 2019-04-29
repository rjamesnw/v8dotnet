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

    public partial class V8Engine
    {
#if DEBUG && TRACKHANDLES
        /// <summary>
        /// Holds all the InternalHandle values that were set with native proxy handles.
        /// <para>Note: This list is only available when compiling with the DEBUG and TRACKHANDLES compiler directives.</para>
        /// </summary>
        public static readonly List<InternalHandle> AllInternalHandlesEverCreated = new List<InternalHandle>();
#endif
    }

    /// <summary>
    /// Wrapper to an InternalHandle value for GC tracking purposes.  Calls work internally using 'InternalHandle' for
    /// efficiency; however, an object handle is needed outside of V8.Net for end users. To prevent creating many objects
    /// for the same native V8 handle, 'Handle' objects are stored in a fixed size array for quick lookup, and are shared
    /// across many InternalHandle values. When all InternalHandle values are gone, this handle gets collected, unless
    /// also referenced.
    /// </summary>
    public unsafe class Handle : IV8Disposable, IHandleBased, INativeHandleBased, IDynamicMetaObjectProvider
    {
        public static readonly Handle Empty = new Handle(null);

        internal InternalHandle _Handle;

        /// <summary>
        /// Just another shorter way to return the 'InternalHandle' value.
        /// <para>Note: You can also prefix a tilde ('~') character to a 'Handle' reference to de-reference it to the underlying
        /// InternHandle value as well (i.e. '(~handle).SetProperty(...)', or 'var h = ~handle; h.Call(...)').</para>
        /// </summary>
        public InternalHandle _ { get { return _Handle; } }

        protected Handle() { }
        internal Handle(InternalHandle h)
        {
            if (h._Object != null)
                throw new InvalidOperationException("'h._Object' must be null - not allowed to overwrite existing object references on handles.");
            _Handle = h;
            _Handle._Object = this;
        }

        /// <summary>
        ///     This is called on the GC finalizer thread to flag that this managed object entry can be collected. This is done by
        ///     marking the native side handle as weak. If a script is running during this call then the native side will detect it 
        ///     and queue the request for later.
        ///     <para>Note: There are no longer any managed references to the object at this point; HOWEVER, there may still be
        ///     NATIVE ones. This means the native object may survive this process.</para>
        /// </summary>
        ~Handle()
        {
            _Handle._Finalize(true);
            //?this.Finalizing();
        }

        /// <summary>
        /// Returns true if this handle contains an error message (the string value is the message).
        /// If you have exception catching in place, you can simply call 'ThrowOnError()' instead.
        /// </summary>
        public bool IsError { get { return _Handle.IsError; } }

        /// <summary>
        /// Checks if the handle represents an error, and if so, throws one of the corresponding derived V8Exception exceptions.
        /// See 'JSValueType' for possible exception states.  You can check the 'IsError' property to see if this handle represents an error.
        /// <para>Exceptions thrown: V8InternalErrorException, V8CompilerErrorException, V8ExecutionErrorException, and V8Exception (for any general V8-related exceptions).</para>
        /// </summary>
        public void ThrowOnError() { _Handle.ThrowOnError(); }

        /// <summary>
        /// Returns a reference to the engine this tracker handle belongs to.
        /// </summary>
        public V8Engine Engine
        {
            get { return _Handle.Engine; }
        }

        /// <summary>
        ///     Returns true if calling 'Dispose()' will release the native side handle immediately on the native side. All
        ///     InternalHandle values not kept alive can be disposed quickly.  The only handle left on a managed object cannot be
        ///     disposed, nor the global object handle, as these will be locked. If false is returned, then there is a managed
        ///     object associated.
        ///     <para>This may be overridden in derived types, such as <see cref="V8NativeObject"/>.</para>
        /// </summary>
        /// <value> A true or false value. </value>
        /// <seealso cref="P:V8.Net.IV8Disposable.CanDispose"/>
        public virtual bool CanDispose
        {
            get { return _Handle.CanDispose; }
        }

        /// <summary>
        ///     Disposes of the underlying native handle. If the handle cannot be disposed (perhaps because it is locked), an
        ///     <see cref="InvalidOperationException"/> error will occur. Optionally you may find 'TryDispose()' more convenient to ignore such
        ///     situations.
        ///     <para>
        ///     If the handle represents a managed V8NativeObject instance, the handle cannot be disposed externally. Managed
        ///     objects will begin a disposal process when there are no more managed references. When this occurs, the native side
        ///     V8 handle is made "weak".  When there are no more references in V8, the V8's GC calls back into the managed side to
        ///     notify that disposal can complete. In all other cases, disposing a handle will succeed.
        ///     </para>
        /// </summary>
        public virtual void Dispose()
        {
            _Handle.Dispose();
        }

        /// <summary>
        /// Same as 'Dispose()', except that any errors are suppressed (i.e. if the handle is locked). 
        /// If successful (which usually just means this handle was cleared), then 'true' is returned.
        /// </summary>
        public bool TryDispose()
        {
            return _Handle.TryDispose();
        }

        /// <summary>
        /// Returns a the underlying InternalHandle value.
        /// </summary>
        public virtual InternalHandle InternalHandle
        {
            get { return _Handle; }
            set { throw new NotSupportedException("Not allowed to change the underlying internal handle directly on a 'Handle' object. Derived types can override this to provide a setter where supported."); }
        }

        public V8NativeObject Object
        {
            get { return _Handle.Object; }
        }

        public static implicit operator InternalHandle(Handle h) { return h._Handle; }
        public static implicit operator Handle(HandleProxy* hp) { return ((InternalHandle)hp).GetTrackableHandle(); }
        public static InternalHandle operator ~(Handle h) { return h._Handle; }

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

        public override string ToString()
        {
            return _Handle.ToString();
        }

        public override int GetHashCode()
        {
            return _Handle.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return _Handle.Equals(obj);
        }

        // --------------------------------------------------------------------------------------------------------------------

        HandleProxy* INativeHandleBased.GetNativeHandleProxy()
        {
            return (HandleProxy*)_Handle;
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    /// <summary>
    /// Keeps track of native V8 handles (C++ native side).
    /// <para>DO NOT STORE THIS HANDLE. Use "Handle" instead (i.e. "Handle h = someInternalHandle;"), or use the value with the "using(someInternalHandle){}" statement.</para>
    /// </summary>
    public unsafe struct InternalHandle :
        IHandle, IHandleBased, INativeHandleBased,
        IV8Object,
        IBasicHandle, // ('IDisposable' will not box in a "using" statement: http://stackoverflow.com/questions/2412981/if-my-struct-implements-idisposable-will-it-be-boxed-when-used-in-a-using-statem)
        IDynamicMetaObjectProvider
    {
        // --------------------------------------------------------------------------------------------------------------------

        public static readonly InternalHandle Empty = new InternalHandle((HandleProxy*)null);

        /// <summary>
        ///     Returns a handle that initially does not have any trackers associated, and only references the underlying native
        ///     proxy handle object.  This can be useful for "watching" the proxy handle. For example, in testing garbage collection,
        ///     one can test IsDisposed, IsDisposing, or IsNativelyWeak without blocking the GC collection due to a reference of any
        ///     associated managed object in the handle.
        ///     <para>Warning: Tracker handles are shared via index IDs on a global array in the engine.  As such, accessing certain
        ///     properties and methods on the untracked handle can trigger a pull to update the untracked handle with the tracker.
        ///     Normally this is not a problem, unless one is testing the GC, as it may prevent the GC from collecting the object
        ///     during the test.</para>
        /// </summary>
        public static InternalHandle GetUntrackedHandleFromInternal(InternalHandle source) { var h = Empty; h._HandleProxy = source; return h; }
        public static InternalHandle GetUntrackedHandleFromProxy(HandleProxy* source) { var h = Empty; h._HandleProxy = source; return h; }
        public static InternalHandle GetUntrackedHandleFromObject(V8NativeObject source) { var h = Empty; h._HandleProxy = source._Handle; return h; }

        // --------------------------------------------------------------------------------------------------------------------

        internal HandleProxy* _HandleProxy; // (the native proxy struct wrapped by this instance)

        /// <summary>
        /// The managed object represented by this handle, if any, or null otherwise.
        /// If this handle does not represent a managed object, then this may be set to a 'Handle' instead to allow tracking 
        /// and disposing the internal handle value within external user code.
        /// </summary>
        internal Handle _Object; // (or "Handle" tracker)

        /// <summary>
        ///     InternalHandle values are disposed within the engine system automatically.  If a handle is to be used outside the
        ///     engine, this should be called to allow the handle to be tracked by the managed GC.
        ///     <para>Note: This should usually only need to be called on handles returned from methods on the engine.  This is not
        ///     called by default on handles passed into callbacks, as those handles are disposed automatically upon return from the
        ///     callback.  When a callback completes, the native side calls '{HandleProxy}.TryDispose()' to try to dispose handles
        ///     (such as those for arguments) automatically. This method must be called to prevent those handles from being
        ///     disposed. If not called, and you store the handle value, you'll get an error if you try to use it later.</para>
        /// </summary>
        /// <returns> The current handle. </returns>
        public InternalHandle KeepTrack()
        {
            GetTrackableHandle(); // (never return this value - the object responsible for this handle may itself have a null handle currently)
            return this;
        }

        /// <summary>
        ///     When objects are created a weak reference is kept in order to translate object IDs into references. This means
        ///     however that objects are not "rooted" by default, and unless you have your own reference, the object will eventually
        ///     get reclaimed by the garbage collector (GC).  The side-effect is that objects on the native side no longer show any
        ///     connection to the managed side.  To prevent this, either keep your own reference, or call this method. This is
        ///     called on <see cref="ObjectBinder"/> objects automatically, since they are usually created within script.
        ///     <para>Note: The native side handle will be made weak.  As soon as the native side tries to garbage collect the
        ///     object, the internal rooted reference will be removed.</para>
        ///     If the associated object is no longer needed, you can call <see cref="Abandon"/> so the native V8 GC will dispose of
        ///     it on it's own.
        /// </summary>
        /// <returns> The current handle. </returns>
        /// <seealso cref="Abandon"/>
        public InternalHandle KeepAlive()
        {
            if (!IsObject) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);
            if (_HandleProxy->_ObjectID >= 0)
            {
                if (_Object == null) _Object = Object;
                if (Engine._MakeObjectRooted(_HandleProxy->_ObjectID)) // (returns false if already rooted)
                    V8NetProxy.MakeWeakHandle(this);
            }
            else throw new InvalidOperationException($"This handle only tracks a native side object. {nameof(KeepAlive)}() only keeps alive managed-side objects from being garbage collected."
                + " If you are creating accessors on a native-only object, then create the accessors first, then call this method.");
            return this;
        }

        /// <summary>
        ///     If the object is no longer needed, you can call this so the native V8 GC can dispose of the managed object on it's
        ///     own (via a native GC callback that V8.Net hooks into). This is normally done if <see cref="KeepAlive"/> was
        ///     previously called, which permanently roots managed objects to prevent the managed GC from claiming them.
        /// </summary>
        /// <returns> An InternalHandle. </returns>
        /// <seealso cref="KeepAlive"/>
        public InternalHandle Abandon()
        {
            if (_HandleProxy->_ObjectID >= 0)
            {
                if (_Object == null) _Object = Object;
                Engine._UnrootObject(_HandleProxy->_ObjectID); // (returns false if already rooted)
            }
            return this;
        }

        ///// <summary>
        ///// Cloning is no longer necessary.  Please use '{source}.KeepAlive()' or '{target}.Set({source})' instead.
        ///// </summary>
        ///// <returns></returns>
        //[Obsolete("Cloning is no longer necessary.  Please use '{source}.KeepAlive()' or '{target}.Set({source})' instead.", true)]
        //?public InternalHandle Clone() { throw new NotSupportedException("Cloning is no longer necessary.  Please use '{source}.KeepAlive()' or '{target}.Set({source})' instead."); }

        /// <summary>
        /// Returns a 'Handle' instance that can be used to track this value-based handle.  When no more value-based handles
        /// exist, including any references to the tracker itself, the tracker will initiate disposal of the native proxy
        /// handle. Calling 'KeepAlive()' implicitly calls this method as well.
        /// </summary>
        /// <param name="createIfMissing">If true (default), then a tracker handle based object is created if one doesn't exist
        /// yet.  If false, then a tracker is only returned if one already exists, otherwise null is returned.
        /// <para>Tracker handles are objects, instead of values, and are used to dispose of value base handles via the garbage
        /// collector. This method is called implicitly when converting from an InternalHandle value to a Handle type.</para>
        /// </param>
        public Handle GetTrackableHandle(bool createIfMissing = true)
        {
            if (_Object == null && !IsEmpty && !IsDisposed)
            {
                _Object = Object; // (always check first if there is an associated managed object that should be used)

                if (_Object == null)
                {
                    // ... no object to use, so create a tracker handle ...
                    var engine = Engine;
                    if (engine != null)
                    {
                        var handleID = _HandleProxy->ID;
                        WeakReference wref = handleID >= 0 && handleID < engine._TrackerHandles.Length ? engine._TrackerHandles[handleID] : null; // (first check if one already exists and return that)
                        Handle h = wref != null ? (Handle)wref.Target : null;
                        if (h != null)
                            _Object = h;
                        else
                            if (createIfMissing)
                        {
                            h = new Handle(this); // (need to create a new tracker handle)                   
                            if (handleID >= engine._TrackerHandles.Length)
                                Array.Resize(ref engine._TrackerHandles, (100 + handleID) * 2); // (make sure the tracker handle quick reference array can contain the handle ID)
                            if (wref != null)
                                wref.Target = h;
                            else
                                engine._TrackerHandles[handleID] = new WeakReference(h);
                            _Object = h;
                        }
                    }
                }
                else if (_Object is V8NativeObject && ((V8NativeObject)_Object)._Handle.IsEmpty)
                    ((V8NativeObject)_Object)._Handle.Set(this); // (if the object used to track this handle does not represent the current handle, then make sure it does)

                _HandleProxy->ManagedReference = _Object != null ? 2 : 1; // (lets the native side know if there's a managed reference responsible for disposing the native handle proxy)
            }

            return _Object;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Wraps a given native handle proxy to provide methods to operate on it.
        /// </summary>
        internal InternalHandle(HandleProxy* hp, bool keepAlive = false)
        {
            _HandleProxy = null;
            _Object = null;
            _Set(hp);
            if (keepAlive)
                KeepTrack();
        }

        /// <summary>
        /// Sets this instance to the same specified handle value.
        /// </summary>
        public InternalHandle(InternalHandle handle, bool keepAlive = false)
        {
            _HandleProxy = null;
            _Object = null;
            _Set(handle);
            if (keepAlive)
                KeepTrack();
        }

        /// <summary>
        /// Updates the local object reference to point to the manage object that is represented by this handle, and returns
        /// the object found, or null otherwise.
        /// <para>The situations that may necessitate calling this method occur if the managed object ID in the underlying
        /// native proxy object gets updated.  In such (rare and usually specialized) case, this handle will not contain a
        /// reference to the managed object, and the GC might try to collect it. Internally, this method is always called
        /// when wrapping a native ProxyHandle pointer with an InternalHandle, which makes sure any related managed object
        /// also gets properly referenced. The method is also called if 'ObjectID' ever gets updated internally.</para>
        /// </summary>
        public V8NativeObject UpdateObjectReference()
        {
            if (_HandleProxy != null)
            {
                if (!(_Object is V8NativeObject obj) || obj.ID != _HandleProxy->_ObjectID)
                    _Object = null; // (not a valid reference anymore)

                // ... check for a managed object and get the reference for this handle ...

                if (_HandleProxy->_ObjectID >= 0 && _Object == null)
                    _Object = Object;

                _HandleProxy->ManagedReference = _Object != null ? 2 : 1; // (no longer has a managed object reference)
            }
            return _Object as V8NativeObject;
        }

        /// <summary>
        /// Disposes the current handle and sets it to another handle. Before setting, 'KeepAlive()' is called on the given
        /// handle so both handles can be tracked. Once this handle is set you can treat it like any other object
        /// reference and copy it around like a normal value (i.e. no need to keep calling this method). A rule of thumb is to 
        /// either set 'keepAlive' to true when creating a new handle via 'new InternalHandle(...)', or use this method to set
        /// the initial value.
        /// <para>Note 1: Under the new handle system, when 'KeepAlive()' is called (default mode for V8NativeObject handles),
        /// you do not need to call this method anymore. The GC will track it and dispose it when ready.</para>
        /// <para>Note 2: If the current handle is locked (see IsLocked) then an exception error can occur.</para>
        /// </summary>
        public InternalHandle Set(InternalHandle h)
        {
            return _Set(h.KeepTrack());
        }

        InternalHandle _Set(HandleProxy* hp)
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
                    var currentHandleProxies = engine._HandleProxies;

                    if (handleID >= currentHandleProxies.Length)
                        lock (engine._HandleProxies)
                        {
                            // (need to resize the handle arrays, starting with the native array, then the trackers)

                            // ... resize the handle proxy array ...
                            HandleProxy*[] _newHandleProxiesArray = new HandleProxy*[(100 + handleID) * 2];
                            Array.Copy(currentHandleProxies, _newHandleProxiesArray, currentHandleProxies.Length);
                            engine._HandleProxies = currentHandleProxies = _newHandleProxiesArray;
                        }

                    currentHandleProxies[handleID] = _HandleProxy;

#if TRACE
                    if (engine._HandleProxyDiscoveryStacks.Length < currentHandleProxies.Length)
                        Array.Resize(ref engine._HandleProxyDiscoveryStacks, currentHandleProxies.Length);
                    engine._HandleProxyDiscoveryStacks[handleID] = Description + ": " + Environment.NewLine + Environment.StackTrace;
#endif


                    if (_HandleProxy->ManagedReference == 0)
                    {
                        _HandleProxy->ManagedReference = 1;

                        GC.AddMemoryPressure((Marshal.SizeOf(typeof(HandleProxy)))); // (many handle instances can share one proxy)

#if DEBUG && TRACKHANDLES
                        //V8Engine.AllInternalHandlesEverCreated.Add(GetUntrackedHandle(this));
#endif
                    }

                    GetTrackableHandle(false);
                }
            }

            return this;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        ///     Returns true if this handle is directly accessed on a V8NativeObject based object, or the global object.  Such
        ///     handles are disposed under a controlled process that must synchronize across both V8 and V8.Net environments (native
        ///     and managed sides).
        /// </summary>
        /// <value> A true or false value. </value>
        public bool IsLocked
        {
            get
            {
                var engine = Engine;

                if (_HandleProxy == null || engine == null)
                    return false;

                if (_Object == null)
                    GetTrackableHandle(false);

                unsafe
                {
                    // ... this little trick uses the wrapped unsafe pointer to determine if this value exists directly
                    // on the underlying object, which is the case if the pointer to the proxy pointer is that same 
                    // between this value, and the one on the target object ...

                    fixed (void* ptr1 = &_HandleProxy, ptr2 = &engine._GlobalObject._HandleProxy)
                    {
                        if (ptr1 == ptr2) return true; // (if true, then handle proxy pointer is the same value as the global object handle, and should be locked)
                    }

                    if (_Object == null || !(_Object is V8NativeObject obj))
                        return false;
                    else
                        fixed (void* ptr1 = &_HandleProxy, ptr2 = &obj._Handle._HandleProxy)
                        {
                            return ptr1 == ptr2; // (if false, then handle proxy pointers are copies, so this handle value can be safely cleared)
                        }
                }
            }
        }

        /// <summary>
        /// Returns true if calling 'Dispose()' will release the native side handle immediately on the native side.
        /// All InternalHandle values not kept alive can be disposed quickly.  The only handle left on a managed object cannot
        /// be disposed, nor the global object handle, as these will be locked. If false is returned, then there is a managed
        /// object associated.
        /// </summary>
        //?public bool CanDispose
        //{
        //    get
        //    {
        //        return (_Object == null || !(_Object is V8NativeObject) || ((V8NativeObject)_Object)._Handle.ID != ID);
        //    }
        //}
        public bool CanDispose => !IsLocked;

        /// <summary>
        ///     Releases the unmanaged V8 handle resource. Returns true if released, and false if empty or already released.
        /// </summary>
        /// <param name="finalizer"> True if called via the Handle finalizer. </param>
        internal bool _Finalize(bool finalizer) //? We may no need 'finalizer' anymore.
        {
            if (_HandleProxy != null)
            {
                _HandleProxy->ManagedReference = 1; // (can now dispose, so downgrade this just in case)
                // DO NOT clear '_HandleProxy->_ObjectID' as the engine needs it to clear references on it's end, if an object existed)
                _HandleProxy->Disposed |= 2; // (flag that the managed side is done with it)
                V8NetProxy.DisposeHandleProxy(_HandleProxy); // (note: this will not work unless '__HandleProxy->Disposed' is 1, which starts the disposal process)

                _CurrentObjectID = -1;

                GC.RemoveMemoryPressure((Marshal.SizeOf(typeof(HandleProxy))));

                _HandleProxy = null;
                _Object = null;
            }
            return true;
        }

        bool _Dispose(bool ignoreErrors)
        {
            if (CanDispose)
            {
                _Finalize(false);
                return true;
            }
            else if (IsLocked && !ignoreErrors)
                throw new InvalidOperationException("The handle is locked and cannot be disposed. Locked handles are either the global object, or belong to 'V8NativeObject' objects, which are responsible for disposing them under controlled conditions.");
            return false;
        }

        /// <summary>
        /// Forces disposal of the underlying native handle.
        /// </summary>
        public void ForceDispose() { _Finalize(false); }

        /// <summary>
        ///     Disposes of the underlying native handle. If the handle cannot be disposed (perhaps because it is locked), an
        ///     <see cref="InvalidOperationException"/> error will occur. Optionally you may find 'TryDispose()' more convenient to ignore such
        ///     situations.
        ///     <para>
        ///     If the handle represents a managed V8NativeObject instance, the handle cannot be disposed externally. Managed
        ///     objects will begin a disposal process when there are no more managed references. When this occurs, the native side
        ///     V8 handle is made "weak".  When there are no more references in V8, the V8's GC calls back into the managed side to
        ///     notify that disposal can complete. In all other cases, disposing a handle will succeed.
        ///     </para>
        /// </summary>
        public void Dispose() { _Dispose(false); }

        /// <summary>
        /// Same as 'Dispose()', except that any errors are suppressed (i.e. if the handle is locked). 
        /// If successful (which usually just means this handle was cleared), then 'true' is returned.
        /// </summary>
        public bool TryDispose()
        {
            return _Dispose(true);
        }

        /// <summary>
        /// True if this handle is going through a disposal process on the native side.
        /// </summary>
        public bool IsDisposing
        {
            get { return _HandleProxy != null && _HandleProxy->IsDisposing; }
        }

        /// <summary>
        /// Returns true if this handle is disposed (no longer in use).  Disposed native proxy handles are kept in a cache for performance reasons.
        /// </summary>
        public bool IsDisposed { get { return _HandleProxy == null || _HandleProxy->IsDisposed; } }

        /// <summary>
        /// Returns true if the managed side tried to dispose the handle. This sets a bit flag that the handle can be disposed when the native side is ready.
        /// </summary>
        public bool IsCLRDisposed
        {
            get { return _HandleProxy != null && _HandleProxy->IsCLRDisposed; }
        }

        // --------------------------------------------------------------------------------------------------------------------

        public static implicit operator Handle(InternalHandle handle)
        {
            return handle._HandleProxy == null ? Handle.Empty : handle.GetTrackableHandle();
        }

        public static implicit operator V8NativeObject(InternalHandle handle)
        {
            return handle.Object;
        }

        public static implicit operator HandleProxy* (InternalHandle handle)
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
        #region ### SHARED HANDLE CODE START ###

        /// <summary>
        /// The ID (index) of this handle on both the native and managed sides.
        /// </summary>
        public int ID { get { return _HandleProxy != null ? _HandleProxy->ID : -1; } }

        /// <summary>
        /// The JavaScript type this handle represents.
        /// </summary>
        public JSValueType ValueType { get { return _HandleProxy != null ? _HandleProxy->_Type : JSValueType.Uninitialized; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A reference to the V8Engine instance that owns this handle.
        /// </summary>
        public V8Engine Engine { get { return _HandleProxy != null && _HandleProxy->EngineID >= 0 && _HandleProxy->EngineID < V8Engine._Engines.Length ? V8Engine._Engines[_HandleProxy->EngineID] : null; } }

        // --------------------------------------------------------------------------------------------------------------------
        // Managed Object Properties and References

        /// <summary>
        ///     The ID of the managed object represented by this handle. This ID is expected when handles are passed to
        ///     'V8ManagedObject.GetObject()'.
        ///     <para>If this value is less than 0 then it is a user specified ID tracking value, and as such there is no associated
        ///     'V8NativeObject' object, and the 'Object' property will be null.  This can occur when native objects are created in
        ///     script and returned to the managed side. </para>
        /// </summary>
        /// <value> The identifier of the object. </value>
        public Int32 ObjectID
        {
            get
            {
                return _HandleProxy == null ? -1 : _HandleProxy->_ObjectID < -1 || _HandleProxy->_ObjectID >= 0 ? _HandleProxy->_ObjectID : -1;
            }
            internal set
            {
                if (_HandleProxy != null)
                    _HandleProxy->_ObjectID = value;
                UpdateObjectReference();
            }
        }

        /// <summary>
        /// Returns the managed object ID "as is" from the native HandleProxy object.
        /// </summary>
        internal Int32 _CurrentObjectID
        {
            get { return _HandleProxy != null ? _HandleProxy->_ObjectID : -1; }
            set { if (_HandleProxy != null) _HandleProxy->_ObjectID = value; }
        }

        /// <summary>
        /// A reference to the managed object associated with this handle. This property is only valid for object handles, and
        /// will return null otherwise.
        /// Because this property reads from the internal engine objects list, which are store as weak references, this property
        /// can potentially return 'null', even if 'HasObject' returns true.
        /// <para>Note: Checking 'HasObject' can be initially faster, depending on situation.</para>
        /// </summary>
        public V8NativeObject Object
        {
            get
            {
                if (_Object != null)
                    return _Object as V8NativeObject;
                else if (_HandleProxy != null && _HandleProxy->_ObjectID >= 0 && Engine != null)
                    return Engine._GetExistingObject(_HandleProxy->_ObjectID);
                return null;
            }
        }

        /// <summary>
        /// If this handle represents an object instance binder, then this returns the bound object.
        /// Bound objects are usually custom user objects (non-V8.NET objects) wrapped in ObjectBinder instances.
        /// </summary>
        public object BoundObject { get { return Object == null ? null : BindingMode == BindingMode.Instance ? ((ObjectBinder)Object).Object : null; } }

        object IBasicHandle.Object { get { return BoundObject ?? Object; } }

        InternalHandle IHandleBased.InternalHandle { get { return this; } }

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
                return Object == null ? null : BindingMode == BindingMode.Static ? ((TypeBinderFunction)Object).TypeBinder
                    : BindingMode == BindingMode.Instance ? ((ObjectBinder)Object).TypeBinder : null;
            }
        }

        /// <summary>
        /// Returns true if this handle is associated with a managed object (checks if ObjectID is valid).
        /// <para>Note: This can be false even though 'IsObjectType' may be true.
        /// A handle can represent a native V8 object handle without requiring an associated managed object. In such case,
        /// 'HasObject' returns false.</para>
        /// <para>Warning: True does not guarantee that 'Object' will not be null.  Objects are referenced by a numerical ID
        /// and the 'WeakReference' entry may become null under specialized circumstances.  No object reference is actually
        /// pulled until requested by calling 'GetTrackerHandle()',  'KeepAlive()', or 'UpdateObjectReference()'.
        /// If any of the previously mentioned methods are called, then this property can be reliable. </para>
        /// </summary>
        public bool HasObject
        {
            get
            {
                return _HandleProxy->_ObjectID >= 0;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        ///     Reading from this property returns either the underlying managed object, or else causes a native call to fetch the
        ///     current V8 value associated with this handle (for primitive types only - for Arrays, see ArrayLength and
        ///     GetProperty(Int32)).
        ///     <para>For objects, this returns the in-script type text as a string - unless this handle represents an object binder,
        ///     in which case this will return the bound object instead.</para>
        /// </summary>
        /// <value> The value. </value>
        public object Value
        {
            get
            {
                if (_HandleProxy != null)
                {
                    if (IsBinder)
                        return BoundObject;

                    if (CLRTypeID >= 0)
                    {
                        var argInfo = new ArgInfo(this);
                        return argInfo.ValueOrDefault; // (this object represents a ArgInfo object, so return its value)
                    }

                    V8NetProxy.UpdateHandleValue(_HandleProxy);
                    return _HandleProxy->Value;
                }
                else return null;
            }
        }

        /// <summary>
        ///     Reading from this property returns either the underlying managed object, or else causes a ONE-TIME native call to
        ///     fetch the current V8 value associated with this handle. This can be a bit faster, as subsequent calls return the
        ///     same value.
        ///     <para>Note: If the underlying V8 proxy handle value changes (i.e. properties), you should use the 'Value' property
        ///     instead to make sure any changes are reflected each time.  Only use this property more than once if you're sure the
        ///     target of the V8 handle will not change.</para>
        /// </summary>
        /// <value> The last value. </value>
        public object LastValue
        {
            get
            {
                if (_HandleProxy != null)
                {
                    if (IsBinder)
                        return BoundObject;

                    if (CLRTypeID >= 0)
                    {
                        var argInfo = new ArgInfo(this);
                        return argInfo.ValueOrDefault; // (this object represents a ArgInfo object, so return its value)
                    }

                    if (_HandleProxy->_Type != JSValueType.Uninitialized)
                        V8NetProxy.UpdateHandleValue(_HandleProxy);
                    return _HandleProxy->Value;
                }
                else return null;
            }
        }

        /// <summary>
        /// Returns the array length for handles that represent arrays. For all other types, this returns 0.
        /// Note: To get the items of the array, use 'GetProperty(#)'.
        /// </summary>
        public Int32 ArrayLength { get { return IsArray ? V8NetProxy.GetArrayLength(_HandleProxy) : 0; } }

        // --------------------------------------------------------------------------------------------------------------------

        ///// <summary>
        ///// Returns 'true' if this handle is associated with a managed object that has no other CLR based references, and the
        ///// managed GC finalizer has attempted to claim it. The underlying native handle may also be in a weak state, in which 
        ///// case 'IsNativelyWeak' will also return true.
        ///// </summary>
        //?public bool IsWeakManagedObject
        //{
        //    get
        //    {
        //        return _HandleProxy != null && _HandleProxy->IsDisposing;
        //    }
        //}

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Forces the underlying object, if any, to separate from the handle.  This is done by swapping the managed object with
        /// a place holder object to keep the ID (index) for the current object alive until the native V8 engine's GC can remove
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
                        if (weakRef.Target is V8NativeObject obj)
                        {
                            var placeHolder = new V8NativeObject
                            {
                                _Engine = obj._Engine,
                                Template = obj.Template
                            };
                            weakRef.Target = placeHolder; // (this must be done first before moving the handle to the new object!)
                            placeHolder._Handle = obj._Handle;
                            placeHolder._Handle._Object = placeHolder;
                            obj.Template = null;
                            obj._ID = null;
                            obj._Handle = InternalHandle.Empty;
                            return obj;
                        }
                    }
                }
            return null;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns true if this handle is empty (that is, equal to 'Handle.Empty'), and false if a valid handle exists. This 
        /// will also return true if the underlying proxy object ends up in a disposed state.
        /// <para>An empty state is when a handle is set to 'InternalHandle.Empty' and has no valid native V8 handle assigned.
        /// This is similar to "undefined"; however, this property will be true if a valid native V8 handle exists that is set to "undefined".</para>
        /// </summary>
        public bool IsEmpty { get { return _HandleProxy == null || _HandleProxy->IsDisposed; } }

        /// <summary>
        /// Returns true if this handle is undefined or empty (empty is when this handle is an instance of 'Handle.Empty', or is disposed).
        /// <para>"Undefined" does not mean "null".  A variable (handle) can be defined and set to "null".</para>
        /// </summary>
        public bool IsUndefined { get { return IsEmpty || ValueType == JSValueType.Undefined; } }

        /// <summary>
        /// Returns 'true' if this handle represents a 'null' value (that is, an explicit 'null' value).
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
                return ValueType != JSValueType.Uninitialized &&
                    (ValueType == JSValueType.BoolObject
                    || ValueType == JSValueType.NumberObject
                    || ValueType == JSValueType.StringObject
                    || ValueType == JSValueType.Function
                    || ValueType == JSValueType.Date
                    || ValueType == JSValueType.RegExp
                    || ValueType == JSValueType.Array
                    || ValueType == JSValueType.Object);
            }
        }

        /// <summary>
        /// Used internally to quickly determine when an instance represents a binder object type (faster than reflection!).
        /// </summary>
        public bool IsBinder { get { return BindingMode != BindingMode.None; } }

        /// <summary>
        /// Returns the binding mode (Instance, Static, or None) represented by this handle.  The return is 'None' (0) if not applicable.
        /// </summary>
        public BindingMode BindingMode { get { V8NativeObject o = Object; return o != null ? o._BindingMode : BindingMode.None; } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the 'Value' property type cast to the expected type (this will not cast from native arrays or objects to a CLR type).
        /// <para>Warning: No conversion is made between different value types.</para>
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

        public string DisposalStatus
        {
            get
            {
                var d = _HandleProxy->Disposed; // (flags: 0 = handle is in use, 1 = native side is done with it, 2 = managed side is done with it, 3 - VIRTUALLY disposed [cached on native side for reuse]), 4 = queued for making weak (managed side call), 8 = 'weak' removal in progress, 16 = queued for disposal.
                var msg = "";

                if ((d & 3) == 0) msg += "Active";
                else if ((d & 3) == 3) msg += "Disposed and cached";
                else if ((d & 1) > 0) msg += "Native Disposed ready";
                else if ((d & 2) > 0) msg += "Managed Disposed ready";

                if ((d & 4) > 0) msg += "; queued to make weak";
                if ((d & 8) > 0) msg += "; queued to make strong";
                if ((d & 16) > 0) msg += "; queued for disposal";

                if (_HandleProxy->ManagedReference == 0) msg += "; native handle";
                else if (_HandleProxy->ManagedReference == 1) msg += "; internal handle";
                else if (_HandleProxy->ManagedReference == 2) msg += "; tracked handle";
                else msg += "; invalid managed ref id: " + _HandleProxy->ManagedReference;

                return msg + $" ({d})";
            }
        }

        internal string _IDStatus { get { return "Engine ID: " + _HandleProxy->EngineID + ", Handle ID: " + _HandleProxy->ID + ", Object ID: " + _HandleProxy->_ObjectID + ", Locked: " + IsLocked; } }

        /// <summary>
        /// Returns a string describing the handle (mainly for debugging purposes).
        /// </summary>
        public string Description
        {
            get
            {
                try
                {
                    if (IsEmpty) return "<empty>";
                    if (_HandleProxy->EngineID < 0 || Engine == null) return "<detached>"; // (doesn't belong anywhere - gone rogue! ;) )
                    if (IsDisposed) return "<" + _IDStatus + ",  " + DisposalStatus + ">";
                    if (IsUndefined) return "undefined";

                    if (IsBinder)
                    {
                        if (BindingMode == BindingMode.Static)
                        {
                            var obj = Object;
                            if (obj != null)
                            {
                                var typeBinder = ((TypeBinderFunction)obj).TypeBinder;
                                return "<Type Binder: " + DisposalStatus + ", " + typeBinder.BoundType.FullName + ", " + _IDStatus + ">";
                            }
                            else
                            {
                                //?if (System.Diagnostics.Debugger.IsAttached)
                                //    System.Diagnostics.Debugger.Break();
                                return "<Type Binder: Error - no type binder instance exists.>";
                            }
                        }
                        else
                        {
                            var obj = BoundObject;
                            if (obj != null)
                                return "<Object Binder: " + DisposalStatus + ", " + obj.ToString() + ", " + _IDStatus + ">";
                            else
                            {
                                //?if (System.Diagnostics.Debugger.IsAttached)
                                //    System.Diagnostics.Debugger.Break();
                                return "<Object Binder: Error - no object binder instance exists.>";
                            }
                        }
                    }
                    else if (IsObjectType)
                    {
                        string managedType = "";

                        var mo = Engine._GetExistingObject(ObjectID); // (don't access 'Object', which may cause an object to be pulled and set for tracking on this handle)
                        if (mo != null)
                        {
                            var type = mo.GetType();
                            managedType = " (" + type.Name + (type != typeof(V8NativeObject) && typeof(V8NativeObject).IsAssignableFrom(type) ? " [V8NativeObject]" : "") + ")";
                        }

                        var typeName = Engine.GlobalObject != this ? Enum.GetName(typeof(JSValueType), ValueType) : "global";

                        return "<object: " + typeName + managedType + ", " + DisposalStatus + ", " + _IDStatus + ">";
                    }
                    else
                    {
                        // ... this is a value type ...
                        var val = Value;
                        return "<value: " + DisposalStatus + ", " + (string)Types.ChangeType(val, typeof(string)) + ", " + _IDStatus + ">";
                    }
                }
                catch (Exception ex)
                {
                    return Exceptions.GetFullErrorMessage(ex);
                }
            }
        }

        /// <summary>
        /// If this handle represents an object, the 'Description' property value is returned, otherwise the primitive value
        /// is returned as a string.
        /// <para>Note: This does not pull the same string as calling 'toString()' in JavaScript.  You have to use the 'Call()' 
        /// method on this handle to call that function - as you would normally.</para>
        /// </summary>
        public override string ToString()
        {
            try
            {
                if (IsEmpty) return "<empty>";
                if (_HandleProxy->EngineID < 0 || Engine == null) return "<detached>"; // (doesn't belong anywhere - gone rogue! ;) )
                if (IsDisposed) return "<" + _IDStatus + ", Object ID: " + _HandleProxy->_ObjectID + ",  " + DisposalStatus + ">";
                if (IsUndefined) return "undefined";
                if (IsObjectType) return Description;
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
            return obj is IHandleBased && _HandleProxy == ((IHandleBased)obj).InternalHandle._HandleProxy;
        }

        public override int GetHashCode()
        {
            return (int)ID;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns true if this handle contains an error message (the string value is the message).
        /// If you have exception catching in place, you can simply call 'ThrowOnError()' instead.
        /// <para>Note: This returns false if a script was terminated forcibly. Instead, check the 'WasTerminated' property.</para>
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

        /// <summary>
        /// Returns true if execution was terminated, usually by calling '{V8Engine}.TerminateExecution()'.
        /// </summary>
        public bool WasTerminated { get { return ValueType == JSValueType.ExecutionTerminated; } }

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

        /// <summary> Calls the V8 'Set()' function on the underlying native object. Returns true if successful. </summary>
        /// <exception cref="ArgumentNullException"> Thrown when one or more required arguments are null. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when the requested operation is invalid. </exception>
        /// <param name="name"> The name. </param>
        /// <param name="value"> The value. </param>
        /// <param name="attributes">
        ///     (Optional) Flags that describe the property behavior.  They must be 'OR'd together as needed.
        /// </param>
        /// <returns> True if it succeeds, false if it fails. </returns>
        /// <seealso cref="M:V8.Net.IV8Object.SetProperty(string,InternalHandle,V8PropertyAttributes)"/>
        public bool SetProperty(string name, InternalHandle value, V8PropertyAttributes attributes = V8PropertyAttributes.None)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.SetObjectPropertyByName(this, name, value, attributes);
        }

        /// <summary> Calls the V8 'Set()' function on the underlying native object. Returns true if successful. </summary>
        /// <exception cref="InvalidOperationException"> Thrown when the requested operation is invalid. </exception>
        /// <param name="index"> Zero-based index to set. </param>
        /// <param name="value"> The value to set. </param>
        /// <param name="attributes">
        ///     (Optional) Flags that describe the property behavior.  They must be 'OR'd together as needed.
        ///     <para>Warning: V8 does not support setting attributes using numerical indexes.  If you set an attribute, the given
        ///     value is converted to a string, and a named property setter will be used instead. </para>
        /// </param>
        /// <returns> True if it succeeds, false if it fails. </returns>
        /// <seealso cref="M:V8.Net.IV8Object.SetProperty(Int32,InternalHandle,V8PropertyAttributes)"/>
        public bool SetProperty(Int32 index, InternalHandle value, V8PropertyAttributes attributes = V8PropertyAttributes.None)
        {
            // ... can only set properties on objects ...

            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.SetObjectPropertyByIndex(this, index, value);
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
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            if (name.IsNullOrWhiteSpace())
                if (obj == null) throw new InvalidOperationException("You cannot pass 'null' without a valid property name.");
                else
                    name = obj.GetType().Name;

            if (obj is IHandleBased)
                return SetProperty(name, ((IHandleBased)obj).InternalHandle, (V8PropertyAttributes)(memberSecurity ?? ScriptMemberSecurity.ReadWrite));

            if (obj == null || obj is string || obj.GetType().IsValueType) // TODO: Check enum support.
                return SetProperty(name, Engine.CreateValue(obj), (V8PropertyAttributes)(memberSecurity ?? ScriptMemberSecurity.ReadWrite));

            var nObj = Engine.CreateBinding(obj, className, recursive, memberSecurity);

            if (memberSecurity != null)
                return SetProperty(name, nObj, (V8PropertyAttributes)memberSecurity);
            else
                return SetProperty(name, nObj);
        }

        /// <summary>
        /// Sets a property to a given object. If the object is not V8.NET related, then the system will attempt to bind the instance and all public members to
        /// the specified property name.
        /// Returns true if successful.
        /// </summary>
        /// <param name="index"> Zero-based index to set. </param>
        /// <param name="obj">Some value or object instance. 'Engine.CreateValue()' will be used to convert value types, unless the object is already a handle, in which case it is set directly.</param>
        /// <param name="className">A custom in-script function name for the specified object type, or 'null' to use either the type name as is (the default) or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">For object instances, if true, then object reference members are included, otherwise only the object itself is bound and returned.
        /// For security reasons, public members that point to object instances will be ignored. This must be true to included those as well, effectively allowing
        /// in-script traversal of the object reference tree (so make sure this doesn't expose sensitive methods/properties/fields).</param>
        /// <param name="memberSecurity">For object instances, these are default flags that describe JavaScript properties for all object instance members that
        /// don't have any 'ScriptMember' attribute.  The flags should be 'OR'd together as needed.</param>
        public bool SetProperty(int index, object obj, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        {
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            if (obj is IHandleBased)
                return SetProperty(index, ((IHandleBased)obj).InternalHandle, (V8PropertyAttributes)(memberSecurity ?? ScriptMemberSecurity.ReadWrite));

            if (obj == null || obj is string || obj.GetType().IsValueType) // TODO: Check enum support.
                return SetProperty(index, Engine.CreateValue(obj), (V8PropertyAttributes)(memberSecurity ?? ScriptMemberSecurity.ReadWrite));

            var nObj = Engine.CreateBinding(obj, className, recursive, memberSecurity);

            if (memberSecurity != null)
                return SetProperty(index, nObj, (V8PropertyAttributes)memberSecurity);
            else
                return SetProperty(index, nObj);
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
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

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
            if (name.IsNullOrWhiteSpace())
                throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return new InternalHandle(V8NetProxy.GetObjectPropertyByName(this, name), true);
        }

        /// <summary>
        /// Calls the V8 'Get()' function on the underlying native object.
        /// If the property doesn't exist, the 'IsUndefined' property will be true.
        /// </summary>
        public InternalHandle GetProperty(Int32 index)
        {
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return new InternalHandle(V8NetProxy.GetObjectPropertyByIndex(this, index), true);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the V8 'Delete()' function on the underlying native object.
        /// Returns true if the property was deleted.
        /// </summary>
        public bool DeleteProperty(string name)
        {
            if (name.IsNullOrWhiteSpace())
                throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.DeleteObjectPropertyByName(this, name);
        }

        /// <summary>
        /// Calls the V8 'Delete()' function on the underlying native object.
        /// Returns true if the property was deleted.
        /// </summary>
        public bool DeleteProperty(Int32 index)
        {
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.DeleteObjectPropertyByIndex(this, index);
        }

        // --------------------------------------------------------------------------------------------------------------------
        // TODO: DOES NOT WORK PROPERLY!!!!!!!!!!!!!!!!!!!

        /// <summary>
        ///     Calls the V8 'SetAccessor()' function on the underlying native object to create a property that is controlled by
        ///     "getter" and "setter" callbacks.
        ///     <para>WARNING: If you try to set managed accessors on a native-ONLY object (as in, this handle does not yet have a
        ///     managed-side object associated with it) then
        ///     <see cref="V8Engine.CreateObject(InternalHandle, bool)"/> will be called to create a wrapper object so the
        ///     accessor delegates will not get garbage collected, causing errors. You can optionally take control of this yourself
        ///     and call one of the 'CreateObject()' methods on <see cref="V8Engine"/>.</para>
        /// </summary>
        /// <exception cref="ArgumentNullException"> Thrown when one or more required arguments are null. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when the requested operation is invalid. </exception>
        /// <param name="name"> The property name. </param>
        /// <param name="getter">
        ///     The property getter delegate that returns a value when the property is accessed within JavaScript.
        /// </param>
        /// <param name="setter">
        ///     The property setter delegate that sets AND returns a value when the property is accessed within JavaScript.
        /// </param>
        /// <param name="attributes"> (Optional) The attributes to assign to the property. </param>
        /// <param name="access"> (Optional) The access security on the property. </param>
        /// <seealso cref="M:V8.Net.IV8Object.SetAccessor(string,GetterAccessor,SetterAccessor,V8PropertyAttributes,V8AccessControl)"/>
        public void SetAccessor(string name, GetterAccessor getter, SetterAccessor setter,
            V8PropertyAttributes attributes = V8PropertyAttributes.None, V8AccessControl access = V8AccessControl.Default)
        {
            if (name.IsNullOrWhiteSpace())
                throw new ArgumentNullException(nameof(name), "Cannot be null, empty, or only whitespace.");
            if (attributes == V8PropertyAttributes.Undefined)
                attributes = V8PropertyAttributes.None;
            if (attributes < 0) throw new InvalidOperationException("'attributes' has an invalid value.");
            if (access < 0) throw new InvalidOperationException("'access' has an invalid value.");

            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            var engine = Engine;

            if (!(Object is V8NativeObject o)) // (we need to first get the managed object if object exists)
                o = engine.CreateObject(this); // (create a managed object to store and keep the accessors alive)

            o.SetAccessor(name, getter, setter, attributes, access);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns a list of all property names for this object (including all objects in the prototype chain).
        /// </summary>
        public string[] GetPropertyNames()
        {
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

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
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

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
            if (name.IsNullOrWhiteSpace())
                throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return V8NetProxy.GetPropertyAttributes(this, name);
        }

        // --------------------------------------------------------------------------------------------------------------------

        internal InternalHandle _Call(string functionName, InternalHandle _this, params InternalHandle[] args)
        {
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            HandleProxy** nativeArrayMem = Utilities.MakeHandleProxyArray(args);

            var result = V8NetProxy.Call(this, functionName, _this, args.Length, nativeArrayMem);

            Utilities.FreeNativeMemory((IntPtr)nativeArrayMem);

            return result;
        }

        /// <summary>
        /// Calls the specified function property on the underlying object.
        /// The '_this' parameter is the "this" reference within the function when called.
        /// </summary>
        public InternalHandle Call(string functionName, InternalHandle _this, params InternalHandle[] args)
        {
            if (functionName.IsNullOrWhiteSpace())
                throw new ArgumentNullException("functionName (cannot be null, empty, or only whitespace)");
            
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return _Call(functionName, _this, args);
        }

        /// <summary>
        /// Calls the specified function property on the underlying object.
        /// The 'this' property will not be specified, which will default to the global scope as expected.
        /// </summary>
        public InternalHandle StaticCall(string functionName, params InternalHandle[] args)
        {
            if (functionName.IsNullOrWhiteSpace())
                throw new ArgumentNullException("functionName (cannot be null, empty, or only whitespace)");

            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return _Call(functionName, InternalHandle.Empty, args);
        }

        /// <summary>
        /// Calls the underlying object as a function.
        /// The '_this' parameter is the "this" reference within the function when called.
        /// </summary>
        public InternalHandle Call(InternalHandle _this, params InternalHandle[] args)
        {
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return _Call(null, _this, args);
        }

        /// <summary>
        /// Calls the underlying object as a function.
        /// The 'this' property will not be specified, which will default to the global scope as expected.
        /// </summary>
        public InternalHandle StaticCall(params InternalHandle[] args)
        {
            if (!IsObjectType)
                throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);

            return _Call(null, InternalHandle.Empty, args);
        }

        // --------------------------------------------------------------------------------------------------------------------

        [Obsolete("Using object inspector on this handle will cause handle leaks when this property is read from.  Please use 'GetPrototype()' instead.", true)]
        public InternalHandle Prototype // (cannot be a property, else the object inspector will cause handle leaks)
        { get { throw new NotSupportedException(); } }

        /// <summary>
        /// The prototype of the object (every JavaScript object implicitly has a prototype).
        /// <para>Note: As with any InternalHandle returned, you are responsible to dispose it.  It is recommended to type cast
        /// this to an ObjectHandle before use.</para>
        /// </summary>
        public InternalHandle GetPrototype() // (cannot be a property, else )
        {
            if (!IsObjectType) throw new InvalidOperationException(_NOT_AN_OBJECT_ERRORMSG);
            return V8NetProxy.GetObjectPrototype(_HandleProxy);
        }

        // --------------------------------------------------------------------------------------------------------------------

        HandleProxy* INativeHandleBased.GetNativeHandleProxy()
        {
            return (HandleProxy*)this;
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    /// <summary>
    /// Intercepts JavaScript access for properties on the associated JavaScript object for retrieving a value.
    /// <para>To allow the V8 engine to perform the default get action, return "Handle.Empty".</para>
    /// </summary>
    public delegate InternalHandle GetterAccessor(InternalHandle _this, string propertyName);

    /// <summary>
    /// Intercepts JavaScript access for properties on the associated JavaScript object for setting values.
    /// <para>To allow the V8 engine to perform the default set action, return "Handle.Empty".</para>
    /// </summary>
    public delegate InternalHandle SetterAccessor(InternalHandle _this, string propertyName, InternalHandle value);
    // ========================================================================================================================
}

