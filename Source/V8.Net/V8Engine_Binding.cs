using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;

#if !(V1_1 || V2 || V3 || V3_5)
using System.Dynamic;
#endif

namespace V8.Net
{
    // ========================================================================================================================

    public enum BindingMode
    {
        /// <summary>
        /// The V8NativeObject is not a binding object.
        /// </summary>
        None,

        /// <summary>
        /// The V8NativeObject is a binding object for instances (i.e. ObjectBinder).
        /// </summary>
        Instance,

        /// <summary>
        /// The V8NativeObject is not a binding object for types  (i.e. V8Function).
        /// </summary>
        Static
    }

    // ========================================================================================================================

    /// <summary>
    /// Represent a TypeBinder JavaScript function (for static properties, and creating new objects in script).
    /// </summary>
    public class TypeBinderFunction : V8Function // TODO: Investigate if DLR expressions can help in any of this.
    {
        public TypeBinder TypeBinder { get; internal set; }
    }

    // ========================================================================================================================

    /// <summary>
    /// Keeps track of object references based on an array of one or more related types. The reference is stored with the last type entry.
    /// The object references are stored based on a tree of nested types for fast dictionary-tree-style lookup.
    /// Currently, this class is used in the type binder to cache references to MemberInfo objects based on generic type parameters.
    /// </summary>
#if !(V1_1 || V2 || V3 || V3_5)
    [DebuggerDisplay("(Object: {Object}, SubTypes: {SubTypes.Count})")]
#endif
    public class TypeLibrary<T> where T : class
    {
        public readonly Dictionary<Type, TypeLibrary<T>> SubTypes = new Dictionary<Type, TypeLibrary<T>>();
        public T Object;

        public T Set(T value, params Type[] types) { return Set(types, value); }
        public T Set(Type[] types, T value)
        {
            if (types == null) types = new Type[0];
            return _Set(types, value);
        }
        T _Set(Type[] types, T value, int depth = 0)
        {
            if (depth >= types.Length) return (Object = value);
            Type type = types[depth];
            if (!SubTypes.TryGetValue(type, out TypeLibrary<T> _typeLibrary))
            {
                // ... this sub type doesn't exist yet, so add it ...
                _typeLibrary = new TypeLibrary<T>();
                SubTypes[type] = _typeLibrary;
            }
            return _typeLibrary._Set(types, value, depth + 1);
        }

        /// <summary> Gets the object of type <typeparamref name="T"/> from the type tree based on given types. </summary>
        /// <param name="strict">
        ///     True to find exact type matches. If false is past in, and a type can't be found explicitly, then the best match is
        ///     found based on assignability.
        /// </param>
        /// <param name="types"> A variable-length parameters list containing types. </param>
        /// <returns> Returns the type 'T' instance. </returns>
        public T Get(bool strict, params Type[] types)
        {
            if (types == null) types = new Type[0];
            return _Get(types, strict);
        }
        T _Get(Type[] types, bool strict, int depth = 0)
        {
            if (depth >= types.Length) return Object;
            Type type = types[depth];
            if (!SubTypes.TryGetValue(type, out TypeLibrary<T> _typeLibrary))
                if (strict)
                    return null; // (nothing found)
                else
                    foreach (var item in SubTypes)
                        if (item.Key.IsAssignableFrom(type))
                        {
                            _typeLibrary = item.Value;
                            break;
                        }
            return _typeLibrary?._Get(types, strict, depth + 1);
        }

        /// <summary> Determine if an object exists using the given types. </summary>
        /// <param name="strict">
        ///     True to find exact type matches. If false is past in, and a type can't be found explicitly, then the best match is
        ///     found based on assignability.
        /// </param>
        /// <param name="types"> A variable-length parameters list containing types. </param>
        /// <returns> True if it succeeds, false if it fails. </returns>
        public bool Exists(bool strict, params Type[] types)
        {
            return Get(strict, types) == null;
        }

        public IEnumerable<TypeLibrary<T>> Items { get { return _GetItems(); } }

        IEnumerable<TypeLibrary<T>> _GetItems()
        {
            if (Object != null) yield return this;
            foreach (var kv in SubTypes)
                foreach (var item in kv.Value._GetItems())
                    yield return item;
        }
    }

    // ========================================================================================================================

    /// <summary>
    /// Wraps a script value with strong CLR type information for use with generics and method invocation.
    /// <para>
    /// This struct represents an argument passed from script to V8.NET binding logic. If the argument represents type information, it is extracted.
    /// In either case, 'Value' will be the requested strong-typed value, or the default value, whichever is detected first (in that order).
    /// This can be used to pass arguments to methods, where the value is converted to a specific type if necessary.
    /// </para>
    /// <para>Warning: The struct only extracts information, converting the script argument if necessary, and does not own the 'ArgInfoSource' handle.
    /// As such, the caller is still responsible to release it.</para>
    /// </summary>
    public unsafe struct ArgInfo
    {
        public readonly InternalHandle ArgInfoSource; // (note: will not be released by this struct [nor can it be])

        public readonly Type Type;
        public readonly Int32 TypeID;

        public readonly object Value;
        public readonly Type OriginalValueType;

        /// <summary>
        /// Returns true if this ArgInfo value has valid type information.  This will be false for empty instances.
        /// </summary>
        public bool IsValid { get { return Type != null; } }

        /// <summary>
        /// Returns true if a valid value exists.  If false is returned, this usually means this is a type-only ArgInfo object.
        /// </summary>
        public readonly bool HasValue;

        /// <summary>
        /// Returns true if the information was taken from a native ArgInfo object.
        /// </summary>
        public bool IsSourceFromArgInfoObject { get { return ArgInfoSource.CLRTypeID >= 0; } }

        public object As(Type newtype) { return Types.ChangeType(Value, newtype); }

        public Exception Error; // (only used if part of a list of arguments)
        public bool HasError { get { return Error != null; } }

        public ParameterInfo ExpectedParameter;
        public Type ExpectedType;
        public bool HasDefaultValue { get { return ExpectedParameter != null && ExpectedParameter.DefaultValue != DBNull.Value; } }
        public object DefaultValue { get { return HasDefaultValue ? ExpectedParameter.DefaultValue : null; } }

        /// <summary>
        /// Returns either the underlying argument value (in converted form), or the default value, whichever is detected first (in that order).
        /// This can be used to pass arguments to methods, where a specific CLR type is required.
        /// </summary>
        public object ValueOrDefault
        {
            get
            {
                if (Error != null) throw Error; // (error was not dealt with yet!)
                if (!HasValue && HasDefaultValue) return DefaultValue;
                return Value;
            }
        }

        public ArgInfo(InternalHandle handle, ParameterInfo paramInfo = null, Type expectedType = null)
        {
            ArgInfoSource = handle;
            ExpectedParameter = paramInfo;
            ExpectedType = expectedType ?? (paramInfo != null ? paramInfo.ParameterType : null);
            Type = null;
            TypeID = -1;
            Value = null;
            Error = null;

            if (handle.CLRTypeID >= 0) // (must be an object type with ID <= -2)
            {
                TypeID = handle.CLRTypeID;

                using (var hValue = handle.GetProperty("$__Value"))
                {
                    HasValue = !hValue.IsUndefined;
                    Value = HasValue ? hValue.Value : null;
                }

                // (type is set last, as it is used as the flag to determine if the info is valid)
                Type = TypeID >= 0 ? handle.Engine._RegisteredTypes[TypeID] : null; // (this will return 'null' if the index is invalid)
            }
            else
            {
                HasValue = !ArgInfoSource.IsUndefined;
                Value = HasValue ? ArgInfoSource.Value : null;

                if (ArgInfoSource.IsBinder) // (type binders are supported for generic method parameters and types [so no need to invoke them as functions to get a strong type!])
                    Type = ArgInfoSource.TypeBinder.BoundType;
            }

            OriginalValueType = Value != null ? Value.GetType() : typeof(object);

            if (Type == null) Type = OriginalValueType;

            // ... step 1: convert the script value to the strong type if necessary ...
            // (reason: "Type" represents the type explicitly requested on the script side, so that needs to be established first [consider this conversion path: Number->String->Object {boxed double vs string object}])
            // (one exception: if the expected type is a V8.NET handle, then pass it directly)

            if (ExpectedType == typeof(InternalHandle))
            {
                Value = ArgInfoSource;
                Type = typeof(InternalHandle);
            }
            else if (ExpectedType == typeof(Handle))
            {
                Value = new Handle(ArgInfoSource);
                Type = typeof(Handle);
            }
            else if (ExpectedType == typeof(V8Engine))
            {
                Value = ArgInfoSource.Engine;
                Type = typeof(V8Engine);
            }
            else
            {
                if (!Type.IsAssignableFrom(OriginalValueType))
                    try { Value = Types.ChangeType(Value, Type); }
                    catch (Exception ex) { Error = ex; }

                // ... step2: convert the strong value to the expected type (if given, and if necessary) ...
                // (note: if 'IsGenericParameter' is true, then this type represents a type ONLY, and any value is ignored)

                if (Error == null && ExpectedType != null && !ExpectedType.IsGenericParameter && (!ExpectedType.IsGenericType || ExpectedType.IsConstructedGenericType()))
                    if (ExpectedType.IsAssignableFrom(Type))
                        Type = ExpectedType; // (this sets the explicit type expected)
                    else
                        try
                        {
                            Value = Types.ChangeType(Value, ExpectedType);
                            Type = ExpectedType; // (this sets the explicit type expected)
                        }
                        catch (Exception ex) { Error = ex; }
            }
        }

        /// <summary>
        /// Returns an array of ArgInfo values for the given handles.
        /// </summary>
        public static ArgInfo[] GetArguments(InternalHandle[] handles, uint handlesOffset = 0, ParameterInfo[] expectedParameters = null)
        {
            var handlesLength = handles.Length - handlesOffset;
            var length = expectedParameters != null ? expectedParameters.Length : handlesLength;

            ArgInfo[] argInfoItems = new ArgInfo[length];

            for (var i = 0; i < length; i++)
                argInfoItems[i] = new ArgInfo(i < handlesLength ? handles[handlesOffset + i] : InternalHandle.Empty, expectedParameters != null ? expectedParameters[i] : null);

            return argInfoItems;
        }

        /// <summary>
        /// Returns an array of ArgInfo values for the expected types.
        /// </summary>
        public static ArgInfo[] GetTypes(InternalHandle[] handles, uint handlesOffset = 0, Type[] expectedTypes = null)
        {
            var handlesLength = handles.Length - handlesOffset;
            var length = expectedTypes != null ? expectedTypes.Length : handlesLength;

            ArgInfo[] argInfoItems = new ArgInfo[length];

            for (var i = 0; i < length; i++)
                argInfoItems[i] = new ArgInfo(i < handlesLength ? handles[handlesOffset + i] : InternalHandle.Empty, null, expectedTypes != null ? expectedTypes[i] : null);

            return argInfoItems;
        }

        /// <summary>
        /// Returns an array of ArgInfo values for the expected types.
        /// </summary>
        public static Type[] GetSystemTypes(IEnumerable<ArgInfo> argInfoList)
        {
            int count = argInfoList.Count(), i = 0;
            var types = new Type[count];
            var enumerator = argInfoList.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.HasError) throw enumerator.Current.Error;
                types[i++] = enumerator.Current.Type;
            }
            return types;
        }

        /// <summary>
        /// Extracts and returns an array of all handles from the specified arguments.
        /// </summary>
        /// <param name="argInfoArgs"></param>
        /// <returns></returns>
        public static InternalHandle[] GetHandles(ArgInfo[] argInfoArgs)
        {
            InternalHandle[] handles = new InternalHandle[argInfoArgs.Length];
            for (int i = 0; i < argInfoArgs.Length; i++)
                handles[i] = argInfoArgs[i].ArgInfoSource;
            return handles;
        }
    }

    // ========================================================================================================================

    /// <summary>
    /// A V8.NET binder for CLR types.
    /// </summary>
    public sealed unsafe class TypeBinder
    {
        // --------------------------------------------------------------------------------------------------------------------

        public static string TYPE_BINDER_MISSING_MSG = "The ObjectBinder is missing for {0} '{1}' ({2}).";

        /// <summary>
        /// The engine that will own the 'ObjectTemplate' instance.
        /// </summary>
        public readonly V8Engine Engine;

        /// <summary>
        /// A reference to the type binder for the immediate base type inherited by the bound type.
        /// </summary>
        public readonly TypeBinder BaseTypeBinder;

        /// <summary>
        /// Represents a V8 template object used for generating native V8 objects which will correspond to the binding for 
        /// instances of the underlying type.
        /// </summary>
        public readonly ObjectTemplate InstanceTemplate;

        /// <summary>
        /// Represents a V8 template object used for generating native V8 function objects which will correspond to the binding 
        /// for the underlying type (for creating new instances within script).
        /// </summary>
        public readonly FunctionTemplate TypeTemplate;

        /// <summary>
        /// The function used to represent this bound type in script.
        /// </summary>
        public TypeBinderFunction TypeFunction { get; private set; }

        /// <summary>
        /// The type represented by this type binder.
        /// </summary>
        public readonly Type BoundType; // (type of object this binding represents)

        /// <summary>
        /// A unique internal ID used to quickly identify the type for best performance.
        /// </summary>
        public Int32 TypeID { get; private set; }

        /// <summary>
        /// The name that will be displayed when invoking 'Object.valueOf()' in JavaScript on function objects which represent this type.
        /// This is also the name that will be used for the property created from this type binder.
        /// </summary>
        public readonly string ClassName;

        /// <summary>
        /// If true, then nested object references are included, otherwise they are ignored.  By default, the references are ignored for security reasons.
        /// <param>When an object is bound, only the object instance itself is bound (and not any reference members).</param>
        /// </summary>
        public bool Recursive { get { return _Recursive ?? (BaseTypeBinder != null ? BaseTypeBinder.Recursive : false); } }
        internal bool? _Recursive;

        /// <summary>
        /// Default member attributes for members that don't have the 'ScriptMember' attribute.
        /// </summary>
        public ScriptMemberSecurity DefaultMemberSecurity { get { return _DefaultMemberSecurity ?? (BaseTypeBinder != null ? BaseTypeBinder.DefaultMemberSecurity : Engine.DefaultMemberBindingSecurity); } }
        internal ScriptMemberSecurity? _DefaultMemberSecurity;

        /// <summary>
        /// The indexer for this type, if applicable, otherwise this is null.
        /// </summary>
        public PropertyInfo Indexer { get; private set; }

        /// <summary>
        /// The ScriptObject attribute if one exists for the underlying type, otherwise this is null.
        /// </summary>
        public ScriptObject ScriptObjectAttribute { get { return _ScriptObjectAttribute; } }
        ScriptObject _ScriptObjectAttribute;

        /// <summary>
        /// Holds details for each member on a type when binding objects.
        /// </summary>
#if !(V1_1 || V2 || V3 || V3_5)
        [DebuggerDisplay("{MemberName}x{TotalImmediateMembers}")]
#endif
        internal class _MemberDetails
        {
            public readonly TypeBinder TypeBinder;
            public _MemberDetails(TypeBinder owner) { TypeBinder = owner; }

            public _MemberDetails BaseDetails; // (if set, this is these are the inherited members of the same name represented by this member).

            public MemberInfo FirstMember; // (this is a quick cached reference to the first member in 'Members', which is faster for fields and properties)
            public readonly TypeLibrary<MemberInfo> ImmediateMembers = new TypeLibrary<MemberInfo>(); // (if the count is > 1, then this member detail instance represents a method or property (indexer) overload)
            public uint TotalImmediateMembers = 1; // (if > 1 then this member is overloaded)

            /// <summary>
            /// Enumerates all the type libraries for this member detail and any base member details.
            /// </summary>
            public IEnumerable<TypeLibrary<MemberInfo>> MemberTypeLibraries
            {
                get
                {
                    yield return ImmediateMembers;
                    if (BaseDetails != null)
                        foreach (var tl in BaseDetails.MemberTypeLibraries)
                            yield return tl;
                }
            }

            /// <summary>
            /// Enumerates all the type libraries for this member detail and any base member details,
            /// and returns each underlying MemberInfo reference.
            /// </summary>
            public IEnumerable<MemberInfo> Members
            {
                get
                {
                    foreach (var mi in MemberTypeLibraries.SelectMany(mtl => mtl.Items.Select(i => i.Object)))
                        yield return mi;
                }
            }
            /// <summary>
            /// Enumerates all the type libraries for this member detail and any base member details,
            /// and returns each underlying MethodInfo reference.
            /// </summary>
            public IEnumerable<MethodInfo> MethodMembers { get { return from m in Members where m.MemberType == MemberTypes.Method select (MethodInfo)m; } }

            /// <summary>
            ///     Given a list of types, returns the matching MemberInfo reference, or 'null' if not found. This is used mainly with
            ///     member details instances representing overloaded method members.
            /// </summary>
            /// <param name="strict">
            ///     True to find exact type matches. If false is past in, and a type can't be found explicitly, then the best match is
            ///     found based on assignability.
            /// </param>
            /// <param name="types"> . </param>
            /// <returns> The found member by types. </returns>
            public MemberInfo FindMemberByTypes(bool strict, params Type[] types)
            {
                MemberInfo mi;
                foreach (var mtl in MemberTypeLibraries)
                {
                    mi = mtl.Get(strict, types);
                    if (mi != null) return mi;
                }
                return null;
            }

            public TypeLibrary<TypeLibrary<MemberInfo>> ConstructedMemberGroups; // (if this member is a generic type, then this is the cache of constructed generic definitions)
            public uint TotalConstructedMembers = 0; // (if > 1 then this member is overloaded)

            public string MemberName; // (might be different from MemberInfo!)
            public MemberTypes MemberType; // (might be different from MemberInfo!)
            public ScriptMemberSecurity? MemberSecurity;
            public ScriptMemberSecurity InheritedMemberSecurity { get { return MemberSecurity != null ? MemberSecurity.Value : BaseDetails != null ? BaseDetails.InheritedMemberSecurity : ScriptMemberSecurity.NoAcccess; } }
            public bool HasSecurityFlags(ScriptMemberSecurity memberSecurity) { var ims = InheritedMemberSecurity; return ims >= 0 && memberSecurity >= 0 ? ims.HasFlag(memberSecurity) : ims == memberSecurity; } // (any negative values must be checked with direct equality)
            public BindingMode BindingMode;
            public NativeGetterAccessor Getter;
            public NativeSetterAccessor Setter;
            public V8Function Method;
            //??public TypeLibrary<V8Function> Methods; // (overloads ['SingleMethod' should be null])
            public InternalHandle ValueOverride; // (the value override is a user value that, if exists, overrides the bindings)

            public bool Accessible // (returns true if this member is allowed to be accessed)
            {
                get
                {
                    bool recursive = TypeBinder.Recursive;
                    if (!recursive)
                    {
                        if (MemberType == MemberTypes.Field && ((FieldInfo)FirstMember).FieldType.IsClass && ((FieldInfo)FirstMember).FieldType != typeof(string)) return false; // (don't include nested objects, except strings)
                        if (MemberType == MemberTypes.Property && ((PropertyInfo)FirstMember).PropertyType.IsClass && ((PropertyInfo)FirstMember).PropertyType != typeof(string)) return false; // (don't include nested objects, except strings)
                    }
                    return !HasSecurityFlags(ScriptMemberSecurity.NoAcccess);
                }
            }
        }

        _MemberDetails _Constructors;

        internal readonly Dictionary<string, _MemberDetails> _Members = new Dictionary<string, _MemberDetails>();
        public IEnumerable<MemberInfo> Members { get { return from m in _Members.Values from mi in m.Members select mi; } }

        IEnumerable<_MemberDetails> _FieldDetails(BindingMode bindingMode)
        { return from kv in _Members where (bindingMode == BindingMode.None || kv.Value.BindingMode == bindingMode) && kv.Value.MemberType == MemberTypes.Field select kv.Value; }

        IEnumerable<_MemberDetails> _PropertyDetails(BindingMode bindingMode)
        { return from kv in _Members where (bindingMode == BindingMode.None || kv.Value.BindingMode == bindingMode) && kv.Value.MemberType == MemberTypes.Property select kv.Value; }

        IEnumerable<_MemberDetails> _MethodDetails(BindingMode bindingMode)
        { return from kv in _Members where (bindingMode == BindingMode.None || kv.Value.BindingMode == bindingMode) && kv.Value.MemberType == MemberTypes.Method select kv.Value; }

        /// <summary>
        /// Enumerates all type binders in the inheritance hierarchy.
        /// </summary>
        public IEnumerable<TypeBinder> BaseBinders { get { var b = BaseTypeBinder; while (b != null) { yield return b; b = b.BaseTypeBinder; } } }

        // --------------------------------------------------------------------------------------------------------------------

        //? internal List<Delegate> _Delegates; // (delegates to prevent getting garbage collected; this is not null only when a delegate is added)

        ///// <summary> Prevents delegates associated with this . </summary>
        //? internal void _AssociatedDelegate(Delegate d)
        //{
        //    if (_Delegates == null)
        //        _Delegates = new List<Delegate>();
        //    _Delegates.Add(d);
        //}

        // --------------------------------------------------------------------------------------------------------------------

        internal TypeBinder(V8Engine engine, Type type, string className = null, bool? recursive = null, ScriptMemberSecurity? defaultMemberSecurity = null)
        {

            if (engine == null) throw new ArgumentNullException("engine");
            if (type == null) throw new ArgumentNullException("type");

            Engine = engine;
            BoundType = type;
            TypeID = Engine._RegisteredTypes.Add(type); // (this is done here to make sure the type is created and accessible to prevent cyclical calls)
            _Recursive = recursive;
            _DefaultMemberSecurity = defaultMemberSecurity;
            _ScriptObjectAttribute = (from a in type.GetCustomAttributes(true) where a is ScriptObject select (ScriptObject)a).FirstOrDefault();

            if (_DefaultMemberSecurity == null && _ScriptObjectAttribute != null)
                _DefaultMemberSecurity = _ScriptObjectAttribute.Security;

            if (className.IsNullOrWhiteSpace())
            {
                if (_ScriptObjectAttribute != null)
                    ClassName = _ScriptObjectAttribute.TypeName;

                if (string.IsNullOrEmpty(ClassName))
                {
                    ClassName = BoundType.Name;

                    if (BoundType.IsGenericType)
                    {
                        ClassName = ClassName.Substring(0, ClassName.LastIndexOf('`')) + "$" + BoundType.GetGenericArguments().Count();
                    }
                    else
                        if (ClassName == "Object" || ClassName == "Function" || ClassName == "Boolean" || ClassName == "String" || ClassName == "RegExp" || ClassName == "Number" || ClassName == "Math" || ClassName == "Array" || ClassName == "Date")
                        ClassName = "CLR" + ClassName;
                }
            }
            else ClassName = className;

            Engine._Binders[type] = this;

            // ... before pulling the members, make sure to profile the base types first ...

            if (type.BaseType != null)
                BaseTypeBinder = Engine.RegisterType(type.BaseType, null, recursive, defaultMemberSecurity); // (note: this is recursive if there are multiple unbound base types)
            // (note: parameters are taken directly from the arguments given, and not from this instance's fields)

            // ... setup the templates needed ...

            //? InstanceTemplate = Engine.CreateObjectTemplate<ObjectTemplate>(false);
            //? InstanceTemplate.RegisterNamedPropertyInterceptors(); // (only the named interceptors for named members)

            TypeTemplate = Engine.CreateFunctionTemplate<FunctionTemplate>(ClassName);

            InstanceTemplate = TypeTemplate.InstanceTemplate;
            InstanceTemplate.RegisterNamedPropertyInterceptors(); // (only the named interceptors for named members are needed at this point)

            // ... extract the members and apply to the templates ...

            _BindInstanceMembers();
            // (note: the instance member reflection includes static members during the process, which is why 
            // '_BindTypeMembers()' must be called AFTER, since it relies on the'_Members' being properly mapped out) 

            _BindTypeMembers();
        }

        // --------------------------------------------------------------------------------------------------------------------

        void _BindInstanceMembers()
        {
            if (BoundType == null) throw new InvalidOperationException("'BoundType' is null.");

            var members = BoundType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); //| BindingFlags.FlattenHierarchy

            int mi;
            string memberName;
            ScriptMemberSecurity? memberSecurity;
            ScriptMember scriptMemberAttrib;
            _MemberDetails memberDetails;

            for (mi = 0; mi < members.Length; mi++)
            {
                var member = members[mi]; // (need to use 'var' for the lambda closures in 'SetAccessor()' below)

                if (member.DeclaringType != BoundType)
                {
                    // ... this member does not belong to this type binder ...
                    continue;
                }

                memberName = member.Name;
                memberSecurity = DefaultMemberSecurity;

                scriptMemberAttrib = (from a in member.GetCustomAttributes(true) where a is ScriptMember select (ScriptMember)a).LastOrDefault();

                if (scriptMemberAttrib != null)
                {
                    memberSecurity = scriptMemberAttrib.Security;

                    if (!scriptMemberAttrib.InScriptName.IsNullOrWhiteSpace())
                        memberName = scriptMemberAttrib.InScriptName;
                }

                memberDetails = _CreateMemberDetails(memberName, memberSecurity, member,
                    s => _Members.GetValueOrDefault(s),
                    md => _Members[md.MemberName] = md);
            }

            // ... resolve the members that belong to the base types ...
            // (note: this effectively simulates the flattening of all inherited base type members)

            foreach (var tb in BaseBinders) // (this will return the inherited base types in order from the immediate base upwards, which is really important when linking '_MemberDetails' instances)
            {
                // ... go through all the INSTANCE types in the declaring type binder and add them ...

                var baseInstanceMembers = from md in tb._Members.Values where md.BindingMode == BindingMode.Instance select md;

                foreach (var baseInstanceMemberDetails in baseInstanceMembers.Where(md => md.BindingMode == BindingMode.Instance))
                {
                    var md = _Members.GetValueOrDefault(baseInstanceMemberDetails.MemberName); // (find a local member by the same name as this base member name, if any)
                    if (md == null)
                        _Members[baseInstanceMemberDetails.MemberName] = baseInstanceMemberDetails; // (adopt the base member into this local member list for quick reference [i.e. all base names will exist in this subtype])
                    else
                        if (md.BaseDetails == null && md.TypeBinder == this)
                        md.BaseDetails = baseInstanceMemberDetails; // (the iteration goes up the inheritance chain, so once 'BaseDetails' is set, it is ignored [because the base type binders would have already linked the details])
                }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------
        // Note: The following method use to be used in more than one place, and is now used in only one currently.

        /// <summary>
        /// Used to help update a '_MemberDetails' dictionary with the supplied member information via supplied callbacks.
        /// The members are tracked by name (via the 'getExisting()' callback), and as such, only a single '_MemberDetails'
        /// instance should exist per name.  Other members are added as overloads to the instance returned from 'getExisting()',
        /// if any.
        /// </summary>
        /// <param name="memberName">The name of the member to create the details for.</param>
        /// <param name="memberSecurity">The member security to apply.</param>
        /// <param name="memberInfo">The type's member information details.</param>
        /// <param name="getExisting">A callback to check if there's an existing member by the same name.</param>
        /// <param name="set">A callback for when no member exists and needs to be added (i.e. no existing member details were updated).</param>
        /// <returns>The resulting '_MemberDetails' instance, which may be an already existing one.</returns>
        internal _MemberDetails _CreateMemberDetails(string memberName, ScriptMemberSecurity? memberSecurity, MemberInfo memberInfo,
            Func<string, _MemberDetails> getExisting, Action<_MemberDetails> set)
        {
            if (memberName.IsNullOrWhiteSpace())
                memberName = memberInfo.Name;

            _MemberDetails memberDetails = null;

            if (memberInfo.MemberType == MemberTypes.Field)
            {
                var fieldInfo = memberInfo as FieldInfo;

                memberDetails = new _MemberDetails(this)
                {
                    FirstMember = fieldInfo,
                    MemberName = memberName,
                    MemberType = MemberTypes.Field,
                    MemberSecurity = memberSecurity,
                    BindingMode = fieldInfo.IsStatic ? BindingMode.Static : BindingMode.Instance
                };

                memberDetails.ImmediateMembers.Set(fieldInfo, fieldInfo.FieldType);

                set(memberDetails);
            }
            else if (memberInfo.MemberType == MemberTypes.Property)
            {
                var propertyInfo = memberInfo as PropertyInfo;

                if (propertyInfo.GetIndexParameters().Count() > 0) // (this property is an indexer ([#]), so skip - this will be supported another way)
                {
                    Indexer = propertyInfo;
                    if (!InstanceTemplate.IndexedPropertyInterceptorsRegistered)
                        InstanceTemplate.RegisterIndexedPropertyInterceptors();
                    return null;
                }

                var getMethod = propertyInfo.GetGetMethod();
                var setMethod = propertyInfo.GetSetMethod();
                var isStatic = getMethod != null && getMethod.IsStatic || setMethod != null && setMethod.IsStatic;

                memberDetails = new _MemberDetails(this)
                {
                    FirstMember = propertyInfo,
                    MemberName = memberName,
                    MemberType = MemberTypes.Property,
                    MemberSecurity = memberSecurity,
                    BindingMode = isStatic ? BindingMode.Static : BindingMode.Instance
                };

                memberDetails.ImmediateMembers.Set(propertyInfo, propertyInfo.PropertyType);

                set(memberDetails);
            }
            else if (memberInfo.MemberType == MemberTypes.Method)
            {
                var methodInfo = memberInfo as MethodInfo;

                if (!methodInfo.IsSpecialName)
                {
                    if (methodInfo.IsGenericMethodDefinition)
                        memberName = memberName + "$" + methodInfo.GetGenericArguments().Count();

                    var existingMemberDetails = getExisting != null ? getExisting(memberName) : null;

                    memberDetails = existingMemberDetails ?? new _MemberDetails(this)
                    {
                        FirstMember = methodInfo,
                        MemberName = memberName,
                        MemberType = MemberTypes.Method,
                        BindingMode = methodInfo.IsStatic ? BindingMode.Static : BindingMode.Instance
                    };

                    if (memberSecurity >= 0 && memberDetails.MemberSecurity >= 0)
                        memberDetails.MemberSecurity |= memberSecurity; // (combine all security attributes for all overloaded members, if any)
                    else
                        memberDetails.MemberSecurity = memberSecurity;

                    // ... register the method based on number and type of expected parameters ...

                    var types = Arrays.Concat(methodInfo.GetGenericArguments(), methodInfo.GetParameters().Select(p => p.ParameterType).ToArray());

                    memberDetails.ImmediateMembers.Set(methodInfo, types);

                    if (existingMemberDetails == null)
                        set(memberDetails);
                    else
                        memberDetails.TotalImmediateMembers++;
                }
            }
            else if (memberInfo.MemberType == MemberTypes.Constructor)
            {
                var constructorInfo = memberInfo as ConstructorInfo;

                if (_Constructors != null)
                {
                    memberDetails = _Constructors;
                    memberDetails.TotalImmediateMembers++;
                }
                else
                    memberDetails = _Constructors = new _MemberDetails(this)
                    {
                        FirstMember = constructorInfo,
                        MemberName = memberName,
                        MemberType = MemberTypes.Method,
                        BindingMode = constructorInfo.IsStatic ? BindingMode.Static : BindingMode.Instance
                    };

                if (memberSecurity >= 0 && memberDetails.MemberSecurity >= 0)
                    memberDetails.MemberSecurity |= memberSecurity; // (combine all security attributes for all overloaded members, if any)
                else
                    memberDetails.MemberSecurity = memberSecurity;

                // ... register the method based on number and type of expected parameters ...

                var types = constructorInfo.GetParameters().Select(p => p.ParameterType).ToArray();

                memberDetails.ImmediateMembers.Set(constructorInfo, types);
            }

            return memberDetails;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Binds a getter and setter to read and/or write to the specified data member (field or property only).
        /// </summary>
        /// <param name="memberName">The name of a member on '{ObjectBinder}.Object', or a new in-script name if 'fieldInfo' is supplied.</param>
        /// <param name="getter">Returns the getter delegate to use for a native callback.</param>
        /// <param name="setter">Returns the setter delegate to use for a native callback.</param>
        /// <param name="fieldInfo">If null, this will be pulled using 'memberName'.  If specified, then 'memberName' can be used to rename the field name.</param>
        /// <returns>An exception on error, or null on success.</returns>
        internal bool _GetBindingForDataMember(_MemberDetails memberDetails, out NativeGetterAccessor getter, out NativeSetterAccessor setter)
        {
            if (memberDetails.MemberType == MemberTypes.Field)
                return _GetBindingForField(memberDetails, out getter, out setter);
            else if (memberDetails.MemberType == MemberTypes.Property)
                return _GetBindingForProperty(memberDetails, out getter, out setter);
            else
            {
                getter = null;
                setter = null;
                return false;
            }
        }

        internal bool _GetBindingForField(_MemberDetails memberDetails, out NativeGetterAccessor getter, out NativeSetterAccessor setter)
        {
            string memberName = memberDetails.MemberName;
            FieldInfo fieldInfo = (FieldInfo)memberDetails.FirstMember;
            getter = null;
            setter = null;

            if (TypeTemplate == null) throw new ArgumentNullException("'TypeTemplate' is null.");

            if (string.IsNullOrEmpty(memberName))
                memberName = fieldInfo.Name;

            if (fieldInfo.FieldType == typeof(InternalHandle))
                getter = _CreateGetAccessor<InternalHandle>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(Handle))
                getter = _CreateGetAccessor<Handle>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType == typeof(bool))
                getter = _CreateGetAccessor<bool>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType == typeof(byte))
                getter = _CreateGetAccessor<byte>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(sbyte))
                getter = _CreateGetAccessor<sbyte>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType == typeof(Int16))
                getter = _CreateGetAccessor<Int16>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(UInt16))
                getter = _CreateGetAccessor<UInt16>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType == typeof(Int32))
                getter = _CreateGetAccessor<Int32>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(UInt32))
                getter = _CreateGetAccessor<UInt32>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType == typeof(Int64))
                getter = _CreateGetAccessor<Int64>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(UInt64))
                getter = _CreateGetAccessor<UInt64>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType == typeof(Single))
                getter = _CreateGetAccessor<Single>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(float))
                getter = _CreateGetAccessor<float>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(double))
                getter = _CreateGetAccessor<double>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType == typeof(string))
                getter = _CreateGetAccessor<string>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(char))
                getter = _CreateGetAccessor<char>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType == typeof(DateTime))
                getter = _CreateGetAccessor<DateTime>(memberDetails, fieldInfo);
            else if (fieldInfo.FieldType == typeof(TimeSpan))
                getter = _CreateGetAccessor<TimeSpan>(memberDetails, fieldInfo);

            else if (fieldInfo.FieldType.IsEnum)
                getter = _CreateGetAccessor<Int32>(memberDetails, fieldInfo);

            else if (_Recursive ?? false)
            {
                // ... this type is unknown, but recursive is set, so register the type implicitly and continue ...
                Engine.RegisterType(fieldInfo.FieldType);
                getter = _CreateObjectGetAccessor(memberDetails, fieldInfo);
            }
            else return false;

            setter = _CreateSetAccessor(memberDetails, fieldInfo);

            return true;
        }

        NativeSetterAccessor _CreateSetAccessor(_MemberDetails memberDetails, FieldInfo fieldInfo)
        {
            bool isInternalHandleTypeExpected = fieldInfo.FieldType == typeof(InternalHandle);

            return (HandleProxy* __this, string propertyName, HandleProxy* __value) =>
            {
                InternalHandle _this = __this, value = __value;
                if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                if (!memberDetails.HasSecurityFlags(ScriptMemberSecurity.ReadOnly))
                {
                    if (_this.IsBinder)
                    {
                        object _value = new ArgInfo(value, null, fieldInfo.FieldType).ValueOrDefault;

                        if (isInternalHandleTypeExpected && _value is InternalHandle)
                            _value = ((IHandle)fieldInfo.GetValue(_this.BoundObject)).Set((InternalHandle)_value); // (the current handle *value* must be set properly so it can be disposed before setting if need be)

                        fieldInfo.SetValue(_this.BoundObject, _value);
                    }
                    else
                        return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, fieldInfo.Name), JSValueType.ExecutionError);
                }
                return value;
            };
        }

        NativeGetterAccessor _CreateGetAccessor<T>(_MemberDetails memberDetails, FieldInfo fieldInfo)
        {
            var isSystemType = BoundType.Namespace == "System";

            if (isSystemType)
                return (HandleProxy* __this, string propertyName) =>
                {
                    InternalHandle _this = __this;
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    // (note: it's an error to read some properties in special cases on certain system type instances (such as 'Type.GenericParameterPosition'), so just ignore and return the message on system instances)
                    try
                    {
                        if (_this.IsBinder)
                            return Engine.CreateValue((T)fieldInfo.GetValue(_this.BoundObject));
                        else
                            return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, fieldInfo.Name), JSValueType.ExecutionError);
                    }
                    catch (Exception ex) { return Engine.CreateValue(Exceptions.GetFullErrorMessage(ex)); }
                };
            else
                return (HandleProxy* __this, string propertyName) =>
                {
                    InternalHandle _this = __this;
                    if (memberDetails.MemberSecurity < 0) return InternalHandle.Empty;
                    if (_this.IsBinder)
                        return Engine.CreateValue((T)fieldInfo.GetValue(_this.BoundObject));
                    else
                        return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, fieldInfo.Name), JSValueType.ExecutionError);
                };
        }

        NativeGetterAccessor _CreateObjectGetAccessor(_MemberDetails memberDetails, FieldInfo fieldInfo)
        {
            var isSystemType = BoundType.Namespace == "System";

            if (isSystemType)
                return (HandleProxy* __this, string propertyName) =>
                {
                    InternalHandle _this = __this;
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    // (note: it's an error to read some properties in special cases on certain system type instances (such as 'Type.GenericParameterPosition'), so just ignore and return the message on system instances)
                    try
                    {
                        if (_this.IsBinder)
                            return Engine.CreateValue(fieldInfo.GetValue(_this.BoundObject), _Recursive);
                        else
                            return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, fieldInfo.Name), JSValueType.ExecutionError);
                    }
                    catch (Exception ex) { return Engine.CreateValue(Exceptions.GetFullErrorMessage(ex)); }
                };
            else
                return (HandleProxy* __this, string propertyName) =>
                {
                    InternalHandle _this = __this;
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    if (_this.IsBinder)
                        return Engine.CreateValue(fieldInfo.GetValue(_this.BoundObject), _Recursive);
                    else
                        return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, fieldInfo.Name), JSValueType.ExecutionError);
                };
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Binds a getter and setter to read and/or write to the specified data member.
        /// </summary>
        /// <param name="memberName">The name of a member on '{ObjectBinder}.Object', or a new in-script name if 'propInfo' is supplied.</param>
        /// <param name="getter">Returns the getter delegate to use for a native callback.</param>
        /// <param name="setter">Returns the setter delegate to use for a native callback.</param>
        /// <param name="propInfo">If null, this will be pulled using 'memberName'.  If specified, then 'memberName' can be used to rename the property name.</param>
        /// <returns>An exception on error, or null on success.</returns>
        internal bool _GetBindingForProperty(_MemberDetails memberDetails, out NativeGetterAccessor getter, out NativeSetterAccessor setter)
        {
            string memberName = memberDetails.MemberName;
            PropertyInfo propInfo = (PropertyInfo)memberDetails.FirstMember;
            getter = null;
            setter = null;

            if (TypeTemplate == null) throw new ArgumentNullException("'TypeTemplate' is null.");

            if (string.IsNullOrEmpty(memberName))
                memberName = propInfo.Name;

            if (propInfo.PropertyType == typeof(InternalHandle))
                getter = _CreateGetAccessor<InternalHandle>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(Handle))
                getter = _CreateGetAccessor<Handle>(memberDetails, propInfo);

            else if (propInfo.PropertyType == typeof(bool))
                getter = _CreateGetAccessor<bool>(memberDetails, propInfo);

            else if (propInfo.PropertyType == typeof(byte))
                getter = _CreateGetAccessor<byte>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(sbyte))
                getter = _CreateGetAccessor<sbyte>(memberDetails, propInfo);

            else if (propInfo.PropertyType == typeof(Int16))
                getter = _CreateGetAccessor<Int16>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(UInt16))
                getter = _CreateGetAccessor<UInt16>(memberDetails, propInfo);

            else if (propInfo.PropertyType == typeof(Int32))
                getter = _CreateGetAccessor<Int32>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(UInt32))
                getter = _CreateGetAccessor<UInt32>(memberDetails, propInfo);

            else if (propInfo.PropertyType == typeof(Int64))
                getter = _CreateGetAccessor<Int64>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(UInt64))
                getter = _CreateGetAccessor<UInt64>(memberDetails, propInfo);

            else if (propInfo.PropertyType == typeof(Single))
                getter = _CreateGetAccessor<Single>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(float))
                getter = _CreateGetAccessor<float>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(double))
                getter = _CreateGetAccessor<double>(memberDetails, propInfo);

            else if (propInfo.PropertyType == typeof(string))
                getter = _CreateGetAccessor<string>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(char))
                getter = _CreateGetAccessor<char>(memberDetails, propInfo);

            else if (propInfo.PropertyType == typeof(DateTime))
                getter = _CreateGetAccessor<DateTime>(memberDetails, propInfo);
            else if (propInfo.PropertyType == typeof(TimeSpan))
                getter = _CreateGetAccessor<TimeSpan>(memberDetails, propInfo);

            else if (propInfo.PropertyType.IsEnum)
                getter = _CreateGetAccessor<Int32>(memberDetails, propInfo);

            else if (_Recursive ?? false)
            {
                // ... this type is unknown, but recursive is set, so register the type implicitly and continue ...
                Engine.RegisterType(propInfo.PropertyType);
                getter = _CreateObjectGetAccessor(memberDetails, propInfo);
            }
            else return false;

            setter = _CreateSetAccessor(memberDetails, propInfo);

            return true;
        }

        NativeSetterAccessor _CreateSetAccessor(_MemberDetails memberDetails, PropertyInfo propertyInfo)
        {
            bool isInternalHandleTypeExpected = propertyInfo.PropertyType == typeof(InternalHandle);
            bool canRead = propertyInfo.CanRead;
            bool canWrite = propertyInfo.CanWrite;

            return (HandleProxy* __this, string propertyName, HandleProxy* value) =>
            {
                InternalHandle _this = __this;
                if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                if (canWrite && !memberDetails.HasSecurityFlags(ScriptMemberSecurity.ReadOnly))
                {
                    if (_this.IsBinder)
                    {
                        object _value = new ArgInfo(value, null, propertyInfo.PropertyType).ValueOrDefault;

                        if (isInternalHandleTypeExpected && canRead && _value is InternalHandle)
                            _value = ((IHandle)propertyInfo.GetValue(_this.BoundObject, null)).Set((InternalHandle)_value); // (the current handle *value* must be set properly so it can be disposed before setting if need be)

                        propertyInfo.SetValue(_this.BoundObject, _value, null);
                    }
                    else
                        return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, propertyInfo.Name), JSValueType.ExecutionError);
                }
                return value;
            };
        }

        NativeGetterAccessor _CreateGetAccessor<T>(_MemberDetails memberDetails, PropertyInfo propertyInfo)
        {
            var isSystemType = BoundType.Namespace == "System";
            //??var getMethod = propertyInfo.GetGetMethod();

            if (isSystemType)
                return (HandleProxy* __this, string propertyName) =>
                {
                    InternalHandle _this = __this;
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    if (propertyInfo.CanRead)
                    {
                        // (note: it's an error to read some properties in special cases on certain system type instances (such as 'Type.GenericParameterPosition'), so just ignore and return the message on system instances)
                        try
                        {
                            if (_this.IsBinder)
                                return Engine.CreateValue((T)propertyInfo.GetValue(_this.BoundObject, null));
                            else
                                return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, propertyInfo.Name), JSValueType.ExecutionError);
                        }
                        catch (Exception ex) { return Engine.CreateValue(Exceptions.GetFullErrorMessage(ex)); }
                    }
                    return InternalHandle.Empty;
                };
            else
                return (HandleProxy* __this, string propertyName) =>
                {
                    InternalHandle _this = __this;
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    if (propertyInfo.CanRead)
                    {
                        if (_this.IsBinder)
                            return Engine.CreateValue((T)propertyInfo.GetValue(_this.BoundObject, null));
                        else
                            return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, propertyInfo.Name), JSValueType.ExecutionError);
                    }
                    return InternalHandle.Empty;
                };
        }

        NativeGetterAccessor _CreateObjectGetAccessor(_MemberDetails memberDetails, PropertyInfo propertyInfo)
        {
            var isSystemType = BoundType.Namespace == "System";
            //??var getMethod = propertyInfo.GetGetMethod();

            if (isSystemType)
                return (HandleProxy* __this, string propertyName) =>
                {
                    InternalHandle _this = __this;
                    if (propertyInfo.CanRead)
                    {
                        // (note: it's an error to read some properties in special cases on certain system type instances (such as 'Type.GenericParameterPosition'), so just ignore and return the message on system instances)
                        try
                        {
                            if (_this.IsBinder)
                                return Engine.CreateValue(propertyInfo.GetValue(_this.BoundObject, null), _Recursive);
                            else
                                return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, propertyInfo.Name), JSValueType.ExecutionError);
                        }
                        catch (Exception ex) { return Engine.CreateValue(Exceptions.GetFullErrorMessage(ex)); }
                    }
                    return InternalHandle.Empty;
                };
            else
                return (HandleProxy* __this, string propertyName) =>
                {
                    InternalHandle _this = __this;
                    if (propertyInfo.CanRead)
                    {
                        if (_this.IsBinder)
                            return Engine.CreateValue(propertyInfo.GetValue(_this.BoundObject, null), _Recursive);
                        else
                            return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "property", propertyName, propertyInfo.Name), JSValueType.ExecutionError);
                    }
                    return InternalHandle.Empty;
                };
        }

        // --------------------------------------------------------------------------------------------------------------------

        public static string GetTypeName(Type type)
        {
            if (!type.IsGenericType) return type.Name;
            var name = type.Name.Substring(0, type.Name.LastIndexOf('`')) + "<";
            var i = 0;
            foreach (var p in type.GetGenericArguments())
                name += (i++ > 0 ? ", " : "") + GetTypeName(p);
            return name + ">";
        }

        public static string GetMethodSignatureAsText(MethodInfo methodInfo, int paramErrorIndex = -1)
        {
            var expectedParameters = methodInfo.GetParameters();
            var msg = methodInfo.Name;
            if (methodInfo.IsGenericMethod)
            {
                msg += "<";
                var genTypes = methodInfo.GetGenericArguments();
                for (var i = 0; i < genTypes.Length; i++)
                    msg += (i > 0 ? ", " : "") + genTypes[i].Name;
                msg += ">";
            }
            msg += "(";
            for (var i = 0; i < expectedParameters.Length; i++)
            {
                if (i > 0) msg += ", ";
                if (i != paramErrorIndex)
                    msg += GetTypeName(expectedParameters[i].ParameterType) + " " + expectedParameters[i].Name;
                else
                    msg += "» " + GetTypeName(expectedParameters[i].ParameterType) + " " + expectedParameters[i].Name + " «";
            }
            return msg + ")";
        }

        // --------------------------------------------------------------------------------------------------------------------

        InternalHandle _TranslateGenericArguments(_MemberDetails memberDetails, Type[] expectedGenericTypes, InternalHandle[] args, ref uint argOffset,
            out TypeLibrary<MemberInfo> constructedMembers, ref ParameterInfo[] expectedParameters)
        {
            bool isGenericInvocation = ((MethodBase)memberDetails.FirstMember).IsGenericMethodDefinition; // (note: always false for constructors!)
            InternalHandle[] typeArgs;
            ArgInfo[] genericArgInfos;
            constructedMembers = null;

            if (isGenericInvocation)
            {
                // ... get the type arguments from the first argument, which should be an array ...
                // (first argument is always an array of types)

                if (args.Length == 0 || !args[0].IsArray || args[0].ArrayLength == 0)
                    return Engine.CreateError("No types given: The first argument of a generic method must be an array of types.", JSValueType.ExecutionError);

                typeArgs = new InternalHandle[args[0].ArrayLength]; // TODO: Create a faster way to extract an array of internal handles.

                for (int i = 0; i < args[0].ArrayLength; i++)
                    typeArgs[i] = args[0].GetProperty(i);

                // ... with the array of types, convert to an 'ArgInfo' array with the extracted argument type information and values...

                genericArgInfos = ArgInfo.GetTypes(typeArgs, 0, expectedGenericTypes); // ('expectedGenericTypes' is a fixed array of types that are ALWAYS expected if only 1 member exists [no overloads])
                var genericSystemTypes = ArgInfo.GetSystemTypes(genericArgInfos);

                if (memberDetails.ConstructedMemberGroups == null)
                    memberDetails.ConstructedMemberGroups = new TypeLibrary<TypeLibrary<MemberInfo>>();

                constructedMembers = memberDetails.ConstructedMemberGroups.Get(true, genericSystemTypes);

                if (constructedMembers == null)
                {
                    // ... there can be multiple generic methods (overloads) with different parameters types, so we need to generate and cache each one to see the affect on the parameters ...

                    var genericMethodInfos = memberDetails.ImmediateMembers.Items.Select(i => (MethodInfo)i.Object).ToArray();
                    constructedMembers = new TypeLibrary<MemberInfo>();
                    MethodInfo constructedMethod = null;

                    for (int i = 0; i < genericMethodInfos.Length; i++)
                    {
                        constructedMethod = genericMethodInfos[i].MakeGenericMethod(genericSystemTypes);
                        constructedMembers.Set(constructedMethod, constructedMethod.GetParameters().Select(p => p.ParameterType).ToArray());
                        memberDetails.TotalConstructedMembers++;
                    }

                    // ... cache the array of constructed generic methods (any overloads with the same generic parameters), which will exist in a type
                    // library for quick lookup the next time this given types are supplied ...
                    memberDetails.ConstructedMemberGroups.Set(constructedMembers, genericSystemTypes);
                }

                // ... if there's only one group of constructed members, and only one member in that group, then we can force the parameter types (makes
                // calling it much easier for the user) ...

                if (memberDetails.TotalConstructedMembers == 1 && constructedMembers.Items.Count() == 1)
                    expectedParameters = ((MethodBase)constructedMembers.Items.First().Object).GetParameters();
                // TODO: Consider a more efficient way to do the above.

                // ... at this point the 'constructedMethodInfos' will have an array of methods that match the types supplied for this generic method call.
                // Next, the arguments ????

                argOffset = 1; // (skip first array argument)
            }

            return InternalHandle.Empty;
        }

        void _CopyArguments(ParameterInfo[] expectedParameters, Dictionary<int, object[]> convertedArgumentArrayCache, InternalHandle[] args,
            ref int paramIndex, uint argOffset, out object[] convertedArguments, out ArgInfo[] argInfos)
        {
            convertedArguments = null;

            argInfos = ArgInfo.GetArguments(args, argOffset, expectedParameters);

            // ... create/grow the converted arguments array if necessary ...
            if (!convertedArgumentArrayCache.TryGetValue(argInfos.Length, out convertedArguments) || convertedArguments.Length < argInfos.Length)
                convertedArgumentArrayCache[argInfos.Length] = convertedArguments = new object[argInfos.Length]; // (array is too small, so discard

            ArgInfo tInfo;

            for (paramIndex = 0; paramIndex < argInfos.Length; paramIndex++)
            {
                tInfo = argInfos[paramIndex];

                if (tInfo.HasError) throw tInfo.Error;

                convertedArguments[paramIndex] = tInfo.ValueOrDefault;
            }

            paramIndex = -1;
        }

        InternalHandle _InvokeMethod(_MemberDetails memberDetails, MethodInfo soloMethod, object[] convertedArguments, ref InternalHandle _this,
            ArgInfo[] argInfos, TypeLibrary<MemberInfo> constructedMembers)
        {
            if (soloMethod != null)
            {
                var result = soloMethod.Invoke(_this.BoundObject, convertedArguments);
                return soloMethod.ReturnType == typeof(void) ? InternalHandle.Empty : Engine.CreateValue(result, _Recursive);
            }
            else // ... more than one method exists (overloads) ..
            {
                var systemTypes = ArgInfo.GetSystemTypes(argInfos);

                var methodInfo = constructedMembers != null ? (MethodInfo)constructedMembers.Get(true, systemTypes) : (MethodInfo)memberDetails.FindMemberByTypes(true, systemTypes); // (note: this expects exact types matches!)
                if (methodInfo == null)
                    methodInfo = constructedMembers != null ? (MethodInfo)constructedMembers.Get(false, systemTypes) : (MethodInfo)memberDetails.FindMemberByTypes(false, systemTypes); // (this is a lot more relaxed)

                if (methodInfo == null)
                    throw new TargetInvocationException("There is no method matching the supplied parameter types ("
                        + String.Join(", ", (from t in systemTypes select GetTypeName(t)).ToArray()) + ").", null);
                var result = methodInfo.Invoke(_this.BoundObject, convertedArguments);
                return methodInfo.ReturnType == typeof(void) ? InternalHandle.Empty : Engine.CreateValue(result, _Recursive);
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Binds a specific or named method of the specified object to a 'V8Function' callback wrapper.
        /// The returned function can be used in setting native V8 object properties to function values.
        /// </summary>
        /// <param name="memberDetails">A reference to the member details for the given function representing the underlying type.</param>
        /// <param name="func">The 'V8Function' wrapper for specified method. This function can be used with 'new' in JS to create a new instance of the CLR type.</param>
        /// <param name="methodName">An optional name to return when 'valueOf()' is called on a JS object (this defaults to the method's name [i.e. 'memberDetails.MemberName']).</param>
        internal bool _GetBindingForMethod(_MemberDetails memberDetails, out V8Function func, string methodName = null)
        {
            func = null;

            if (memberDetails == null) throw new ArgumentNullException("'memberDetails' is null or empty.");

            if (TypeTemplate == null) throw new InvalidOperationException("'TypeTemplate' is null.");
            if (BoundType == null) throw new InvalidOperationException("'BoundType' is null.");

            if (string.IsNullOrEmpty(methodName))
                methodName = memberDetails.MemberName;

            bool isGenericMember = ((MethodInfo)memberDetails.FirstMember).IsGenericMethodDefinition;
            MethodInfo soloMethod = memberDetails.TotalImmediateMembers == 1 && !isGenericMember ? (MethodInfo)memberDetails.FirstMember : null;
            var expectedParameters = soloMethod != null ? soloMethod.GetParameters() : null;
            var expectedGenericTypes = isGenericMember ? ((MethodInfo)memberDetails.FirstMember).GetGenericArguments() : null;

            Dictionary<int, object[]> convertedArgumentArrayCache = new Dictionary<int, object[]>(); // (a cache of argument arrays based on argument length to use for calling overloaded methods)
            object[] convertedArguments;
            // (note: the argument array cache for method invocations will exist in the closure for this member only)

            // ... if we know the number of parameters (only one method) then create the argument array now ...
            if (expectedParameters != null)
                convertedArgumentArrayCache[expectedParameters.Length] = new object[expectedParameters.Length];

            var funcTemplate = Engine.CreateFunctionTemplate(methodName);

            func = funcTemplate.GetFunctionObject<V8Function>((V8Engine engine, bool isConstructCall, InternalHandle _this, InternalHandle[] args) =>
            {
                if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                if (isConstructCall) return Engine.CreateError("Objects cannot be constructed from this function.", JSValueType.ExecutionError); // TODO: Test.

                ArgInfo[] argInfos;
                int paramIndex = -1;
                uint argOffset = 0;
                TypeLibrary<MemberInfo> constructedMembers;
                var _expectedParameters = expectedParameters;

                try
                {
                    //?if (memberDetails.BindingMode == BindingMode.Instance && !_this.IsBinder)
                    if (!_this.IsBinder)
                        return Engine.CreateError(string.Format(TYPE_BINDER_MISSING_MSG, "function", methodName, memberDetails.MemberName), JSValueType.ExecutionError);

                    // ... translate the generic arguments, if applicable ...

                    var paResult = _TranslateGenericArguments(memberDetails, expectedGenericTypes, args, ref argOffset, out constructedMembers, ref _expectedParameters);
                    if (!paResult.IsEmpty) return paResult;

                    // ... get the actual arguments passed from script into an argument value array in order to invoke the method ...

                    _CopyArguments(_expectedParameters, convertedArgumentArrayCache, args, ref paramIndex, argOffset, out convertedArguments, out argInfos);

                    // ... invoke the method ...

                    return _InvokeMethod(memberDetails, soloMethod, convertedArguments, ref _this, argInfos, constructedMembers);
                }
                catch (Exception ex)
                {
                    var msg = "Failed to invoke method ";
                    if (expectedParameters != null)
                    {
                        msg += GetMethodSignatureAsText(soloMethod, paramIndex) + Environment.NewLine;
                    }
                    else
                    {
                        msg = memberDetails.MemberName + ":  No method was found matching the specified arguments.  These are the available parameter types:" + Environment.NewLine;
                        foreach (var m in memberDetails.MethodMembers)
                            msg += GetMethodSignatureAsText(m, paramIndex) + Environment.NewLine;
                        msg += Environment.NewLine;
                    }
                    var argError = paramIndex >= 0 ? "Error is in argument #" + (1 + paramIndex) + ":" + Environment.NewLine : "";
                    return Engine.CreateError(msg + argError + Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
                }
            });

            var sigCount = 1;
            foreach (var m in memberDetails.MethodMembers)
                func.SetProperty("$__Signature" + (sigCount++), Engine.CreateValue(GetMethodSignatureAsText(m)), V8PropertyAttributes.Locked);

            return true;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Binds the constructor and all static members on the underlying type.
        /// </summary>
        void _BindTypeMembers()
        {
            // (note: if abstract, '_Constructors' will be 'null')
            ConstructorInfo soloConstructor = _Constructors != null && _Constructors.TotalImmediateMembers == 1 ? (ConstructorInfo)_Constructors.FirstMember : null;
            var expectedParameters = soloConstructor != null ? soloConstructor.GetParameters() : null;

            Dictionary<int, object[]> convertedArgumentArrayCache = new Dictionary<int, object[]>(); // (a cache of argument arrays based on argument length to use for calling overloaded methods)
            object[] convertedArguments;
            // (note: the argument array cache for method invocations will exist in the closure for this member only)

            // ... if we know the number of parameters (only one method) then create the argument array now ...
            if (expectedParameters != null)
                convertedArgumentArrayCache[expectedParameters.Length] = new object[expectedParameters.Length];

            TypeFunction = TypeTemplate.GetFunctionObject<TypeBinderFunction>((engine, isConstructCall, _this, args) =>
            {
                InternalHandle handle;
                uint argOffset = 0;
                int paramIndex = -1;
                TypeLibrary<MemberInfo> constructedMembers;
                var _expectedParameters = expectedParameters;

                if (isConstructCall)
                    try
                    {
                        if (BoundType.IsAbstract) // (note: if abstract, '_Constructors' will be 'null')
                            handle = Engine.CreateError("The CLR type '" + BoundType.Name + "' is abstract - you cannot create instances from abstract types.", JSValueType.ExecutionError);
                        else
                        {
                            // ... translate the generic arguments, if applicable ...

                            var paResult = _TranslateGenericArguments(_Constructors, null, args, ref argOffset, out constructedMembers, ref _expectedParameters); // TODO: This is most likely irrelevant for constructors! Look into bypassing.
                            if (!paResult.IsEmpty) return paResult;

                            // ... get the actual arguments passed from script into an argument value array in order to invoke the method ...

                            _CopyArguments(_expectedParameters, convertedArgumentArrayCache, args, ref paramIndex, argOffset, out convertedArguments, out ArgInfo[] argInfos);

                            // ... invoke the constructor ...

                            handle = Engine.CreateBinding(Activator.CreateInstance(BoundType, convertedArguments), null, _Recursive, _DefaultMemberSecurity, false);
                            handle.Object.Initialize(true, args);

                            // ... set the prototype of the new instance (created from an object template) to maintain the prototype chain ...

                            //handle.SetProperty("__proto__", _this); // TODO: Really, accessor hooks on the function instance template needs to be used with the object binder.

                            //??if (soloConstructor != null)
                            //{
                            //    var result = Engine.CreateBinding(Activator.CreateInstance(BoundType, convertedArguments), null, _Recursive, _DefaultMemberSecurity);
                            //}
                            //else // ... more than one method exists (overloads) ..
                            //{
                            //    var systemTypes = ArgInfo.GetSystemTypes(argInfos);
                            //    var methodInfo = constructedMembers != null ? (MethodInfo)constructedMembers.Get(systemTypes) : (MethodInfo)memberDetails.FindMemberByTypes(systemTypes); // (note: this expects exact types matches!)
                            //    if (methodInfo == null)
                            //        throw new TargetInvocationException("There is no method matching the supplied parameter types ("
                            //            + String.Join(", ", (from t in systemTypes select GetTypeName(t)).ToArray()) + ").", null);
                            //    var result = methodInfo.Invoke(_this.BoundObject, convertedArguments);
                            //    return methodInfo.ReturnType == typeof(void) ? InternalHandle.Empty : Engine.CreateValue(result, _Recursive);
                            //}
                        }
                    }
                    catch (Exception ex)
                    {
                        handle = Engine.CreateError("Failed to invoke constructor: " + Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
                    }
                else
                    try
                    {
                        // ... when a type is invoked like a function, a special typed-value object is returned for use as strong-CLR-typed arguments ...

                        if (args.Length > 0)
                        {
                            var bindingMode = args[0].BindingMode;
                            if (bindingMode == BindingMode.Static)
                                return Engine.CreateError("You cannot pass the static type '" + args[0].TypeBinder.BoundType.Name + "' as an argument.", JSValueType.ExecutionError);
                            //??else if (bindingMode == BindingMode.Instance)
                            //    return Engine.CreateError("You cannot pass the object binder for '" + args[0].TypeBinder.BoundType.Name + "' as an argument.", JSValueType.ExecutionError);
                        }

                        if (args.Length > 1)
                            return Engine.CreateError("You cannot pass more than one argument.", JSValueType.ExecutionError);

                        handle = Engine.CreateObject(/*Int32.MinValue + TypeID*/);
                        handle.SetProperty("$__Type", Engine.CreateValue(BoundType.AssemblyQualifiedName), V8PropertyAttributes.Locked);
                        handle.SetProperty("$__TypeID", Engine.CreateValue(TypeID), V8PropertyAttributes.Locked);
                        handle.SetProperty("$__Value", args.Length > 0 ? args[0] : InternalHandle.Empty, V8PropertyAttributes.DontDelete);
                    }
                    catch (Exception ex)
                    {
                        handle = Engine.CreateError("Failed to strongly type the specified value '" + args[0].Value + "': " + Exceptions.GetFullErrorMessage(ex), JSValueType.ExecutionError);
                    }

                return handle;
            });

            TypeFunction._BindingMode = BindingMode.Static;
            TypeFunction.TypeBinder = this;

            // TODO: Consolidate the below with the template version above (see '_ApplyBindingToTemplate()').

            foreach (var details in _FieldDetails(BindingMode.Static))
                if (_GetBindingForDataMember(details, out var getter, out var setter) && details.MemberSecurity >= 0)
                {
                    details.Getter = getter;
                    details.Setter = setter;
                    _RefreshMemberSecurity(details);
                }

            foreach (var details in _PropertyDetails(BindingMode.Static))
                if (_GetBindingForDataMember(details, out var getter, out var setter) && details.MemberSecurity >= 0)
                {
                    details.Getter = getter;
                    details.Setter = setter;
                    _RefreshMemberSecurity(details);
                }

            foreach (var details in _MethodDetails(BindingMode.Static))
                if (_GetBindingForMethod(details, out var func) && details.MemberSecurity >= 0)
                {
                    details.Method = func;
                    _RefreshMemberSecurity(details);
                }
        }

        void _RefreshMemberSecurity(_MemberDetails member, ScriptMemberSecurity? security = null, bool forceChange = false)
        {
            if (forceChange || member.MemberSecurity >= 0) // (once NoAccess is set, this cannot be changed by default)
            {
                member.MemberSecurity = security ?? member.MemberSecurity;

                // ... if this member is static, then make sure to update the related property on the static constructor function ...

                if (member.BindingMode == BindingMode.Static)
                {
                    // ... need to translate this for the native side - for instance, -1 "No Access" has no native side support ...
                    var propertyAttribs = (V8PropertyAttributes)(member.MemberSecurity == ScriptMemberSecurity.NoAcccess ? ScriptMemberSecurity.Hidden | ScriptMemberSecurity.Locked : member.MemberSecurity < 0 ? 0 : member.MemberSecurity);

                    switch (member.MemberType)
                    {
                        case MemberTypes.Field:
                            V8NetProxy.SetObjectAccessor(TypeFunction, TypeFunction.Object.ID, member.MemberName, member.Getter, member.Setter, V8AccessControl.Default, propertyAttribs);
                            //TypeFunction.SetAccessor(member.MemberName, member.Getter, member.Setter, propertyAttribs); // TODO: Investigate need to add access control value.
                            break;
                        case MemberTypes.Property:
                            V8NetProxy.SetObjectAccessor(TypeFunction, TypeFunction.Object.ID, member.MemberName, member.Getter, member.Setter, V8AccessControl.Default, propertyAttribs);
                            //TypeFunction.SetAccessor(member.MemberName, member.Getter, member.Setter, propertyAttribs); // TODO: Investigate need to add access control value.
                            break;
                        case MemberTypes.Method:
                            TypeFunction.SetProperty(member.MemberName, member.Method, propertyAttribs); // TODO: Investigate need to add access control value.
                            break;
                    }
                }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        //??readonly Dictionary<int, object> _Instances = new Dictionary<int, object>();
        //??"THIS DOESN'T SOLVE THE TYPE BIND GC ISSUE YET"

        /// <summary>
        /// Returns a new 'ObjectBinder' based instance that is associated with the specified object instance.
        /// It's an error to pass an object instance that is not of the same underlying type as this type binder.
        /// </summary>
        /// <param name="obj">An object instance for the object binder (required).</param>
        /// <param name="initializeBinder">If true (default) then then 'IV8NativeObject.Initialize()' is called on the created object before returning.</param>
        /// <returns>A new 'ObjectBinder' instance you can use when setting properties to the specified object instance.</returns>
        public T CreateObject<T, InstanceType>(InstanceType obj, bool initializeBinder = true)
            where T : ObjectBinder, new()
            where InstanceType : class
        {
            if (obj == null) throw new ArgumentNullException("obj");

            var objType = obj.GetType();
            if (objType != BoundType)
                throw new InvalidOperationException("'obj' instance of type '" + objType.Name + "' is not compatible with type '" + BoundType.Name + "' as represented by this type binder.");

            var binder = InstanceTemplate.CreateObject<T>(initializeBinder);

            binder.Object = obj;
            binder.InternalHandle.KeepAlive();

            return binder;
        }

        /// <summary>
        /// Returns a new 'ObjectBinder' based instance that is associated with the specified object instance.
        /// It's an error to pass an object instance that is not of the same underlying type as this type binder.
        /// </summary>
        /// <param name="initializeBinder">If true (default) then 'IV8NativeObject.Initialize()' is called on the created object before returning.</param>
        /// <returns>A new 'ObjectBinder' instance you can use when setting properties to the specified object instance.</returns>
        public ObjectBinder CreateObject(object obj, bool initializeBinder = true)
        {
            return CreateObject<ObjectBinder, object>(obj, initializeBinder);
        }

        // --------------------------------------------------------------------------------------------------------------------

        internal _MemberDetails _GetMemberDetails(MemberInfo member)
        {
            var members = from md in _Members.Values where md.ImmediateMembers.Items.Any(i => i.Object.MetadataToken == member.MetadataToken && i.Object.Module == member.Module) select md;
            return members.FirstOrDefault();
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Changes the security of a specific member for the underlying type represented by this TypeBinder instance.
        /// </summary>
        /// <param name="member">A specific MemberInfo instance.  If this is not found/supported on the local type, an exception will be thrown.</param>
        /// <param name="memberSecurity">The new security to apply.</param>
        public void ChangeMemberSecurity(MemberInfo member, ScriptMemberSecurity memberSecurity)
        {
            var memberDetails = _GetMemberDetails(member);

            if (memberDetails == null) throw new MissingMemberException("The member '" + member.Name + "' was not found.");

            _RefreshMemberSecurity(memberDetails, memberSecurity);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Changes the security of a specific member for the underlying type represented by this TypeBinder instance.
        /// </summary>
        /// <param name="memberName">A specific member name.  If this is not found/supported on the local type, or the name has too many matches, an exception will be thrown.
        /// <para>Note: The name you enter here is the in-script name, including any "${type #}" suffixes for generic types (for example, "Join$1", where '1' is the
        /// number of expected generic types).  If a member has overloads, the security attribute will apply to all of them.</para></param>
        /// <param name="security">The new security to apply.</param>
        public void ChangeMemberSecurity(string memberName, ScriptMemberSecurity memberSecurity)
        {
            var memberDetails = (from kv in _Members where kv.Key == memberName select kv.Value).FirstOrDefault();

            if (memberDetails == null) throw new MissingMemberException("The member '" + memberName + "' was not found.");

            _RefreshMemberSecurity(memberDetails, memberSecurity);
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    /// <summary>
    /// 'ObjectBinder' instances represent JavaScript object properties that are bound to CLR objects or types.
    /// </summary>
    public unsafe class ObjectBinder : V8ManagedObject
    {
        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        ///     Gets or sets the CLR object that is associated with this binding. You can dynamically replace objects, but only of
        ///     the same type once set the first time.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the object is set again with an instance of a different type.
        /// </exception>
        /// <value> The object associated with this binding. </value>
        new public object Object
        {
            get { return _Object; }
            set
            {
                if (value == null) throw new InvalidOperationException("'value' cannot be null.");

                var valueType = value.GetType();

                if (_ObjectType == null)
                {
                    _Object = value;
                    ObjectType = valueType;
                }
                else if (valueType == _ObjectType)
                    _Object = value;
                else
                    throw new InvalidOperationException("Once an object is set, you can only replace the instance with another of the SAME type.");
            }
        }
        internal object _Object;

        /// <summary>
        ///     Gets or sets the type of the object that this binder will work with. Once this is set it cannot be changed.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the object type is set again with a different type.
        /// </exception>
        /// <value> The type of the object expected for this binder. </value>
        public Type ObjectType
        {
            get { return _ObjectType; }
            set
            {
                if (value == null) throw new InvalidOperationException("'value' cannot be null.");
                if (_ObjectType == null)
                {
                    _ObjectType = value;
                    TypeBinder = Engine.RegisterType(_ObjectType);
                }
                else if (value != _ObjectType)
                    throw new InvalidOperationException("Once an object type is set you cannot change it.");
            }
        }
        Type _ObjectType;

        /// <summary>
        ///     Gets or sets the type binder that is automatically set when <see cref="Object"/> or <see cref="ObjectType"/> is set.
        ///     The TypeBinder reference holds all the cached reflection details needed to read the associated CLR object instance.
        ///     As the JavaScript code accesses new properties and types not yet bound, they get bound automatically when accessed.
        ///     This prevents the need to bind everything at once!
        /// </summary>
        /// <value> The type binder associated with this binder instance. </value>
        public TypeBinder TypeBinder { get; private set; }

        public ObjectBinder() { _BindingMode = BindingMode.Instance; }

        // --------------------------------------------------------------------------------------------------------------------

        public override InternalHandle Initialize(bool isConstructCall, params InternalHandle[] args)
        {
            if (_Object != null)
            {
                if (ObjectType == null)
                    ObjectType = _Object.GetType();

                if (_Object is IV8NativeObject)
                    _Proxy = (IV8NativeObject)_Object;
            }

            return base.Initialize(isConstructCall, args);
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override InternalHandle IndexedPropertyGetter(int index)
        {
            if (TypeBinder.Indexer != null && TypeBinder.Indexer.CanRead)
                return Engine.CreateValue(TypeBinder.Indexer.GetValue(Object, new object[] { index }), TypeBinder._Recursive);
            return InternalHandle.Empty;
        }
        public override InternalHandle IndexedPropertySetter(int index, InternalHandle value, V8PropertyAttributes attributes = V8PropertyAttributes.Undefined)
        {
            if (TypeBinder.Indexer != null && TypeBinder.Indexer.CanWrite)
                TypeBinder.Indexer.SetValue(_Object, new ArgInfo(value, null, TypeBinder.Indexer.PropertyType).ValueOrDefault, new object[] { index });
            return IndexedPropertyGetter(index);
        }
        public override bool? IndexedPropertyDeleter(int index)
        {
            return null;
        }
        public override V8PropertyAttributes? IndexedPropertyQuery(int index)
        {
            return null;
        }
        public override InternalHandle IndexedPropertyEnumerator()
        {
            return InternalHandle.Empty;
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override InternalHandle NamedPropertyGetter(ref string propertyName)
        {
            var memberDetails = TypeBinder._Members.GetValueOrDefault(propertyName);
            if (memberDetails != null && memberDetails.BindingMode == BindingMode.Instance && memberDetails.Accessible) // (no access == undefined) 
            {
                if (!memberDetails.ValueOverride.IsEmpty && !memberDetails.ValueOverride.IsUndefined) return memberDetails.ValueOverride;

                switch (memberDetails.MemberType)
                {
                    case MemberTypes.Field:
                        {
                            var fieldInfo = (FieldInfo)memberDetails.FirstMember;
                            var getter = memberDetails.Getter;
                            if (getter == null)
                            {
                                // .. first time access, create a binding ...
                                TypeBinder._GetBindingForDataMember(memberDetails, out getter, out NativeSetterAccessor setter);
                                memberDetails.Getter = getter;
                                memberDetails.Setter = setter;
                            }
                            return getter.Invoke(_Handle, propertyName);
                        }
                    case MemberTypes.Property:
                        {
                            var propertyInfo = (PropertyInfo)memberDetails.FirstMember;
                            if (!propertyInfo.CanRead) break;
                            var getter = memberDetails.Getter;
                            if (getter == null)
                            {
                                // .. first time access, create a binding ...
                                TypeBinder._GetBindingForDataMember(memberDetails, out getter, out NativeSetterAccessor setter);
                                memberDetails.Getter = getter;
                                memberDetails.Setter = setter;
                            }
                            return getter.Invoke(_Handle, propertyName);
                        }
                    case MemberTypes.Method:
                        {
                            var method = memberDetails.Method;
                            if (method == null)
                            {
                                TypeBinder._GetBindingForMethod(memberDetails, out method);
                                memberDetails.Method = method;
                            }
                            return method;
                        }
                }
            }
            return InternalHandle.Empty;
        }

        public override InternalHandle NamedPropertySetter(ref string propertyName, InternalHandle value, V8PropertyAttributes attributes = V8PropertyAttributes.Undefined)
        {
            var memberDetails = TypeBinder._Members.GetValueOrDefault(propertyName);
            if (memberDetails != null && memberDetails.BindingMode == BindingMode.Instance && !memberDetails.HasSecurityFlags(ScriptMemberSecurity.NoAcccess)) // (if undefined = no access)
            {
                // ... if the member details contains a valid override value, update and return it instead ...
                // (override values exist because method properties cannot be overwritten; to restore, the property must be deleted)
                if (!memberDetails.ValueOverride.IsEmpty)
                {
                    memberDetails.ValueOverride = value.KeepTrack();
                    if (!memberDetails.ValueOverride.IsUndefined)
                        return memberDetails.ValueOverride;
                }

                switch (memberDetails.MemberType)
                {
                    case MemberTypes.Field:
                        {
                            var fieldInfo = (FieldInfo)memberDetails.FirstMember;
                            if (!fieldInfo.IsInitOnly)
                            {
                                var setter = memberDetails.Setter;
                                if (setter == null)
                                {
                                    // .. first time access, create a binding ...
                                    TypeBinder._GetBindingForDataMember(memberDetails, out NativeGetterAccessor getter, out setter);
                                    memberDetails.Getter = getter;
                                    memberDetails.Setter = setter;
                                }
                                return setter.Invoke(_Handle, propertyName, value);
                            }
                            break;
                        }
                    case MemberTypes.Property:
                        {
                            var propertyInfo = (PropertyInfo)memberDetails.FirstMember;
                            if (!propertyInfo.CanWrite) break;
                            var setter = memberDetails.Setter;
                            if (setter == null)
                            {
                                // .. first time access, create a binding ...
                                TypeBinder._GetBindingForDataMember(memberDetails, out NativeGetterAccessor getter, out setter);
                                memberDetails.Getter = getter;
                                memberDetails.Setter = setter;
                            }
                            return setter.Invoke(_Handle, propertyName, value);
                        }
                    case MemberTypes.Method:
                        {
                            if (memberDetails.ValueOverride.IsEmpty)
                            {
                                var method = memberDetails.Method;
                                if (method != null)
                                {
                                    TypeBinder._GetBindingForMethod(memberDetails, out method);
                                    memberDetails.Method = method;
                                }
                                return method;
                            }
                            else
                            {
                                return memberDetails.ValueOverride = value.KeepTrack();
                            }
                        }
                }
            }
            return InternalHandle.Empty;
        }

        public override bool? NamedPropertyDeleter(ref string propertyName)
        {
            var memberDetails = TypeBinder._Members.GetValueOrDefault(propertyName);
            // (make sure to only consider instance members)
            if (memberDetails != null && memberDetails.BindingMode == BindingMode.Instance && !memberDetails.HasSecurityFlags(ScriptMemberSecurity.NoAcccess)) // (if undefined = no access)
            {
                if (!memberDetails.ValueOverride.IsEmpty)
                {
                    memberDetails.ValueOverride.Dispose();
                    return true;
                }
            }
            return null;
        }

        public override V8PropertyAttributes? NamedPropertyQuery(ref string propertyName)
        {
            var memberDetails = TypeBinder._Members.GetValueOrDefault(propertyName);
            // (make sure to only consider instance members)
            if (memberDetails != null && memberDetails.BindingMode == BindingMode.Instance && !memberDetails.HasSecurityFlags(ScriptMemberSecurity.NoAcccess)) // (if undefined = no access)
            {
                return (V8PropertyAttributes)memberDetails.MemberSecurity;
            }
            return null;
        }

        public override InternalHandle NamedPropertyEnumerator()
        {
            return Engine.CreateValue(from m in TypeBinder._Members.Values
                                      where m.BindingMode == BindingMode.Instance && !m.HasSecurityFlags(ScriptMemberSecurity.Hidden) // (make sure to only enumerate instance members)
                                      select m.MemberName);
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    public class ObjectBinder<T> : ObjectBinder where T : class, new()
    {
        public ObjectBinder() { Object = new T(); }
    }

    // ========================================================================================================================
    // The binding section has methods to help support exposing objects and types to the V8 JavaScript environment.

    public unsafe partial class V8Engine
    {
        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// This is a global setting for this engine instance for binding members of types that do not have one of the 'ScriptObject' or 'ScriptMember' security attributes.
        /// Using this default security, if you call a function in script that returns an unregistered managed type, the type will be available by reference only,
        /// and no members will be bound (no properties will exist).
        /// </summary>
        public ScriptMemberSecurity DefaultMemberBindingSecurity = ScriptMemberSecurity.NoAcccess;

        /// <summary>
        /// Holds a list of all binders that can operate on an instance of a given type.
        /// </summary>
        internal readonly Dictionary<Type, TypeBinder> _Binders = new Dictionary<Type, TypeBinder>();
        public IEnumerable<TypeBinder> Binders { get { return _Binders.Values; } }

        /// <summary>
        /// Provides an ID for each registered type binder for internal use (to prevent having to re-construct the type object more than once).
        /// </summary>
        internal IndexedObjectList<Type> _RegisteredTypes = new IndexedObjectList<Type>();

        /// <summary>
        /// Returns true if a binding exists for the specified type.
        /// </summary>
        public bool IsTypeRegistered(Type type) { return _Binders.ContainsKey(type); }

        /// <summary>
        /// Registers binding related schema for the given type on top an 'ObjectTemplate' instance.  If a type already exists that doesn't match the given parameters, it is replaced.
        /// <para>This is done implicitly, so there's no need to register types before binding them; however, explicitly registering a type using this
        /// method gives the user more control over the behaviour of the binding process.</para>
        /// </summary>
        /// <param name="type">The type to create and cache a binding for.</param>
        /// <param name="className">A custom in-script function name for the specified type, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object is bound, only the object instance itself is bound (and not any reference members). If true, then nested object references are included.</param>
        /// <param name="memberSecurity">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public TypeBinder RegisterType(Type type, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        {
            TypeBinder binder = null;
            lock (_Binders) { _Binders.TryGetValue(type, out binder); }
            if (binder != null && (className == null || className == binder.ClassName)) // (note: if the class name changes, we have no choice but to create a new type binder with new templates)
            {
                if (recursive != null)
                    binder._Recursive = recursive.Value;

                if (memberSecurity != null)
                    binder._DefaultMemberSecurity = memberSecurity;

                return binder;
            }
            else
                return new TypeBinder(this, type, className, recursive, memberSecurity);
        }

        /// <summary>
        /// Registers a binding for the given type.  If a type already exists that doesn't match the given parameters, it is replaced.
        /// <para>This is done implicitly, so there's no need to register types before binding them; however, explicitly registering a type using this
        /// method gives the user more control over the behaviour of the binding process.</para>
        /// </summary>
        /// <typeparam name="T">The type to create and cache a binding for.</typeparam>
        /// <param name="className">A custom in-script function name for the specified type, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object is bound, only the object instance itself is bound (and not any reference members). If true, then nested object references are included.</param>
        /// <param name="memberSecurity">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public TypeBinder RegisterType<T>(string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        { return RegisterType(typeof(T), className, recursive, memberSecurity); }

        /// <summary>
        /// Returns the TypeBinder for the given type.  If nothing is found, 'null' will be returned.
        /// </summary>
        /// <param name="type">The type to search for.</param>
        public TypeBinder GetTypeBinder(Type type)
        {
            TypeBinder binder = null;
            lock (_Binders) { _Binders.TryGetValue(type, out binder); }
            return binder;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a binding for a given CLR type to expose it in the JavaScript environment.
        /// The type returned is a function (V8Function) object that can be used to create the underlying type.
        /// <para>Note: Creating bindings is a much slower process than creating your own function templates.</para>
        /// </summary>
        /// <param name="className">A custom type name, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object type is instantiate within JavaScript, only the object instance itself is bound (and not any reference members).
        /// If true, then nested object references are included.</param>
        /// <param name="memberSecurity">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public InternalHandle CreateBinding(Type type, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        {
            var typeBinder = RegisterType(type, className, recursive, memberSecurity);
            return typeBinder.TypeFunction;
        }

        /// <summary>
        /// Creates a binding for a given CLR type to expose it in the JavaScript environment.
        /// The type returned is a function that can be used to create the underlying type.
        /// <para>Note: Creating bindings is a much slower process than creating your own function templates.</para>
        /// <param name="className">A custom type name, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object type is instantiate within JavaScript, only the object instance itself is bound (and not any reference members).
        /// If true, then nested object references are included.</param>
        /// <param name="memberSecurity">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public InternalHandle CreateBinding<T>(string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        {
            return CreateBinding(typeof(T), className, recursive, memberSecurity);
        }

        /// <summary>
        /// Creates a binding for a given CLR object instance to expose it in the JavaScript environment (sub-object members are not bound however).
        /// If the object given is actually a boxed primitive type, then a non-object handle can be returned.
        /// If the given object is not a boxed value, then the handle returned is a handle to an object binder with internal property
        /// accessors for the encapsulated object's public fields, properties, and methods.
        /// <para>Note: Creating bindings can be a much slower process than creating your own 'V8NativeObject' types; however, 
        /// bound types are cached and not created each time for the best efficiency.</para>
        /// </summary>
        /// <param name="obj">The object to create a binder for.</param>
        /// <param name="className">A custom type name, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object type is instantiate within JavaScript, only the object instance itself is bound (and not any reference members).
        /// If true, then nested object references are included.</param>
        /// <param name="memberSecurity">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        /// <param name="initializeBinder">If true (default) then 'IV8NativeObject.Initialize()' is called on the created object before returning.</param>
        public InternalHandle CreateBinding(object obj, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null, bool initializeBinder = true)
        {
            var objType = obj != null ? obj.GetType() : null;

            if (objType == null || obj is IHandleBased)
                return CreateValue(obj, recursive, memberSecurity);

            var typeBinder = RegisterType(objType, className, recursive, memberSecurity);

            return typeBinder.CreateObject(obj, initializeBinder);
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
