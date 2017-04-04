using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Text;

namespace V8.Net
{
    // ========================================================================================================================
#if !(V1_1 || V2 || V3 || V3_5)
    using System.Dynamic;
    using System.Reflection;
#else
    public interface IDynamicMetaObjectProvider { }
    public partial class Expression { public static Expression Empty() { return null; } }
    public enum BindingRestrictions { Empty }
    public partial class DynamicMetaObject
    { public DynamicMetaObject(Expression ex, BindingRestrictions rest, object value) { } }
#endif

    // ========================================================================================================================

    /// <summary>
    /// The basic handle interface is a higher level interface that implements members that can be common to many handle types for various 3rd-party script
    /// implementations.  It's primary purpose is to support the DreamSpace.Net development framework, which can support various scripting engines, and is
    /// designed to be non-V8.NET specific.  Third-party scripts should implement this interface for their handles, or create and return value wrappers that
    /// implement this interface.
    /// </summary>
    public interface IBasicHandle : IV8Disposable, IConvertible
    {
        /// <summary>
        /// Returns the underlying value of this handle.
        /// If the handle represents an object, the the object OR a value represented by the object is returned.
        /// </summary>
        object Value { get; }

        /// <summary>
        /// Returns the underlying object associated with this handle.
        /// This exists because 'Value' my not return the underlying object, depending on implementation.
        /// </summary>
        object Object { get; }

        /// <summary>
        /// Returns true if this handle is associated with a CLR object.
        /// </summary>
        bool HasObject { get; }

        /// <summary>
        /// Returns true if this handle is empty (that is, equal to 'Handle.Empty'), and false if a valid handle exists.
        /// <para>An empty state is when a handle is set to 'Handle.Empty' and has no valid native V8 handle assigned.
        /// This is similar to "undefined"; however, this property will be true if a valid native V8 handle exists that is set to "undefined".</para>
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Returns true if this handle is undefined or empty (empty is when this handle is an instance of 'Handle.Empty').
        /// <para>"Undefined" does not mean "null".  A variable (handle) can be defined and set to "null".</para>
        /// </summary>
        bool IsUndefined { get; }

        /// <summary>
        /// Returns 'true' if this handle represents a 'null' value (that is, an explicitly defined 'null' value).
        /// This will return 'false' if 'IsEmpty' or 'IsUndefined' is true.
        /// </summary>
        bool IsNull { get; }

        /// <summary>
        /// The handle represents a Boolean value.
        /// </summary>
        bool IsBoolean { get; }

        /// <summary>
        /// The handle represents an Int32 value.
        /// </summary>
        bool IsInt32 { get; }

        /// <summary>
        /// The handle represents a number value.
        /// </summary>
        bool IsNumber { get; }

        /// <summary>
        /// The handle represents a string value.
        /// </summary>
        bool IsString { get; }

        /// <summary>
        /// The handle represents a *script* object.
        /// </summary>
        bool IsObject { get; }

        /// <summary>
        /// The handle represents a function/procedure/method value.
        /// </summary>
        bool IsFunction { get; }

        /// <summary>
        /// The handle represents a date value.
        /// </summary>
        bool IsDate { get; }

        /// <summary>
        /// The handle represents an array object.
        /// </summary>
        bool IsArray { get; }

        /// <summary>
        /// The handle represents a regular expression object.
        /// </summary>
        bool IsRegExp { get; }

        /// <summary>
        /// Returns true of the handle represents ANY *script* object type.
        /// </summary>
        bool IsObjectType { get; }

        /// <summary>
        /// Returns true of this handle represents an error.
        /// </summary>
        bool IsError { get; }

        /// <summary>
        /// Returns the 'Value' property type cast to the expected type.
        /// Warning: No conversion is made between different value types.
        /// </summary>
        DerivedType As<DerivedType>();

        /// Returns the 'LastValue' property type cast to the expected type.
        /// Warning: No conversion is made between different value types.
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
    }

    /// <summary>
    /// Represents a handle type for tracking native objects.
    /// </summary>
    public interface IHandle : IHandleBased
    {
        /// <summary>
        /// The ID of that native side proxy handle that this managed side handle represents.
        /// </summary>
        int ID { get; }

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
        InternalHandle Set(InternalHandle handle);

        /// <summary>
        /// Returns true if this handle is disposed (no longer in use).  Disposed native proxy handles are kept in a cache for performance reasons.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Returns true if this handle is empty (not associated with a native side handle).
        /// </summary>
        bool IsEmpty { get; }
    }

    /// <summary>
    /// Represents a type that uses or supports a handle.
    /// </summary>
    public interface IHandleBased
    {
        /// <summary>
        /// Returns the engine associated with this instance.
        /// </summary>
        V8Engine Engine { get; }

        /// <summary>
        /// Returns a handle value associated with this instance.
        /// </summary>
        InternalHandle InternalHandle { get; }

        /// <summary>
        /// Returns the object for this instance, or 'null' if not applicable/available.
        /// </summary>
        V8NativeObject Object { get; }
    }

    // ========================================================================================================================

    /// <summary>
    /// Represents methods that can be called on V8 objects (this includes handles).
    /// </summary>
    public interface IV8Object
    {
        /// <summary>
        /// Calls the V8 'Set()' function on the underlying native object.
        /// Returns true if successful.
        /// </summary>
        /// <param name="attributes">Flags that describe the property behavior.  They must be 'OR'd together as needed.</param>
        bool SetProperty(string name, InternalHandle value, V8PropertyAttributes attributes = V8PropertyAttributes.Undefined);

        /// <summary>
        /// Calls the V8 'Set()' function on the underlying native object.
        /// Returns true if successful.
        /// </summary>
        bool SetProperty(Int32 index, InternalHandle value);

        /// <summary>
        /// Sets a property to a given object. If the object is not V8.NET related, then the system will attempt to bind the instance and all public members to
        /// the specified property name.
        /// Returns true if successful.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="obj">Some value or object instance. 'Engine.CreateValue()' will be used to convert value types.</param>
        /// <param name="className">A custom in-script function name for the specified object type, or 'null' to use either the type name as is (the default) or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">For object instances, if true, then object reference members are included, otherwise only the object itself is bound and returned.
        /// For security reasons, public members that point to object instances will be ignored. This must be true to included those as well, effectively allowing
        /// in-script traversal of the object reference tree (so make sure this doesn't expose sensitive methods/properties/fields).</param>
        /// <param name="memberSecurity">For object instances, these are default flags that describe JavaScript properties for all object instance members that
        /// don't have any 'ScriptMember' attribute.  The flags should be 'OR'd together as needed.</param>
        bool SetProperty(string name, object obj, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null);

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
        bool SetProperty(Type type, V8PropertyAttributes propertyAttributes = V8PropertyAttributes.None, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null);

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the V8 'Get()' function on the underlying native object.
        /// If the property doesn't exist, the 'IsUndefined' property will be true.
        /// </summary>
        InternalHandle GetProperty(string name);

        /// <summary>
        /// Calls the V8 'Get()' function on the underlying native object.
        /// If the property doesn't exist, the 'IsUndefined' property will be true.
        /// </summary>
        InternalHandle GetProperty(Int32 index);

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the V8 'Delete()' function on the underlying native object.
        /// Returns true if the property was deleted.
        /// </summary>
        bool DeleteProperty(string name);

        /// <summary>
        /// Calls the V8 'Delete()' function on the underlying native object.
        /// Returns true if the property was deleted.
        /// </summary>
        bool DeleteProperty(Int32 index);

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls the V8 'SetAccessor()' function on the underlying native object to create a property that is controlled by "getter" and "setter" callbacks.
        /// </summary>
        void SetAccessor(string name,
            V8NativeObjectPropertyGetter getter, V8NativeObjectPropertySetter setter,
            V8PropertyAttributes attributes = V8PropertyAttributes.None, V8AccessControl access = V8AccessControl.Default);

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns a list of all property names for this object (including all objects in the prototype chain).
        /// </summary>
        string[] GetPropertyNames();

        /// <summary>
        /// Returns a list of all property names for this object (excluding the prototype chain).
        /// </summary>
        string[] GetOwnPropertyNames();

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Get the attribute flags for a property of this object.
        /// If a property doesn't exist, then 'V8PropertyAttributes.None' is returned
        /// (Note: only V8 returns 'None'. The value 'Undefined' has an internal proxy meaning for property interception).</para>
        /// </summary>
        V8PropertyAttributes GetPropertyAttributes(string name);

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Calls an object property with a given name on a specified object as a function and returns the result.
        /// The '_this' property is the "this" object within the function when called.
        /// If the function name is null or empty, then the current object is assumed to be a function object.
        /// </summary>
        InternalHandle Call(string functionName, InternalHandle _this, params InternalHandle[] args);

        /// <summary>
        /// Calls an object property with a given name on a specified object as a function and returns the result.
        /// If the function name is null or empty, then the current object is assumed to be a function object.
        /// </summary>
        InternalHandle StaticCall(string functionName, params InternalHandle[] args);

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    /// <summary>
    /// This is a class used internally to create a meta object for dynamic access to object properties for object based handles.
    /// </summary>
    public sealed class DynamicHandle : DynamicMetaObject
    {
        InternalHandle _Handle;
        V8Engine _Engine;

#if (V1_1 || V2 || V3 || V3_5)

        internal DynamicHandle(object value, Expression parameter)
            : base(parameter, BindingRestrictions.Empty, value)
        {
            if (value is IHandleBased) // TODO: Consider allowed direct access on the engine itself (make it dynamic as well ... ?).
            {
                _Handle = ((IHandleBased)value).InternalHandle;
                _Engine = ((IHandleBased)value).Engine;
            }
        }

#else

        internal DynamicHandle(object value, Expression parameter)
            : base(parameter, BindingRestrictions.GetTypeRestriction(parameter, value.GetType()), value)
        {
            if (value is IHandleBased) // TODO: Consider allowed direct access on the engine itself (make it dynamic as well ... ?).
            {
                _Handle = ((IHandleBased)value).InternalHandle;
                _Engine = ((IHandleBased)value).Engine;
            }
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            if (!_Handle.IsObjectType) throw new InvalidOperationException(InternalHandle._NOT_AN_OBJECT_ERRORMSG);

            Expression[] args = new Expression[1];
            MethodInfo methodInfo = ((Func<string, InternalHandle>)_Handle.GetProperty).Method;

            args[0] = Expression.Constant(binder.Name);

            Expression self = Expression.Convert(Expression, typeof(InternalHandle), ((Func<object, InternalHandle>)(obj => (obj is IHandleBased) ? ((IHandleBased)obj).InternalHandle : InternalHandle.Empty)).Method);

            Expression methodCall = Expression.Call(self, methodInfo, args);

            BindingRestrictions restrictions = Restrictions;

            Func<InternalHandle, object> handleWrapper = h => h.HasObject ? h.Object : (Handle)h; // (need to wrap the internal handle value with an object based handle in order to dispose of the value!)

            return new DynamicMetaObject(Expression.Convert(methodCall, typeof(object), handleWrapper.Method), restrictions);
        }

        public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
        {
            if (!_Handle.IsObjectType) throw new InvalidOperationException(InternalHandle._NOT_AN_OBJECT_ERRORMSG);

            var isHandleBase = typeof(IHandleBased).IsAssignableFrom(value.RuntimeType);

            Expression[] args = new Expression[isHandleBase ? 3 : 5];
            MethodInfo methodInfo;

            // ... create parameters for the call expression to set a value given a property name ...

            args[0] = Expression.Constant(binder.Name);

            if (isHandleBase) // (if the interface is implemented, then we need a converter to detect and pull out the handle value)
            {
                Func<object, InternalHandle> handleParamConversion
                    = obj => (obj is IHandleBased) ? ((IHandleBased)obj).InternalHandle
                        : _Engine != null ? _Engine.CreateValue(obj)
                        : InternalHandle.Empty;

                var convertParameter = Expression.Call(
                    Expression.Constant(handleParamConversion.Target),
                    handleParamConversion.Method,
                    Expression.Convert(value.Expression, typeof(object)));

                args[1] = convertParameter;
                args[2] = Expression.Constant(V8PropertyAttributes.None);

                methodInfo = ((Func<string, InternalHandle, V8PropertyAttributes, bool>)_Handle.SetProperty).Method;
            }
            else // (no interface is implemented, so default to just 'object')
            {
                args[1] = Expression.Convert(value.Expression, typeof(object));
                args[2] = Expression.Constant(null, typeof(string));
                args[3] = Expression.Constant(null, typeof(Nullable<bool>));
                args[4] = Expression.Constant(null, typeof(Nullable<ScriptMemberSecurity>));
                methodInfo = ((Func<string, object, string, bool?, ScriptMemberSecurity?, bool>)_Handle.SetProperty).Method;
            }

            Func<object, InternalHandle> conversionDelegate = _ConvertObjectToHandle;
            var self = Expression.Convert(Expression, typeof(InternalHandle), conversionDelegate.Method);

            var methodCall = Expression.Call(self, methodInfo, args);

            BindingRestrictions restrictions = Restrictions.Merge(value.Restrictions);

            return new DynamicMetaObject(Expression.Convert(methodCall, binder.ReturnType), restrictions);
        }

        static InternalHandle _ConvertObjectToHandle(object obj) => (obj as IHandleBased)?.InternalHandle ?? InternalHandle.Empty;

        public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
        {
            return base.BindInvoke(binder, args);
        }

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
            return base.BindInvokeMember(binder, args);
        }

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            return base.BindGetIndex(binder, indexes);
        }

        public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
        {
            return base.BindSetIndex(binder, indexes, value);
        }

        public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
        {
            return base.BindDeleteMember(binder);
        }

        public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes)
        {
            return base.BindDeleteIndex(binder, indexes);
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            if (!_Handle.IsObjectType) throw new InvalidOperationException(InternalHandle._NOT_AN_OBJECT_ERRORMSG);
            return _Handle.GetPropertyNames();
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            Expression convertExpression;

            if (LimitType.IsAssignableFrom(binder.Type))
            {
                convertExpression = Expression.Convert(Expression, binder.Type);
            }
            else if (typeof(V8NativeObject).IsAssignableFrom(binder.Type))
            {
                Func<object, V8NativeObject> getUnderlyingObjectMethod = obj => obj is IHandleBased ? ((IHandleBased)obj).Object : null;
                convertExpression = Expression.Convert(Expression.Convert(Expression, typeof(V8NativeObject), getUnderlyingObjectMethod.Method), binder.Type);
            }
            else
            {
                MethodInfo conversionMethodInfo;

                if (binder.Type == typeof(InternalHandle))
                    conversionMethodInfo =
                        ((Func<object, InternalHandle>)(obj => obj is IHandleBased ? ((IHandleBased)obj).InternalHandle : InternalHandle.Empty)).Method;
                else if (binder.Type == typeof(Handle))
                    conversionMethodInfo =
                        ((Func<object, Handle>)(obj => obj is IHandleBased ? ((IHandleBased)obj).InternalHandle.GetTrackerHandle() : Handle.Empty)).Method;
                else
                    conversionMethodInfo = ((Func<object, object>)(obj => Types.ChangeType(Value, binder.Type))).Method;

                convertExpression = Expression.Convert(Expression, binder.Type, conversionMethodInfo);
            }

            BindingRestrictions restrictions = Restrictions.Merge(BindingRestrictions.GetTypeRestriction(convertExpression, binder.Type));

            return new DynamicMetaObject(convertExpression, Restrictions);
        }

#endif
    }

    // ========================================================================================================================
}
