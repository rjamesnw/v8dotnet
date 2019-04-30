﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

#if V2 || V3 || V3_5
#else
using System.Dynamic;
#endif

namespace V8.Net
{
    // ========================================================================================================================

    public interface ITemplate
    {
        /// <summary>
        /// The V8Engine instance associated with this template.
        /// </summary>
        V8Engine Engine { get; }
    }

    internal interface ITemplateInternal
    {
        uint _ReferenceCount { get; set; }
    }

    // ========================================================================================================================

    public unsafe abstract class TemplateBase<ObjectType> : ITemplate, ITemplateInternal, IV8Disposable where ObjectType : class, IV8NativeObject
    {
        // --------------------------------------------------------------------------------------------------------------------

        public V8Engine Engine { get { return _Engine; } }
        internal V8Engine _Engine;

        ///// <summary>
        ///// Returns true if this template object has been placed into the "abandoned" list due to a GC finalization attempt.
        ///// </summary>
        //?internal bool _IsAbandoned { get { lock (_Engine._AbandondObjects) { return _Engine != null && _Engine._AbandondObjects.Contains(this); } } }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the parent to this template, if any.
        /// This is currently only set on object template instances associated with function templates (where {FunctionTemplate} is the parent).
        /// </summary>
        public ITemplate Parent
        {
            get { return _Parent; }
            internal set
            {
                if (_Parent != null) ((ITemplateInternal)this)._ReferenceCount--;
                _Parent = value;
                if (_Parent != null) ((ITemplateInternal)this)._ReferenceCount++;
            }
        }
        ITemplate _Parent;

        /// <summary>
        /// The number of objects that reference this object.
        /// This is required because of the way the GC resets all weak references to null, and finalizes in no special order.
        /// Dependent objects are required to update this when they are finally collected (as some may become re-registered with the finalizer).
        /// </summary>
        uint ITemplateInternal._ReferenceCount { get; set; }

        // --------------------------------------------------------------------------------------------------------------------

        protected List<Delegate> _NativeCallbacks; // (if not stored here, then delegates will be GC'd and callbacks from native code will fail)

        /// <summary>
        /// Keeps callback delegates alive.
        /// <para>If delegates are used as callbacks (for reverse P/Invoke), then they will become GC'd if there's no managed reference keeping them alive.</para>
        /// </summary>
        protected T _SetDelegate<T>(T d)
        {
            if (_NativeCallbacks == null)
                _NativeCallbacks = new List<Delegate>();

            _NativeCallbacks.Add((Delegate)(object)d);

            return d;
        }

        // --------------------------------------------------------------------------------------------------------------------

        public TemplateBase()
        {
        }
        ~TemplateBase()
        {
            if (!_Finalize(true))
                GC.ReRegisterForFinalize(this); // (something failed on the native side so need to try again)
            //?this.Finalizing();
        }

        protected abstract bool _Finalize(bool finalizer);

        /// <summary> Returns true if this template has child objects created from it. </summary>
        public virtual bool HasChildObjects
        {
            get
            {
                return (((ITemplateInternal)this)._ReferenceCount > 0
                    || (Parent == null || ((ITemplateInternal)Parent)._ReferenceCount > 0));
            }
        }

        public bool CanDispose => true;

        public void Dispose() => _Finalize(false);

        protected abstract void OnInitialized();

        // --------------------------------------------------------------------------------------------------------------------

        protected HandleProxy* _NamedPropertyGetter(string propertyName, ref ManagedAccessorInfo info)
        {
            try
            {
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return null;
                var mo = obj as IV8ManagedObject;
                var result = mo != null ? mo.NamedPropertyGetter(ref propertyName) : null;
                return result;
            }
            catch (Exception ex)
            {
                if (ex.InnerException == V8ValueException.Null)
                {
                    return Engine.CreateNullValue();
                }

                if (ex.InnerException == V8ValueException.Undefined)
                {
                    return InternalHandle.Empty;
                }

                return _Engine.CreateError(Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
            }
        }

        protected HandleProxy* _NamedPropertySetter(string propertyName, HandleProxy* value, ref ManagedAccessorInfo info)
        {
            try
            {
                InternalHandle hValue = value;
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return null;
                var mo = obj as IV8ManagedObject;
                var result = mo != null ? mo.NamedPropertySetter(ref propertyName, hValue, V8PropertyAttributes.Undefined) : null;
                return result;
            }
            catch (Exception ex)
            {
                if (ex.InnerException == V8ValueException.Null)
                {
                    return Engine.CreateNullValue();
                }

                if (ex.InnerException == V8ValueException.Undefined)
                {
                    return InternalHandle.Empty;
                }

                return _Engine.CreateError(Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
            }
        }

        protected V8PropertyAttributes _NamedPropertyQuery(string propertyName, ref ManagedAccessorInfo info)
        {
            try
            {
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return V8PropertyAttributes.Undefined;
                var mo = obj as IV8ManagedObject;
                var result = mo != null ? mo.NamedPropertyQuery(ref propertyName) : null;
                if (result != null) return result.Value;
                else return V8PropertyAttributes.Undefined; // (not intercepted, so perform default action)
            }
            catch
            {
                return V8PropertyAttributes.Undefined; // TODO: Need a better way to marshal/pass these exception object instances (themselves) across the native boundary for the underlying engine instance)
            }
        }

        protected int _NamedPropertyDeleter(string propertyName, ref ManagedAccessorInfo info)
        {
            try
            {
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return -1;
                var mo = obj as IV8ManagedObject;
                var result = mo != null ? mo.NamedPropertyDeleter(ref propertyName) : null;
                if (result != null) return result.Value ? 1 : 0;
                else return -1; // (not intercepted, so perform default action)
            }
            catch
            {
                return -1;
            }
        }

        protected HandleProxy* _NamedPropertyEnumerator(ref ManagedAccessorInfo info)
        {
            try
            {
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return null;
                var mo = obj as IV8ManagedObject;
                return mo != null ? mo.NamedPropertyEnumerator() : null;
            }
            catch (Exception ex)
            {
                return _Engine.CreateError(Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        protected HandleProxy* _IndexedPropertyGetter(Int32 index, ref ManagedAccessorInfo info)
        {
            try
            {
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return null;
                var mo = obj as IV8ManagedObject;
                var result = mo != null ? mo.IndexedPropertyGetter(index) : null;
                return result;
            }
            catch (Exception ex)
            {
                if (ex.InnerException == V8ValueException.Null)
                {
                    return Engine.CreateNullValue();
                }

                if (ex.InnerException == V8ValueException.Undefined)
                {
                    return InternalHandle.Empty;
                }

                return _Engine.CreateError(Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
            }
        }

        protected HandleProxy* _IndexedPropertySetter(Int32 index, HandleProxy* value, ref ManagedAccessorInfo info)
        {
            try
            {
                InternalHandle hValue = value;
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return null;
                var mo = obj as IV8ManagedObject;
                var result = mo != null ? mo.IndexedPropertySetter(index, hValue) : null;
                return result;
            }
            catch (Exception ex)
            {
                if (ex.InnerException == V8ValueException.Null)
                {
                    return Engine.CreateNullValue();
                }

                if (ex.InnerException == V8ValueException.Undefined)
                {
                    return InternalHandle.Empty;
                }

                return _Engine.CreateError(Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
            }
        }

        protected V8PropertyAttributes _IndexedPropertyQuery(Int32 index, ref ManagedAccessorInfo info)
        {
            try
            {
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return V8PropertyAttributes.Undefined;
                var mo = obj as IV8ManagedObject;
                var result = mo != null ? mo.IndexedPropertyQuery(index) : null;
                if (result != null) return result.Value;
                else return V8PropertyAttributes.Undefined; // (not intercepted, so perform default action)
            }
            catch
            {
                return V8PropertyAttributes.Undefined;
            }
        }

        protected int _IndexedPropertyDeleter(Int32 index, ref ManagedAccessorInfo info)
        {
            try
            {
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return -1;
                var mo = obj as IV8ManagedObject;
                var result = mo != null ? mo.IndexedPropertyDeleter(index) : null;
                if (result != null) return result.Value ? 1 : 0;
                else return -1; // (not intercepted, so perform default action)
            }
            catch
            {
                return -1;
            }
        }

        protected HandleProxy* _IndexedPropertyEnumerator(ref ManagedAccessorInfo info)
        {
            try
            {
                var obj = _Engine._GetExistingObject(info.ManagedObjectID);
                if (obj == null)
                    return null;
                var mo = obj as IV8ManagedObject;
                return mo != null ? mo.IndexedPropertyEnumerator() : null;
            }
            catch (Exception ex)
            {
                return _Engine.CreateError(Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
            }
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    public unsafe class ObjectTemplate : TemplateBase<IV8ManagedObject>
    {
        // --------------------------------------------------------------------------------------------------------------------

        internal NativeObjectTemplateProxy* _NativeObjectTemplateProxy;

        // --------------------------------------------------------------------------------------------------------------------

        public ObjectTemplate()
        {
        }

        internal void _Initialize(V8Engine v8EngineProxy, bool registerPropertyInterceptors = true)
        {
            _Initialize(v8EngineProxy,
                V8NetProxy.CreateObjectTemplateProxy(v8EngineProxy._NativeV8EngineProxy), // (create a corresponding native object)
                registerPropertyInterceptors
            );
        }

        internal void _Initialize(V8Engine v8EngineProxy, NativeObjectTemplateProxy* nativeObjectTemplateProxy, bool registerPropertyInterceptors = true)
        {
            if (v8EngineProxy == null)
                throw new ArgumentNullException("v8EngineProxy");

            if (nativeObjectTemplateProxy == null)
                throw new ArgumentNullException("nativeObjectTemplateProxy");

            _Engine = v8EngineProxy;

            _NativeObjectTemplateProxy = nativeObjectTemplateProxy;

            if (registerPropertyInterceptors)
            {
                RegisterNamedPropertyInterceptors();
                RegisterIndexedPropertyInterceptors();
            }

            OnInitialized();
        }

        /// <summary>
        /// Called when the object is initialized instance is ready for use.
        /// </summary>
        protected override void OnInitialized()
        {
        }

        protected override bool _Finalize(bool finalizer) // (note: This can cause issues if removed while the native object exists [because of the callbacks].)
        {
            if (_NativeObjectTemplateProxy != null)
            {
                if (V8NetProxy.DeleteObjectTemplateProxy(_NativeObjectTemplateProxy)) // (delete the corresponding native object as well; WARNING: This may be done on the GC thread!)
                    _NativeObjectTemplateProxy = null;
                else
                    return false; // (bounced, a script might be in progress; try again later)
            }
            return true;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns true if this function template has any name-based interceptors (callbacks) registered.
        /// </summary>
        public bool NamedPropertyInterceptorsRegistered { get; internal set; }

        /// Returns true if this function template has any index-based interceptors (callbacks) registered.
        /// <para>Note: This is numerical indexes only.</para>
        /// </summary>
        public bool IndexedPropertyInterceptorsRegistered { get; internal set; }

        /// <summary>
        /// Registers handlers that intercept access to properties on ALL objects created by this template.  The native V8 engine only supports this on 'ObjectTemplate's.
        /// </summary>
        public void RegisterNamedPropertyInterceptors()
        {
            if (!NamedPropertyInterceptorsRegistered)
            {
                V8NetProxy.RegisterNamedPropertyHandlers(_NativeObjectTemplateProxy,
                    _SetDelegate<ManagedNamedPropertyGetter>(_NamedPropertyGetter),
                    _SetDelegate<ManagedNamedPropertySetter>(_NamedPropertySetter),
                    _SetDelegate<ManagedNamedPropertyQuery>(_NamedPropertyQuery),
                    _SetDelegate<ManagedNamedPropertyDeleter>(_NamedPropertyDeleter),
                    _SetDelegate<ManagedNamedPropertyEnumerator>(_NamedPropertyEnumerator));

                NamedPropertyInterceptorsRegistered = true;
            }
        }

        /// <summary>
        /// Registers handlers that intercept access to properties on ALL objects created by this template.  The native V8 engine only supports this on 'ObjectTemplate's.
        /// </summary>
        public void RegisterIndexedPropertyInterceptors()
        {
            if (!IndexedPropertyInterceptorsRegistered)
            {
                V8NetProxy.RegisterIndexedPropertyHandlers(_NativeObjectTemplateProxy,
                    _SetDelegate<ManagedIndexedPropertyGetter>(_IndexedPropertyGetter),
                    _SetDelegate<ManagedIndexedPropertySetter>(_IndexedPropertySetter),
                    _SetDelegate<ManagedIndexedPropertyQuery>(_IndexedPropertyQuery),
                    _SetDelegate<ManagedIndexedPropertyDeleter>(_IndexedPropertyDeleter),
                    _SetDelegate<ManagedIndexedPropertyEnumerator>(_IndexedPropertyEnumerator));

                IndexedPropertyInterceptorsRegistered = true;
            }
        }

        /// <summary>
        /// Unregisters handlers that intercept access to properties on ALL objects created by this template.  See <see cref="RegisterNamedPropertyInterceptors()"/> and <see cref="RegisterIndexedPropertyInterceptors()"/>.
        /// </summary>
        public void UnregisterPropertyInterceptors()
        {
            if (NamedPropertyInterceptorsRegistered)
            {
                V8NetProxy.UnregisterNamedPropertyHandlers(_NativeObjectTemplateProxy);

                V8NetProxy.UnregisterIndexedPropertyHandlers(_NativeObjectTemplateProxy);

                NamedPropertyInterceptorsRegistered = false;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        [Obsolete("Renamed to 'SetCallAsFunctionHandler()' to stay inline with V8 engine ObjectTemplate function names.", true)]
        public void RegisterInvokeHandler(JSFunction callback) { }

        NativeFunctionCallback _ProxyCallback; // (need to keep a reference)

        /// <summary>
        ///     Registers an invoke handler on the underlying native ObjectTemplate instance, which allows objects to be called like
        ///     a function.
        ///     <para>A proxy delegate is used and stored locally to prevent it from being reclaimed by the
        ///     GC. If you call this method again, the old proxy delegate will be replaced with a new one and registered again on
        ///     the native side. </para>
        /// </summary>
        /// <param name="callback"> A callback that gets invoked when the object is used like a function. </param>
        public void SetCallAsFunctionHandler(JSFunction callback)
        {
            _ProxyCallback = (managedObjectID, isConstructCall, _this, args, argCount) =>
                {
                    return FunctionTemplate._CallBack(managedObjectID, isConstructCall, _this, args, argCount, callback);
                };
            V8NetProxy.SetCallAsFunctionHandler(_NativeObjectTemplateProxy, _ProxyCallback);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates an object of the specified type and returns it.  A V8 object is also created and associated with it.
        /// <para>Performance note: Creating 'V8NativeObject' type objects are allowed, but an object template is not needed for those.  If you create a
        /// 'V8NativeObject' object from a template, it simply wraps the native object create by the template, and property interceptors (call-backs) are still
        /// triggered.  While native objects are faster than managed ones, creating 'V8NativeObject' objects using 'V8Engine.CreateObject()' does not use
        /// interceptors and is many times faster than template objects.  If it is desired to create 'V8NativeObject' objects from templates, consider calling
        /// '<seealso cref="UnregisterPropertyInterceptors()"/>' on the object template to make them the same speed as if 'V8Engine.CreateObject()' was used.</para>
        /// </summary>
        /// <typeparam name="T">The type of managed object to create, which must implement 'IV8NativeObject',</typeparam>
        /// <param name="initialize">If true (default) then then 'IV8NativeObject.Initialize()' is called on the created object before returning.</param>
        public T CreateObject<T>(bool initialize = true) where T : V8NativeObject, new()
        {
            if (_Engine == null)
                throw new InvalidOperationException("You must create object templates by calling one of the 'V8Engine.CreateObjectTemplate()' overloads.");

            if (_NativeObjectTemplateProxy == null)
                throw new InvalidOperationException("This managed object template is either not initialized, or does not support creating V8 objects.");

            // ... create object locally first and index it ...

            var obj = _Engine._CreateManagedObject<T>(this, InternalHandle.Empty);

            // ... create the native object and associated it to the managed wrapper ...

            try
            {
                obj._Handle.Set(V8NetProxy.CreateObjectFromTemplate(_NativeObjectTemplateProxy, obj.ID));
                // (note: setting '_NativeObject' also updates it's '_ManagedObject' field if necessary.
            }
            catch (Exception ex)
            {
                // ... something went wrong, so remove the new managed object ...
                _Engine._RemoveObjectWeakReference(obj.ID);
                throw ex;
            }

            if (initialize)
                obj.Initialize(false, null);

            return (T)obj;
        }

        /// <summary>
        /// See <see cref="CreateObject<T>()"/>.
        /// </summary>
        /// <param name="initialize">If true (default) then then 'IV8NativeObject.Initialize()' is called on the created object before returning.</param>
        public V8ManagedObject CreateObject(bool initialize = true) { return CreateObject<V8ManagedObject>(initialize); }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the V8 'Set()' function on the underlying native object template to set properties that will exist on all objects created from this template.
        /// </summary>
        public void SetProperty(string name, InternalHandle value, V8PropertyAttributes attributes = V8PropertyAttributes.None)
        {
            if (name.IsNullOrWhiteSpace()) throw new ArgumentNullException("name (cannot be null, empty, or only whitespace)");

            V8NetProxy.SetObjectTemplateProperty(_NativeObjectTemplateProxy, name, value, attributes);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        ///     This is updated to hold a reference to the property value getter callback when
        ///     <see cref="SetAccessor(string, GetterAccessor, SetterAccessor, V8PropertyAttributes, V8AccessControl)"/> is called.
        ///     Without a rooted reference the delegate will get garbage collected causing callbacks from the native side will fail.
        /// </summary>
        /// <value> The getter. </value>
        public GetterAccessor Getter { get; private set; }
        NativeGetterAccessor _Getter;
        /// <summary>
        ///     This is updated to hold a reference to the property value setter callback when
        ///     <see cref="SetAccessor(string, GetterAccessor, SetterAccessor, V8PropertyAttributes, V8AccessControl)"/> is called.
        ///     Without a rooted reference the delegate will get garbage collected causing callbacks from the native side will fail.
        /// </summary>
        /// <value> The getter. </value>
        public SetterAccessor Setter { get; private set; }
        NativeSetterAccessor _Setter;

        /// <summary>
        /// Calls the V8 'SetAccessor()' function on the underlying native 'v8::ObjectTenplate' instance to create a property that is controlled by "getter" and "setter" callbacks.
        /// <para>Note: This is template related, which means all objects created from this template will be affected by these special properties.</para>
        /// </summary>
        public void SetAccessor(string name,
            GetterAccessor getter, SetterAccessor setter,
            V8PropertyAttributes attributes = V8PropertyAttributes.None, V8AccessControl access = V8AccessControl.Default)
        {
            attributes = V8NativeObject._CreateAccessorProxies(Engine, name, getter, setter, attributes, access, ref _Getter, ref _Setter);

            Getter = getter;
            Setter = setter;

            V8NetProxy.SetObjectTemplateAccessor(_NativeObjectTemplateProxy, -1, name, _Getter, _Setter, access, attributes);
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
