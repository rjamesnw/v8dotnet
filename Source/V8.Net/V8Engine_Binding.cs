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
    /// Keeps track of object references based on an array of one or more related types.
    /// The object references are stored based on a tree of nested types for fast dictionary-tree-style lookup.
    /// Currently, this class is used to cache new generic types in the type binder.
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
            TypeLibrary<T> _typeLibrary;
            if (!SubTypes.TryGetValue(type, out _typeLibrary))
            {
                // ... this sub type doesn't exist yet, so add it ...
                _typeLibrary = new TypeLibrary<T>();
                SubTypes[type] = _typeLibrary;
            }
            return _typeLibrary._Set(types, value, depth + 1);
        }

        public T Get(params Type[] types)
        {
            if (types == null) types = new Type[0];
            return _Get(types);
        }
        T _Get(Type[] types, int depth = 0)
        {
            if (depth >= types.Length) return Object;
            Type type = types[depth];
            TypeLibrary<T> _typeLibrary;
            if (!SubTypes.TryGetValue(type, out _typeLibrary))
                return null; // (nothing found)
            return _typeLibrary._Get(types, depth + 1);
        }

        public bool Exists(params Type[] types)
        {
            return Get(types) == null;
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

    //?? Turns out this is not faster.
    //    /// <summary>
    //    /// Keeps track of object references based on an array of one or more related strings.
    //    /// The object references are stored based on a tree of nested string characters for lightning-fast indexed-based lookup.
    //    /// This nested index tree is designed to only take the space needed for the given character ranges, and nothing more.
    //    /// Currently, this class is used to cache type members in the type binder based on their names for fast lookup when the named indexer is invoked.
    //    /// </summary>
    //#if !(V1_1 || V2 || V3 || V3_5)
    //    [DebuggerDisplay("{Object}")]
    //#endif
    //    public class CharIndexLibrary<T> where T : class
    //    {
    //        public CharIndexLibrary<T> Parent;
    //        public readonly char Char;

    //        CharIndexLibrary<T>[] _SubChars = new CharIndexLibrary<T>[0];
    //        List<int> _ValidIndexes = new List<int>(); // (holds a series of valid indexes for the sub chars for quick enumeration)
    //        int _LowerCharOffset = ((int)char.MaxValue) + 1; // The lower char offset is subtracted from the name characters (since the full character range is usually not required).

    //        public T Object;

    //        public CharIndexLibrary() { }
    //        public CharIndexLibrary(CharIndexLibrary<T> parent, char character) { Parent = parent; Char = character; }

    //        public T Set(string name, T value)
    //        {
    //            if (name == null) name = "";
    //            var entry = this;
    //            int i = 0, ofs, endDiff, oldLen;
    //            char c;

    //            while (i < name.Length)
    //            {
    //                c = name[i];
    //                ofs = c - entry._LowerCharOffset;

    //                if (ofs < 0)
    //                {
    //                    if (entry._SubChars.Length == 0)
    //                    {
    //                        Array.Resize<CharIndexLibrary<T>>(ref entry._SubChars, 1); // (first time there is nothing to move, and 'entry._LowerCharOffset' is invalid anyway)
    //                    }
    //                    else
    //                    {
    //                        // ... need to reduce the lower offset to store this character - which means bumping everything up ...
    //                        oldLen = entry._SubChars.Length;
    //                        Array.Resize<CharIndexLibrary<T>>(ref entry._SubChars, entry._SubChars.Length + -ofs); // (extend end)
    //                        Array.Copy(entry._SubChars, 0, entry._SubChars, -ofs, oldLen); // (bump up)
    //                        Array.Clear(entry._SubChars, 0, Math.Min(-ofs, oldLen));
    //                        // ... move up the valid indexes ...
    //                        for (var vi = 0; vi < entry._ValidIndexes.Count; vi++)
    //                            entry._ValidIndexes[vi] += -ofs;
    //                    }

    //                    entry._LowerCharOffset += ofs; // (moved down lower offset)
    //                    entry._ValidIndexes.Add(0);
    //                    entry = entry._SubChars[0] = new CharIndexLibrary<T>(entry, c);
    //                }
    //                else if (ofs >= entry._SubChars.Length)
    //                {
    //                    endDiff = 1 + (ofs - entry._SubChars.Length);
    //                    Array.Resize<CharIndexLibrary<T>>(ref entry._SubChars, entry._SubChars.Length + endDiff); // (extend end)
    //                    entry._ValidIndexes.Add(ofs);
    //                    entry = entry._SubChars[ofs] = new CharIndexLibrary<T>(entry, c);
    //                }
    //                else
    //                {
    //                    var _entry = entry._SubChars[ofs];
    //                    if (_entry == null)
    //                    {
    //                        entry._ValidIndexes.Add(ofs);
    //                        entry._SubChars[ofs] = _entry = new CharIndexLibrary<T>(entry, c);
    //                    }
    //                    entry = _entry;
    //                }

    //                i++;
    //            }

    //            entry.Object = value;

    //            return value;
    //        }

    //        public T Get(string name)
    //        {
    //            if (name == null) name = "";
    //            var entry = this;
    //            int i = 0, ofs;
    //            char c;

    //            while (i < name.Length)
    //            {
    //                c = name[i];
    //                ofs = c - entry._LowerCharOffset;

    //                if (ofs < 0)
    //                {
    //                    return null;
    //                }
    //                else if (ofs >= entry._SubChars.Length)
    //                {
    //                    return null;
    //                }
    //                else
    //                {
    //                    entry = entry._SubChars[ofs];
    //                    if (entry == null)
    //                        return null;
    //                }
    //                i++;
    //            }

    //            return entry.Object;
    //        }

    //        public bool Exists(string name)
    //        {
    //            return Get(name) != null;
    //        }

    //        public IEnumerable<CharIndexLibrary<T>> Items { get { return _GetItems(); } }

    //        IEnumerable<CharIndexLibrary<T>> _GetItems()
    //        {
    //            if (Object != null) yield return this;
    //            for (var i = 0; i < _ValidIndexes.Count; i++)
    //                foreach (var item in _SubChars[_ValidIndexes[i]]._GetItems())
    //                    yield return item;
    //        }

    //        public string Name { get { return (Parent != null && Parent.Parent != null ? Parent.Name : "") + Char; } }
    //    }

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

        /// <summary>
        /// The engine that will own the 'ObjectTemplate' instance.
        /// </summary>
        public readonly V8Engine Engine;

        /// <summary>
        /// A reference to the type binder for the immediate base type inherited by the bound type.
        /// </summary>
        public readonly TypeBinder BaseTypeBinder;

        /// <summary>
        /// Represents a V8 template object used for generating native V8 objects which will correspond to the binding for instances of the underlying type.
        /// </summary>
        public readonly ObjectTemplate InstanceTemplate;

        /// <summary>
        /// Represents a V8 template object used for generating native V8 function objects which will correspond to the binding for the underlying type (for creating new instances within script).
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

#if !(V1_1 || V2 || V3 || V3_5)
        [DebuggerDisplay("{MemberName}x{TotalImmediateMembers}")]
#endif
        internal class _MemberDetails
        {
            public readonly TypeBinder TypeBinder;
            public _MemberDetails(TypeBinder owner) { TypeBinder = owner; }

            public _MemberDetails BaseDetails; // (if set, this is these are the inherited members of the same name represented by this member).

            public MemberInfo FirstMember; // (this is a quick cached reference to the first member in 'Members', which is faster for fields and properties)
            public readonly TypeLibrary<MemberInfo> ImmediateMembers = new TypeLibrary<MemberInfo>(); // (if the count is > 1, then this represents a method or property (indexer) overload)
            public uint TotalImmediateMembers = 1; // (if > 1 then this member is overloaded)

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

            public IEnumerable<MemberInfo> Members
            {
                get
                {
                    foreach (var mi in MemberTypeLibraries.SelectMany(mtl => mtl.Items.Select(i => i.Object)))
                        yield return mi;
                }
            }
            public IEnumerable<MethodInfo> MethodMembers { get { return from m in Members where m.MemberType == MemberTypes.Method select (MethodInfo)m; } }

            public MemberInfo FindMemberByTypes(params Type[] types)
            {
                MemberInfo mi;
                foreach (var mtl in MemberTypeLibraries)
                {
                    mi = mtl.Get(types);
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
            public V8NativeObjectPropertyGetter Getter;
            public V8NativeObjectPropertySetter Setter;
            public V8Function Method;
            //??public TypeLibrary<V8Function> Methods; // (overloads ['SingleMethod' should be null])
            public Handle ValueOverride; // (the value override is a user value that, if exists, overrides the bindings)

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

        public IEnumerable<TypeBinder> BaseBinders { get { var b = BaseTypeBinder; while (b != null) { yield return b; b = b.BaseTypeBinder; } } }

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

            InstanceTemplate = Engine.CreateObjectTemplate<ObjectTemplate>(false);
            InstanceTemplate.RegisterNamedPropertyInterceptors();

            TypeTemplate = Engine.CreateFunctionTemplate<FunctionTemplate>(ClassName);

            // ... extract the members and apply to the templates ...

            _BindInstanceMembers();
            // (note: the instance member reflection includes static members during the process, which is why '_BindTypeMembers()' must be called AFTER) 

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
                foreach (var baseInstanceMemberDetails in baseInstanceMembers)
                {
                    var md = _Members.GetValueOrDefault(baseInstanceMemberDetails.MemberName);
                    if (md == null)
                        _Members[baseInstanceMemberDetails.MemberName] = baseInstanceMemberDetails; // (adopt the member into this instance as well for fast lookup)
                    else
                        if (md.BaseDetails == null && md.TypeBinder == this)
                            md.BaseDetails = baseInstanceMemberDetails; // (the iteration goes up the inheritance chain, so once 'BaseDetails' is set, it is ignored [because the base type binders would have already linked the details])
                }
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        internal _MemberDetails _CreateMemberDetails(string memberName, ScriptMemberSecurity? memberSecurity, MemberInfo memberInfo,
            Func<string, _MemberDetails> getExisting, // (called to check if there's an existing member by the same name)
            Action<_MemberDetails> set) // (called when no member exists and needs to be added [i.e. no existing member details were updated])
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
        internal bool _GetBindingForDataMember(_MemberDetails memberDetails, out V8NativeObjectPropertyGetter getter, out V8NativeObjectPropertySetter setter)
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

        internal bool _GetBindingForField(_MemberDetails memberDetails, out V8NativeObjectPropertyGetter getter, out V8NativeObjectPropertySetter setter)
        {
            string memberName = memberDetails.MemberName;
            FieldInfo fieldInfo = (FieldInfo)memberDetails.FirstMember;
            getter = null;
            setter = null;

            if (TypeTemplate == null) throw new ArgumentNullException("'TypeTemplate' is null.");

            if (string.IsNullOrEmpty(memberName))
                memberName = fieldInfo.Name;

            if (fieldInfo.FieldType == typeof(bool))
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

        V8NativeObjectPropertySetter _CreateSetAccessor(_MemberDetails memberDetails, FieldInfo fieldInfo)
        {
            return (InternalHandle _this, string propertyName, InternalHandle value) =>
            {
                if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                if (!memberDetails.HasSecurityFlags(ScriptMemberSecurity.ReadOnly))
                {
                    if (_this.IsBinder)
                    {
                        fieldInfo.SetValue(_this.BoundObject, new ArgInfo(value, null, fieldInfo.FieldType).ValueOrDefault);
                        return value;
                    }
                    else
                        return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + fieldInfo.Name + ").", JSValueType.ExecutionError);
                }
                return value;
            };
        }

        V8NativeObjectPropertyGetter _CreateGetAccessor<T>(_MemberDetails memberDetails, FieldInfo fieldInfo)
        {
            var isSystemType = BoundType.Namespace == "System";

            if (isSystemType)
                return (InternalHandle _this, string propertyName) =>
                {
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    // (note: it's an error to read some properties in special cases on certain system type instances (such as 'Type.GenericParameterPosition'), so just ignore and return the message on system instances)
                    try
                    {
                        if (_this.IsBinder)
                            return Engine.CreateValue((T)fieldInfo.GetValue(_this.BoundObject));
                        else
                            return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + fieldInfo.Name + ").", JSValueType.ExecutionError);
                    }
                    catch (Exception ex) { return Engine.CreateValue(Exceptions.GetFullErrorMessage(ex)); }
                };
            else
                return (InternalHandle _this, string propertyName) =>
                {
                    if (memberDetails.MemberSecurity < 0) return InternalHandle.Empty;
                    if (_this.IsBinder)
                        return Engine.CreateValue((T)fieldInfo.GetValue(_this.BoundObject));
                    else
                        return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + fieldInfo.Name + ").", JSValueType.ExecutionError);
                };
        }

        V8NativeObjectPropertyGetter _CreateObjectGetAccessor(_MemberDetails memberDetails, FieldInfo fieldInfo)
        {
            var isSystemType = BoundType.Namespace == "System";

            if (isSystemType)
                return (InternalHandle _this, string propertyName) =>
                {
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    // (note: it's an error to read some properties in special cases on certain system type instances (such as 'Type.GenericParameterPosition'), so just ignore and return the message on system instances)
                    try
                    {
                        if (_this.IsBinder)
                            return Engine.CreateValue(fieldInfo.GetValue(_this.BoundObject), _Recursive);
                        else
                            return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + fieldInfo.Name + ").", JSValueType.ExecutionError);
                    }
                    catch (Exception ex) { return Engine.CreateValue(Exceptions.GetFullErrorMessage(ex)); }
                };
            else
                return (InternalHandle _this, string propertyName) =>
                {
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    if (_this.IsBinder)
                        return Engine.CreateValue(fieldInfo.GetValue(_this.BoundObject), _Recursive);
                    else
                        return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + fieldInfo.Name + ").", JSValueType.ExecutionError);
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
        internal bool _GetBindingForProperty(_MemberDetails memberDetails, out V8NativeObjectPropertyGetter getter, out V8NativeObjectPropertySetter setter)
        {
            string memberName = memberDetails.MemberName;
            PropertyInfo propInfo = (PropertyInfo)memberDetails.FirstMember;
            getter = null;
            setter = null;

            if (TypeTemplate == null) throw new ArgumentNullException("'TypeTemplate' is null.");

            if (string.IsNullOrEmpty(memberName))
                memberName = propInfo.Name;

            if (propInfo.PropertyType == typeof(bool))
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

        V8NativeObjectPropertySetter _CreateSetAccessor(_MemberDetails memberDetails, PropertyInfo propertyInfo)
        {
            //??var setMethod = propertyInfo.GetSetMethod();

            return (InternalHandle _this, string propertyName, InternalHandle value) =>
            {
                if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                if (propertyInfo.CanWrite && !memberDetails.HasSecurityFlags(ScriptMemberSecurity.ReadOnly))
                {
                    if (_this.IsBinder)
                        propertyInfo.SetValue(_this.BoundObject, new ArgInfo(value, null, propertyInfo.PropertyType).ValueOrDefault, null);
                    else
                        return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + propertyInfo.Name + ").", JSValueType.ExecutionError);
                }
                return value;
            };
        }

        V8NativeObjectPropertyGetter _CreateGetAccessor<T>(_MemberDetails memberDetails, PropertyInfo propertyInfo)
        {
            var isSystemType = BoundType.Namespace == "System";
            //??var getMethod = propertyInfo.GetGetMethod();

            if (isSystemType)
                return (InternalHandle _this, string propertyName) =>
                {
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    if (propertyInfo.CanRead)
                    {
                        // (note: it's an error to read some properties in special cases on certain system type instances (such as 'Type.GenericParameterPosition'), so just ignore and return the message on system instances)
                        try
                        {
                            if (_this.IsBinder)
                                return Engine.CreateValue((T)propertyInfo.GetValue(_this.BoundObject, null));
                            else
                                return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + propertyInfo.Name + ").", JSValueType.ExecutionError);
                        }
                        catch (Exception ex) { return Engine.CreateValue(Exceptions.GetFullErrorMessage(ex)); }
                    }
                    return InternalHandle.Empty;
                };
            else
                return (InternalHandle _this, string propertyName) =>
                {
                    if (memberDetails.MemberSecurity < 0) return Engine.CreateError("Access denied.", JSValueType.ExecutionError);
                    if (propertyInfo.CanRead)
                    {
                        if (_this.IsBinder)
                            return Engine.CreateValue((T)propertyInfo.GetValue(_this.BoundObject, null));
                        else
                            return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + propertyInfo.Name + ").", JSValueType.ExecutionError);
                    }
                    return InternalHandle.Empty;
                };
        }

        V8NativeObjectPropertyGetter _CreateObjectGetAccessor(_MemberDetails memberDetails, PropertyInfo propertyInfo)
        {
            var isSystemType = BoundType.Namespace == "System";
            //??var getMethod = propertyInfo.GetGetMethod();

            if (isSystemType)
                return (InternalHandle _this, string propertyName) =>
                {
                    if (propertyInfo.CanRead)
                    {
                        // (note: it's an error to read some properties in special cases on certain system type instances (such as 'Type.GenericParameterPosition'), so just ignore and return the message on system instances)
                        try
                        {
                            if (_this.IsBinder)
                                return Engine.CreateValue(propertyInfo.GetValue(_this.BoundObject, null), _Recursive);
                            else
                                return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + propertyInfo.Name + ").", JSValueType.ExecutionError);
                        }
                        catch (Exception ex) { return Engine.CreateValue(Exceptions.GetFullErrorMessage(ex)); }
                    }
                    return InternalHandle.Empty;
                };
            else
                return (InternalHandle _this, string propertyName) =>
                {
                    if (propertyInfo.CanRead)
                    {
                        if (_this.IsBinder)
                            return Engine.CreateValue(propertyInfo.GetValue(_this.BoundObject, null), _Recursive);
                        else
                            return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + propertyInfo.Name + ").", JSValueType.ExecutionError);
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

        InternalHandle _TranslateArguments(_MemberDetails memberDetails, Type[] expectedGenericTypes, InternalHandle[] args, ref uint argOffset,
            out TypeLibrary<MemberInfo> constructedMembers, ref ParameterInfo[] expectedParameters)
        {
            bool isGenericInvocation = ((MethodInfo)memberDetails.FirstMember).IsGenericMethodDefinition;
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

                constructedMembers = memberDetails.ConstructedMemberGroups.Get(genericSystemTypes);

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
                    expectedParameters = ((MethodInfo)constructedMembers.Items.First().Object).GetParameters();
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
                var methodInfo = constructedMembers != null ? (MethodInfo)constructedMembers.Get(systemTypes) : (MethodInfo)memberDetails.FindMemberByTypes(systemTypes); // (note: this expects exact types matches!)
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
        /// The returns function can be used in setting native V8 object properties to function values.
        /// </summary>
        /// <param name="obj">The object that contains the method to bind to, or null if 'methodInfo' is supplied and specifies a static method.</param>
        /// <param name="memberName">Required only if 'methodInfo' is null.</param>
        /// <param name="func">The 'V8Function' wrapper for specified method.</param>
        /// <param name="methods">An array of one MethodInfo, for strong binding, or more than one for dynamic invocation.</param>
        /// <param name="className">An optional name to return when 'valueOf()' is called on a JS object (this defaults to the method's name).</param>
        /// <param name="genericTarget">Allows binding a specific handle to the 'this' of a callback (for static bindings).</param>
        /// <param name="genericInstanceTargetUpdater">Allows binding a specific instance to the 'this' of a callback (for static bindings).</param>
        internal bool _GetBindingForMethod(_MemberDetails memberDetails, out V8Function func, string className = null)
        {
            func = null;

            if (memberDetails == null) throw new ArgumentNullException("'memberDetails' is null or empty.");

            if (TypeTemplate == null) throw new InvalidOperationException("'TypeTemplate' is null.");
            if (BoundType == null) throw new InvalidOperationException("'BoundType' is null.");

            if (string.IsNullOrEmpty(className))
                className = memberDetails.MemberName;

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

            var funcTemplate = Engine.CreateFunctionTemplate(className);

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
                    if (!_this.IsBinder)
                        return Engine.CreateError("The ObjectBinder is missing for function '" + className + "' (" + memberDetails.MemberName + ").", JSValueType.ExecutionError);

                    // ... translate the arguments ...

                    var paResult = _TranslateArguments(memberDetails, expectedGenericTypes, args, ref argOffset, out constructedMembers, ref _expectedParameters);
                    if (!paResult.IsEmpty) return paResult;

                    // ... get the translated arguments into an argument value array in order to invoke the method ...

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
                ArgInfo[] argInfos;
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
                            // ... translate the arguments ...

                            var paResult = _TranslateArguments(_Constructors, null, args, ref argOffset, out constructedMembers, ref _expectedParameters);
                            if (!paResult.IsEmpty) return paResult;

                            // ... get the translated arguments into an argument value array in order to invoke the method ...

                            _CopyArguments(_expectedParameters, convertedArgumentArrayCache, args, ref paramIndex, argOffset, out convertedArguments, out argInfos);

                            // ... invoke the constructor ...

                            handle = Engine.CreateBinding(Activator.CreateInstance(BoundType, convertedArguments), null, _Recursive, _DefaultMemberSecurity);

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

                        handle = Engine.CreateObject(Int32.MinValue + TypeID);
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

            V8NativeObjectPropertyGetter getter;
            V8NativeObjectPropertySetter setter;

            foreach (var details in _FieldDetails(BindingMode.Static))
                if (_GetBindingForDataMember(details, out getter, out setter) && details.MemberSecurity >= 0)
                {
                    TypeFunction.SetAccessor(details.MemberName, getter, setter, (V8PropertyAttributes)details.MemberSecurity); // TODO: Investigate need to add access control value.
                }

            foreach (var details in _PropertyDetails(BindingMode.Static))
                if (_GetBindingForDataMember(details, out getter, out setter) && details.MemberSecurity >= 0)
                {
                    TypeFunction.SetAccessor(details.MemberName, getter, setter, (V8PropertyAttributes)details.MemberSecurity); // TODO: Investigate need to add access control value.
                }

            V8Function func;

            foreach (var details in _MethodDetails(BindingMode.Static))
                if (_GetBindingForMethod(details, out func) && details.MemberSecurity >= 0)
                {
                    TypeFunction.SetProperty(details.MemberName, func, (V8PropertyAttributes)details.MemberSecurity); // TODO: Investigate need to add access control value.
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
        /// <returns>A new 'ObjectBinder' instance you can use when setting properties to the specified object instance.</returns>
        public T CreateObject<T, InstanceType>(InstanceType obj)
            where T : ObjectBinder, new()
            where InstanceType : class
        {
            if (obj == null) throw new ArgumentNullException("obj");

            var objType = obj.GetType();
            if (objType != BoundType)
                throw new InvalidOperationException("'obj' instance of type '" + objType.Name + "' is not compatible with type '" + BoundType.Name + "' as represented by this type binder.");

            object binder;

            binder = InstanceTemplate.CreateObject<T>();
            ((T)binder).Object = obj;

            return (T)binder;
        }

        public ObjectBinder CreateObject(object obj)
        {
            return CreateObject<ObjectBinder, object>(obj);
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

            memberDetails.MemberSecurity = memberSecurity;
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

            memberDetails.MemberSecurity = memberSecurity;
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    /// <summary>
    /// 'ObjectBinder' instances represent JavaScript object properties that are bound to CLR objects or types.
    /// </summary>
    public class ObjectBinder : V8ManagedObject
    {
        // --------------------------------------------------------------------------------------------------------------------

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

        public Type ObjectType
        {
            get { return _ObjectType; }
            private set
            {
                if (value == null) throw new InvalidOperationException("'value' cannot be null.");
                if (_ObjectType == null)
                {
                    _ObjectType = value;
                    TypeBinder = Engine.RegisterType(_ObjectType);
                }
            }
        }
        Type _ObjectType;

        public TypeBinder TypeBinder { get; private set; }

        public ObjectBinder() { _BindingMode = BindingMode.Instance; }

        // --------------------------------------------------------------------------------------------------------------------

        public override void Initialize()
        {
            base.Initialize();

            if (ObjectType == null && _Object != null)
                ObjectType = _Object.GetType();
        }

        // --------------------------------------------------------------------------------------------------------------------

        public override InternalHandle IndexedPropertyGetter(int index)
        {
            if (TypeBinder.Indexer != null && TypeBinder.Indexer.CanRead)
                return Engine.CreateValue(TypeBinder.Indexer.GetValue(Object, new object[] { index }), TypeBinder._Recursive);
            return InternalHandle.Empty;
        }
        public override InternalHandle IndexedPropertySetter(int index, InternalHandle value)
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
            if (memberDetails != null && memberDetails.Accessible) // (no access == undefined) 
            {
                if (memberDetails.ValueOverride != null && !memberDetails.ValueOverride.IsUndefined) return memberDetails.ValueOverride;

                switch (memberDetails.MemberType)
                {
                    case MemberTypes.Field:
                        {
                            var fieldInfo = (FieldInfo)memberDetails.FirstMember;
                            var getter = memberDetails.Getter;
                            if (getter == null)
                            {
                                // .. first time access, create a binding ...
                                V8NativeObjectPropertySetter setter;
                                TypeBinder._GetBindingForDataMember(memberDetails, out getter, out setter);
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
                                V8NativeObjectPropertySetter setter;
                                TypeBinder._GetBindingForDataMember(memberDetails, out getter, out setter);
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
            if (memberDetails != null && !memberDetails.HasSecurityFlags(ScriptMemberSecurity.NoAcccess)) // (no access == undefined)
            {
                if (memberDetails.ValueOverride != null && !memberDetails.ValueOverride.IsUndefined)
                {
                    memberDetails.ValueOverride.Set(value);
                    if (!memberDetails.ValueOverride.IsUndefined) return memberDetails.ValueOverride;
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
                                    V8NativeObjectPropertyGetter getter;
                                    TypeBinder._GetBindingForDataMember(memberDetails, out getter, out setter);
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
                                V8NativeObjectPropertyGetter getter;
                                TypeBinder._GetBindingForDataMember(memberDetails, out getter, out setter);
                                memberDetails.Getter = getter;
                                memberDetails.Setter = setter;
                            }
                            return setter.Invoke(_Handle, propertyName, value);
                        }
                    case MemberTypes.Method:
                        {
                            if (value.IsUndefined)
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
                                if (memberDetails.ValueOverride == null)
                                    return memberDetails.ValueOverride = new Handle(value);
                                else
                                    return memberDetails.ValueOverride.Set(value);
                            }
                        }
                }
            }
            return InternalHandle.Empty;
        }

        public override bool? NamedPropertyDeleter(ref string propertyName)
        {
            var memberDetails = TypeBinder._Members.GetValueOrDefault(propertyName);
            if (memberDetails != null && !memberDetails.HasSecurityFlags(ScriptMemberSecurity.NoAcccess)) // (undefined = no access)
            {
                if (memberDetails.ValueOverride != null && !memberDetails.ValueOverride.IsEmpty)
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
            if (memberDetails != null && !memberDetails.HasSecurityFlags(ScriptMemberSecurity.NoAcccess)) // (undefined = no access)
            {
                return (V8PropertyAttributes)memberDetails.MemberSecurity;
            }
            return null;
        }

        public override InternalHandle NamedPropertyEnumerator()
        {
            return Engine.CreateValue(TypeBinder._Members.Keys);
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
        /// The type returned is an object with property accessors for the object's public fields, properties, and methods.
        /// <para>Note: Creating bindings is a much slower process than creating your own 'V8NativeObject' types.</para>
        /// </summary>
        /// <param name="obj">The object to create a binder for.</param>
        /// <param name="className">A custom type name, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object type is instantiate within JavaScript, only the object instance itself is bound (and not any reference members).
        /// If true, then nested object references are included.</param>
        /// <param name="memberSecurity">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public InternalHandle CreateBinding(object obj, string className = null, bool? recursive = null, ScriptMemberSecurity? memberSecurity = null)
        {
            var objType = obj != null ? obj.GetType() : null;

            if (objType == null || obj is IHandleBased)
                return CreateValue(obj, recursive, memberSecurity);

            var typeBinder = RegisterType(objType, className, recursive, memberSecurity);

            return typeBinder.CreateObject(obj);
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
