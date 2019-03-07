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
    /// This is a class used internally to create a meta object for dynamic access to object properties for object based handles.
    /// </summary>
    public sealed class DynamicHandle : DynamicMetaObject
    {
        // --------------------------------------------------------------------------------------------------------------------

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

        // --------------------------------------------------------------------------------------------------------------------

        internal DynamicHandle(object value, Expression parameter)
            : base(parameter, BindingRestrictions.GetTypeRestriction(parameter, value.GetType()), value)
        {
            if (value is IHandleBased) // TODO: Consider allowed direct access on the engine itself (make it dynamic as well ... ?).
            {
                _Handle = ((IHandleBased)value).InternalHandle;
                _Engine = ((IHandleBased)value).Engine;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        // (Gets the internal handle from an object reference if IHandleBased is implemented)
        static InternalHandle _GetInternalHandleFromObject(object obj) => (obj as IHandleBased)?.InternalHandle ?? InternalHandle.Empty;

        static Handle _GetHandleFromObject(object obj) => (obj as IHandleBased)?.InternalHandle.GetTrackerHandle() ?? Handle.Empty;

        // (Get an object from an internal handle [value type] if the handle represents a managed V8 object, otherwise just return the internal handle as an object)
        static object _GetObjectOrHandle(InternalHandle h) => h.HasObject ? h.Object : (Handle)h;

        static V8NativeObject _GetUnderlyingObject(object obj) => (obj as IHandleBased)?.Object;

        // --------------------------------------------------------------------------------------------------------------------

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            if (!_Handle.IsObjectType) throw new InvalidOperationException(InternalHandle._NOT_AN_OBJECT_ERRORMSG);

            Expression[] args = new Expression[1];
            MethodInfo methodInfo = ((Func<string, InternalHandle>)_Handle.GetProperty).Method;

            args[0] = Expression.Constant(binder.Name);

            Func<object, InternalHandle> conversionDelegate = _GetInternalHandleFromObject;

            Expression self = Expression.Convert(Expression, typeof(InternalHandle), conversionDelegate.Method);

            Expression methodCall = Expression.Call(self, methodInfo, args);

            BindingRestrictions restrictions = Restrictions;

            Func<InternalHandle, object> handleWrapper = _GetObjectOrHandle; // (need to wrap the internal handle value with an object based handle in order to dispose of the value!)

            return new DynamicMetaObject(Expression.Convert(methodCall, typeof(object), handleWrapper.Method), restrictions);
        }

        // --------------------------------------------------------------------------------------------------------------------

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

            Func<object, InternalHandle> conversionDelegate = _GetInternalHandleFromObject;
            var self = Expression.Convert(Expression, typeof(InternalHandle), conversionDelegate.Method);

            var methodCall = Expression.Call(self, methodInfo, args);

            BindingRestrictions restrictions = Restrictions.Merge(value.Restrictions);

            return new DynamicMetaObject(Expression.Convert(methodCall, binder.ReturnType), restrictions);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
        {
            return base.BindInvoke(binder, args);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
            return base.BindInvokeMember(binder, args);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            return base.BindGetIndex(binder, indexes);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
        {
            return base.BindSetIndex(binder, indexes, value);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
        {
            return base.BindDeleteMember(binder);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes)
        {
            return base.BindDeleteIndex(binder, indexes);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            if (!_Handle.IsObjectType) throw new InvalidOperationException(InternalHandle._NOT_AN_OBJECT_ERRORMSG);
            return _Handle.GetPropertyNames();
        }

        // --------------------------------------------------------------------------------------------------------------------


        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            Expression convertExpression;

            if (LimitType.IsAssignableFrom(binder.Type))
            {
                convertExpression = Expression.Convert(Expression, binder.Type);
            }
            else if (typeof(V8NativeObject).IsAssignableFrom(binder.Type))
            {
                Func<object, V8NativeObject> getUnderlyingObject = _GetUnderlyingObject;

                convertExpression = Expression.Convert(Expression.Convert(Expression, typeof(V8NativeObject), getUnderlyingObject.Method), binder.Type);
            }
            else
            {
                MethodInfo conversionMethodInfo;

                if (binder.Type == typeof(InternalHandle))
                    conversionMethodInfo = ((Func<object, InternalHandle>)_GetInternalHandleFromObject).Method;
                else if (binder.Type == typeof(Handle))
                    conversionMethodInfo = ((Func<object, Handle>)_GetHandleFromObject).Method;
                else
                    conversionMethodInfo = ((Func<object, object>)(obj => Types.ChangeType(Value, binder.Type))).Method;

                convertExpression = Expression.Convert(Expression, binder.Type, conversionMethodInfo);
            }

            BindingRestrictions restrictions = Restrictions.Merge(BindingRestrictions.GetTypeRestriction(convertExpression, binder.Type));

            return new DynamicMetaObject(convertExpression, Restrictions);
        }

        // --------------------------------------------------------------------------------------------------------------------

#endif
    }

    // ========================================================================================================================
}
