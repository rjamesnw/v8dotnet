using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;

#if !(V1_1 || V2 || V3 || V3_5)
using System.Dynamic;
using System.Diagnostics;
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
    [DebuggerDisplay("{Object}")]
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

    /// <summary>
    /// Keeps track of object references based on an array of one or more related strings.
    /// The object references are stored based on a tree of nested string characters for lightning-fast indexed-based lookup.
    /// This nested index tree is designed to only take the space needed for the given character ranges, and nothing more.
    /// Currently, this class is used to cache type members in the type binder based on their names for fast lookup when the named indexer is invoked.
    /// </summary>
#if !(V1_1 || V2 || V3 || V3_5)
    [DebuggerDisplay("{Object}")]
#endif
    public class CharIndexLibrary<T> where T : class
    {
        public CharIndexLibrary<T> Parent;
        public readonly char Char;

        CharIndexLibrary<T>[] _SubChars = new CharIndexLibrary<T>[0];
        List<int> _ValidIndexes = new List<int>(); // (holds a series of valid indexes for the sub chars for quick enumeration)
        int _LowerCharOffset = ((int)char.MaxValue) + 1; // The lower char offset is subtracted from the name characters (since the full character range is usually not required).

        public T Object;

        public CharIndexLibrary() { }
        public CharIndexLibrary(CharIndexLibrary<T> parent, char character) { Parent = parent; Char = character; }

        public T Set(string name, T value)
        {
            if (name == null) name = "";
            var entry = this;
            int i = 0, ofs, endDiff, oldLen;
            char c;

            while (i < name.Length)
            {
                c = name[i];
                ofs = c - entry._LowerCharOffset;

                if (ofs < 0)
                {
                    if (entry._SubChars.Length == 0)
                    {
                        Array.Resize<CharIndexLibrary<T>>(ref entry._SubChars, 1); // (first time there is nothing to move, and 'entry._LowerCharOffset' is invalid anyway)
                    }
                    else
                    {
                        // ... need to reduce the lower offset to store this character - which means bumping everything up ...
                        oldLen = entry._SubChars.Length;
                        Array.Resize<CharIndexLibrary<T>>(ref entry._SubChars, entry._SubChars.Length + -ofs); // (extend end)
                        Array.Copy(entry._SubChars, 0, entry._SubChars, -ofs, oldLen); // (bump up)
                        Array.Clear(entry._SubChars, 0, Math.Min(-ofs, oldLen));
                        // ... move up the valid indexes ...
                        for (var vi = 0; vi < entry._ValidIndexes.Count; vi++)
                            entry._ValidIndexes[vi] += -ofs;
                    }

                    entry._LowerCharOffset += ofs; // (moved down lower offset)
                    entry._ValidIndexes.Add(0);
                    entry = entry._SubChars[0] = new CharIndexLibrary<T>(entry, c);
                }
                else if (ofs >= entry._SubChars.Length)
                {
                    endDiff = 1 + (ofs - entry._SubChars.Length);
                    Array.Resize<CharIndexLibrary<T>>(ref entry._SubChars, entry._SubChars.Length + endDiff); // (extend end)
                    entry._ValidIndexes.Add(ofs);
                    entry = entry._SubChars[ofs] = new CharIndexLibrary<T>(entry, c);
                }
                else
                {
                    var _entry = entry._SubChars[ofs];
                    if (_entry == null)
                    {
                        entry._ValidIndexes.Add(ofs);
                        entry._SubChars[ofs] = _entry = new CharIndexLibrary<T>(entry, c);
                    }
                    entry = _entry;
                }

                i++;
            }

            entry.Object = value;

            return value;
        }

        public T Get(string name)
        {
            if (name == null) name = "";
            var entry = this;
            int i = 0, ofs;
            char c;

            while (i < name.Length)
            {
                c = name[i];
                ofs = c - entry._LowerCharOffset;

                if (ofs < 0)
                {
                    return null;
                }
                else if (ofs >= entry._SubChars.Length)
                {
                    return null;
                }
                else
                {
                    entry = entry._SubChars[ofs];
                    if (entry == null)
                        return null;
                }
                i++;
            }

            return entry.Object;
        }

        public bool Exists(string name)
        {
            return Get(name) != null;
        }

        public IEnumerable<CharIndexLibrary<T>> Items { get { return _GetItems(); } }

        IEnumerable<CharIndexLibrary<T>> _GetItems()
        {
            if (Object != null) yield return this;
            for (var i = 0; i < _ValidIndexes.Count; i++)
                foreach (var item in _SubChars[_ValidIndexes[i]]._GetItems())
                    yield return item;
        }

        public string Name { get { return (Parent != null && Parent.Parent != null ? Parent.Name : "") + Char; } }
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
        /// Attributes to apply to the members for this type binder.
        /// </summary>
        public IEnumerable<KeyValuePair<MemberInfo, V8PropertyAttributes>> Attributes { get { return _Attributes; } }
        readonly Dictionary<MemberInfo, V8PropertyAttributes> _Attributes = new Dictionary<MemberInfo, V8PropertyAttributes>();

        /// <summary>
        /// If true, then nested object references are included, otherwise they are ignored.  By default, the references are ignored for security reasons.
        /// <param>When an object is bound, only the object instance itself is bound (and not any reference members).</param>
        /// </summary>
        public bool Recursive { get { return _Recursive; } }
        internal bool _Recursive;

        /// <summary>
        /// Default member attributes for members that don't have the 'ScriptMember' attribute.
        /// </summary>
        public V8PropertyAttributes DefaultMemberAttributes { get { return _DefaultMemberAttributes; } }
        internal V8PropertyAttributes _DefaultMemberAttributes = V8PropertyAttributes.Undefined;

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
        [DebuggerDisplay("{MemberName}X{TotalMembers}")]
#endif
        internal class _MemberDetails
        {
            public MemberInfo FirstMember; // (this is a quick cached reference to the first member in 'Members', which is faster for fields and properties)
            public readonly TypeLibrary<MemberInfo> Members = new TypeLibrary<MemberInfo>(); // (if the count is > 1, then this represents a method or property (indexer) overload)
            public uint TotalMembers = 1; // (if > 1 then this member is overloaded)
            public IEnumerable<MethodInfo> MethodMembers { get { return from i in Members.Items where i.Object.MemberType == MemberTypes.Method select (MethodInfo)i.Object; } }
            public TypeLibrary<TypeLibrary<MemberInfo>> ConstructedMembers; // (if this member is a generic type, then this is the cache of constructed generic definitions)
            public uint TotalConstructedMembers = 0; // (if > 1 then this member is overloaded)
            public string MemberName; // (might be different from MemberInfo!)
            public MemberTypes MemberType; // (might be different from MemberInfo!)
            public V8PropertyAttributes Attributes;
            public BindingMode BindingMode;
            public V8NativeObjectPropertyGetter Getter;
            public V8NativeObjectPropertySetter Setter;
            public V8Function Method;
            //??public TypeLibrary<V8Function> Methods; // (overloads ['SingleMethod' should be null])
            public Handle ValueOverride; // (the value override is a user value that, if exists, overrides the bindings)
        }
        internal readonly CharIndexLibrary<_MemberDetails> _Members = new CharIndexLibrary<_MemberDetails>(null, default(char));

        IEnumerable<_MemberDetails> _FieldDetails(BindingMode bindingMode)
        { return from md in _Members.Items where (bindingMode == BindingMode.None || md.Object.BindingMode == bindingMode) && md.Object.MemberType == MemberTypes.Field select md.Object; }

        IEnumerable<_MemberDetails> _PropertyDetails(BindingMode bindingMode)
        { return from md in _Members.Items where (bindingMode == BindingMode.None || md.Object.BindingMode == bindingMode) && md.Object.MemberType == MemberTypes.Property select md.Object; }

        IEnumerable<_MemberDetails> _MethodDetails(BindingMode bindingMode)
        { return from md in _Members.Items where (bindingMode == BindingMode.None || md.Object.BindingMode == bindingMode) && md.Object.MemberType == MemberTypes.Method select md.Object; }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Wraps a script value with strong CLR type information for use with generics and method invocation.
        /// </summary>
        public unsafe struct TypeInfo
        {
            public readonly InternalHandle TypeInfoSource;

            public readonly Type Type;
            public readonly Int32 TypeID;

            public readonly object Value;
            public readonly InternalHandle ValueSource;
            public readonly Type OriginalValueType;

            /// <summary>
            /// Returns true if this TypeInfo value has valid type information.  This will be false for empty instances.
            /// </summary>
            public bool IsValid { get { return Type != null; } }

            /// <summary>
            /// Returns true if a valid value exists.  If false is returned, this usually means this is a type-only TypeInfo object.
            /// </summary>
            public bool HasValue { get { return !ValueSource.IsUndefined; } }

            /// <summary>
            /// Returns true if the information was taken from a native TypeInfo object.
            /// </summary>
            public bool IsSourceFromTypeInfoObject { get { return TypeInfoSource.CLRTypeID >= 0; } }

            public object As(Type newtype) { return Types.ChangeType(Value, newtype); }

            public Exception Error; // (only used if part of a list of arguments)
            public bool HasError { get { return Error != null; } }

            public ParameterInfo ExpectedParameter;
            public Type ExpectedType;
            public bool HasDefaultValue { get { return ExpectedParameter != null && ExpectedParameter.DefaultValue != DBNull.Value; } }
            public object DefaultValue { get { return HasDefaultValue ? ExpectedParameter.DefaultValue : null; } }

            /// <summary>
            /// Returns either the underlying TypeInfo strong value, or the default value, whichever is detected first (in that order).
            /// This can be used to pass arguments to methods, where the value is automatically converted if necessary.
            /// </summary>
            public object ValueOrDefault
            {
                get
                {
                    if (Error != null) throw Error; // (error was not dealt with yet!)
                    if ((ValueSource.IsEmpty || ValueSource.IsUndefined) && HasDefaultValue) return DefaultValue;
                    return Value;
                }
            }

            /// <summary>
            /// Converts a given handle to a TypeInfo value, if the handle represents type information, otherwise '{TypeInfo}.IsValid' will be false.
            /// </summary>
            public TypeInfo(InternalHandle handle, ParameterInfo paramInfo = null, Type expectedType = null)
            {
                TypeInfoSource = handle;
                ExpectedParameter = paramInfo;
                ExpectedType = expectedType ?? (paramInfo != null ? paramInfo.ParameterType : null);
                Type = null;
                TypeID = -1;
                Value = null;
                ValueSource = InternalHandle.Empty;
                Error = null;

                if (handle.CLRTypeID >= 0) // (must be an object type with ID <= -2)
                {
                    TypeID = handle.CLRTypeID;

                    ValueSource = handle.GetProperty("$__Value");

                    Value = ValueSource.IsUndefined ? null : ValueSource.Value;

                    // (type is set last, as it is used as the flag to determine if the info is valid)
                    Type = TypeID >= 0 ? handle.Engine._RegisteredTypes[TypeID] : null; // (this will return 'null' if the index is invalid)
                }
                else
                {
                    ValueSource = TypeInfoSource;
                    Value = ValueSource.Value;

                    if (ValueSource.IsBinder) // (type binders are supported for generic method parameters and types [so no need to invoke them as functions to get a strong type!])
                        Type = ValueSource.TypeBinder.BoundType;
                }

                OriginalValueType = Value != null ? Value.GetType() : typeof(object);

                if (Type == null) Type = OriginalValueType;

                // ... step 1: convert the script value to the strong type if necessary ...

                if (!Type.IsAssignableFrom(OriginalValueType))
                    try { Value = Types.ChangeType(Value, Type); }
                    catch (Exception ex) { Error = ex; }

                // ... step2: convert the strong value to the expected type (if given, and if necessary) ...
                // (note: if 'IsGenericParameter' is true, then this type represents a type ONLY, and any value is ignored)

                if (Error == null && ExpectedType != null && !ExpectedType.IsGenericParameter && !ExpectedType.IsAssignableFrom(Type))
                    try { Value = Types.ChangeType(Value, ExpectedType); }
                    catch (Exception ex) { Error = ex; }
            }

            /// <summary>
            /// Returns an array of TypeInfo values for the given handles.
            /// </summary>
            public static TypeInfo[] GetArguments(InternalHandle[] handles, uint handlesOffset = 0, ParameterInfo[] expectedParameters = null)
            {
                var handlesLength = handles.Length - handlesOffset;
                var length = expectedParameters != null ? expectedParameters.Length : handlesLength;

                TypeInfo[] typeInfoItems = new TypeInfo[length];

                for (var i = 0; i < length; i++)
                    typeInfoItems[i] = new TypeInfo(i < handlesLength ? handles[handlesOffset + i] : InternalHandle.Empty, expectedParameters != null ? expectedParameters[i] : null);

                return typeInfoItems;
            }

            /// <summary>
            /// Returns an array of TypeInfo values for the expected types.
            /// </summary>
            public static TypeInfo[] GetTypes(InternalHandle[] handles, uint handlesOffset = 0, Type[] expectedTypes = null)
            {
                var handlesLength = handles.Length - handlesOffset;
                var length = expectedTypes != null ? expectedTypes.Length : handlesLength;

                TypeInfo[] typeInfoItems = new TypeInfo[length];

                for (var i = 0; i < length; i++)
                    typeInfoItems[i] = new TypeInfo(i < handlesLength ? handles[handlesOffset + i] : InternalHandle.Empty, null, expectedTypes != null ? expectedTypes[i] : null);

                return typeInfoItems;
            }

            /// <summary>
            /// Returns an array of TypeInfo values for the expected types.
            /// </summary>
            public static Type[] GetSystemTypes(IEnumerable<TypeInfo> typeInfoList)
            {
                int count = typeInfoList.Count(), i = 0;
                var types = new Type[count];
                var enumerator = typeInfoList.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.HasError) throw enumerator.Current.Error;
                    types[i++] = enumerator.Current.Type;
                }
                return types;
            }

            public static InternalHandle[] GetHandles(TypeInfo[] typeInfoArgs)
            {
                InternalHandle[] handles = new InternalHandle[typeInfoArgs.Length];
                for (int i = 0; i < typeInfoArgs.Length; i++)
                    handles[i] = typeInfoArgs[i].TypeInfoSource;
                return handles;
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        internal TypeBinder(V8Engine engine, Type type, string className = null, bool recursive = false, V8PropertyAttributes defaultMemberAttributes = V8PropertyAttributes.Undefined)
        {

            if (engine == null) throw new ArgumentNullException("engine");
            if (type == null) throw new ArgumentNullException("type");

            Engine = engine;
            BoundType = type;
            TypeID = Engine._RegisteredTypes.Add(type);
            _Recursive = recursive;
            _DefaultMemberAttributes = defaultMemberAttributes;
            _ScriptObjectAttribute = (from a in type.GetCustomAttributes(true) where a is ScriptObject select (ScriptObject)a).FirstOrDefault();

            if (_DefaultMemberAttributes == V8PropertyAttributes.Undefined)
            {
                if (_ScriptObjectAttribute != null)
                    _DefaultMemberAttributes = (V8PropertyAttributes)_ScriptObjectAttribute.Security;
                else
                    _DefaultMemberAttributes = (V8PropertyAttributes)Engine.DefaultMemberBindingSecurity;
            }

            if (className.IsNullOrWhiteSpace())
            {
                if (_ScriptObjectAttribute != null)
                    ClassName = _ScriptObjectAttribute.TypeName;

                if (string.IsNullOrEmpty(ClassName))
                {
                    ClassName = BoundType.Name;

                    if (BoundType.IsGenericType)
                    {
                        ClassName = ClassName.Substring(0, ClassName.LastIndexOf('`'));
                        foreach (var genArgType in BoundType.GetGenericArguments())
                            ClassName += "_" + genArgType.Name;
                    }
                }
            }
            else ClassName = className;

            Engine._Binders[type] = this;

            InstanceTemplate = Engine.CreateObjectTemplate<ObjectTemplate>(false);
            TypeTemplate = Engine.CreateFunctionTemplate<FunctionTemplate>(ClassName);

            _BindInstanceMembers();
            // (note: the instance member reflection includes static members during the process, which is why '_BindTypeMembers()' must be called AFTER) 

            _BindTypeMembers();
        }

        // --------------------------------------------------------------------------------------------------------------------

        void _BindInstanceMembers()
        {
            if (BoundType == null) throw new InvalidOperationException("'BoundType' is null.");

            var members = BoundType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            int mi;
            string memberName;
            ScriptMemberSecurity attributes;
            ScriptMember scriptMemberAttrib;
            _MemberDetails memberDetails;

            for (mi = 0; mi < members.Length; mi++)
            {
                var member = members[mi]; // (need to use 'var' for the lambda closures in 'SetAccessor()' below)

                memberName = member.Name;
                attributes = (ScriptMemberSecurity)_DefaultMemberAttributes;

                scriptMemberAttrib = (from a in member.GetCustomAttributes(true) where a is ScriptMember select (ScriptMember)a).LastOrDefault();

                if (scriptMemberAttrib != null)
                {
                    if (scriptMemberAttrib.Security != (ScriptMemberSecurity)V8PropertyAttributes.Undefined)
                        attributes = scriptMemberAttrib.Security;

                    if (!string.IsNullOrEmpty(scriptMemberAttrib.InScriptName))
                        memberName = scriptMemberAttrib.InScriptName;
                }

                memberDetails = _CreateMemberDetails(memberName, (V8PropertyAttributes)attributes, member,
                    s => _Members.Get(s),
                    md => _Members.Set(md.MemberName, md));
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        internal _MemberDetails _CreateMemberDetails(string memberName, V8PropertyAttributes attribute, MemberInfo memberInfo,
            Func<string, _MemberDetails> getExisting, // (called to check if there's an existing member by the same name)
            Action<_MemberDetails> set) // (called when no member exists and needs to be added [i.e. no existing member details were updated])
        {
            if (memberName.IsNullOrWhiteSpace())
                memberName = memberInfo.Name;

            _MemberDetails memberDetails = null;

            if (memberInfo.MemberType == MemberTypes.Field)
            {
                var fieldInfo = memberInfo as FieldInfo;

                if (!_Recursive && fieldInfo.FieldType.IsClass && fieldInfo.FieldType != typeof(string)) return null; // (don't include nested objects, except strings)

                memberDetails = new _MemberDetails
                {
                    FirstMember = fieldInfo,
                    MemberName = memberName,
                    MemberType = MemberTypes.Field,
                    Attributes = attribute,
                    BindingMode = fieldInfo.IsStatic ? BindingMode.Static : BindingMode.Instance
                };

                memberDetails.Members.Set(fieldInfo, fieldInfo.FieldType);

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

                if (!_Recursive && propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string)) return null; // (don't include nested objects, except strings)

                var getMethod = propertyInfo.GetGetMethod();
                var setMethod = propertyInfo.GetSetMethod();
                var isStatic = getMethod != null && getMethod.IsStatic || setMethod != null && setMethod.IsStatic;

                memberDetails = new _MemberDetails
                {
                    FirstMember = propertyInfo,
                    MemberName = memberName,
                    MemberType = MemberTypes.Property,
                    Attributes = attribute,
                    BindingMode = isStatic ? BindingMode.Static : BindingMode.Instance
                };

                memberDetails.Members.Set(propertyInfo, propertyInfo.PropertyType);

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

                    memberDetails = existingMemberDetails ?? new _MemberDetails
                   {
                       FirstMember = methodInfo,
                       MemberName = memberName,
                       MemberType = MemberTypes.Method,
                       BindingMode = methodInfo.IsStatic ? BindingMode.Static : BindingMode.Instance
                   };

                    if (attribute >= 0 && memberDetails.Attributes >= 0)
                        memberDetails.Attributes |= attribute; // (combine all security attributes for all overloaded members, if any)
                    else
                        memberDetails.Attributes = attribute;

                    // ... register the method based on number and type of expected parameters ...

                    var types = Arrays.Concat(methodInfo.GetGenericArguments(), methodInfo.GetParameters().Select(p => p.ParameterType).ToArray());

                    memberDetails.Members.Set(methodInfo, types);

                    //??if (memberDetails.Methods == null)
                    //??    memberDetails.Methods = new TypeLibrary<V8Function>();
                    //?? ('Methods' will be populated when accessed for the first time)

                    if (existingMemberDetails == null)
                        set(memberDetails);
                    else
                        memberDetails.TotalMembers++;
                }
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
                getter = _CreateGetAccessor<bool>(fieldInfo);

            else if (fieldInfo.FieldType == typeof(byte))
                getter = _CreateGetAccessor<byte>(fieldInfo);
            else if (fieldInfo.FieldType == typeof(sbyte))
                getter = _CreateGetAccessor<sbyte>(fieldInfo);

            else if (fieldInfo.FieldType == typeof(Int16))
                getter = _CreateGetAccessor<Int16>(fieldInfo);
            else if (fieldInfo.FieldType == typeof(UInt16))
                getter = _CreateGetAccessor<UInt16>(fieldInfo);

            else if (fieldInfo.FieldType == typeof(Int32))
                getter = _CreateGetAccessor<Int32>(fieldInfo);
            else if (fieldInfo.FieldType == typeof(UInt32))
                getter = _CreateGetAccessor<UInt32>(fieldInfo);

            else if (fieldInfo.FieldType == typeof(Int64))
                getter = _CreateGetAccessor<Int64>(fieldInfo);
            else if (fieldInfo.FieldType == typeof(UInt64))
                getter = _CreateGetAccessor<UInt64>(fieldInfo);

            else if (fieldInfo.FieldType == typeof(Single))
                getter = _CreateGetAccessor<Single>(fieldInfo);
            else if (fieldInfo.FieldType == typeof(float))
                getter = _CreateGetAccessor<float>(fieldInfo);
            else if (fieldInfo.FieldType == typeof(double))
                getter = _CreateGetAccessor<double>(fieldInfo);

            else if (fieldInfo.FieldType == typeof(string))
                getter = _CreateGetAccessor<string>(fieldInfo);
            else if (fieldInfo.FieldType == typeof(char))
                getter = _CreateGetAccessor<char>(fieldInfo);

            else if (fieldInfo.FieldType == typeof(DateTime))
                getter = _CreateGetAccessor<DateTime>(fieldInfo);
            else if (fieldInfo.FieldType == typeof(TimeSpan))
                getter = _CreateGetAccessor<TimeSpan>(fieldInfo);

            else if (fieldInfo.FieldType.IsEnum)
                getter = _CreateGetAccessor<Int32>(fieldInfo);

            else if (_Recursive)
            {
                // ... this type is unknown, but recursive is set, so register the type implicitly and continue ...
                Engine.RegisterType(fieldInfo.FieldType);
                getter = _CreateObjectGetAccessor(fieldInfo);
            }
            else return false;

            setter = _CreateSetAccessor(fieldInfo);

            return true;
        }

        V8NativeObjectPropertySetter _CreateSetAccessor(FieldInfo fieldInfo)
        {
            return (InternalHandle _this, string propertyName, InternalHandle value) =>
            {
                if (_this.IsBinder)
                {
                    fieldInfo.SetValue(_this.BoundObject, new TypeInfo(value, null, fieldInfo.FieldType).ValueOrDefault);
                    return value;
                }
                else
                    return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + fieldInfo.Name + ").", JSValueType.ExecutionError);
            };
        }

        V8NativeObjectPropertyGetter _CreateGetAccessor<T>(FieldInfo fieldInfo)
        {
            var isSystemType = BoundType.Namespace == "System";

            if (isSystemType)
                return (InternalHandle _this, string propertyName) =>
                {
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
                    if (_this.IsBinder)
                        return Engine.CreateValue((T)fieldInfo.GetValue(_this.BoundObject));
                    else
                        return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + fieldInfo.Name + ").", JSValueType.ExecutionError);
                };
        }

        V8NativeObjectPropertyGetter _CreateObjectGetAccessor(FieldInfo fieldInfo)
        {
            var isSystemType = BoundType.Namespace == "System";

            if (isSystemType)
                return (InternalHandle _this, string propertyName) =>
                {
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
                getter = _CreateGetAccessor<bool>(propInfo);

            else if (propInfo.PropertyType == typeof(byte))
                getter = _CreateGetAccessor<byte>(propInfo);
            else if (propInfo.PropertyType == typeof(sbyte))
                getter = _CreateGetAccessor<sbyte>(propInfo);

            else if (propInfo.PropertyType == typeof(Int16))
                getter = _CreateGetAccessor<Int16>(propInfo);
            else if (propInfo.PropertyType == typeof(UInt16))
                getter = _CreateGetAccessor<UInt16>(propInfo);

            else if (propInfo.PropertyType == typeof(Int32))
                getter = _CreateGetAccessor<Int32>(propInfo);
            else if (propInfo.PropertyType == typeof(UInt32))
                getter = _CreateGetAccessor<UInt32>(propInfo);

            else if (propInfo.PropertyType == typeof(Int64))
                getter = _CreateGetAccessor<Int64>(propInfo);
            else if (propInfo.PropertyType == typeof(UInt64))
                getter = _CreateGetAccessor<UInt64>(propInfo);

            else if (propInfo.PropertyType == typeof(Single))
                getter = _CreateGetAccessor<Single>(propInfo);
            else if (propInfo.PropertyType == typeof(float))
                getter = _CreateGetAccessor<float>(propInfo);
            else if (propInfo.PropertyType == typeof(double))
                getter = _CreateGetAccessor<double>(propInfo);

            else if (propInfo.PropertyType == typeof(string))
                getter = _CreateGetAccessor<string>(propInfo);
            else if (propInfo.PropertyType == typeof(char))
                getter = _CreateGetAccessor<char>(propInfo);

            else if (propInfo.PropertyType == typeof(DateTime))
                getter = _CreateGetAccessor<DateTime>(propInfo);
            else if (propInfo.PropertyType == typeof(TimeSpan))
                getter = _CreateGetAccessor<TimeSpan>(propInfo);

            else if (propInfo.PropertyType.IsEnum)
                getter = _CreateGetAccessor<Int32>(propInfo);

            else if (_Recursive)
            {
                // ... this type is unknown, but recursive is set, so register the type implicitly and continue ...
                Engine.RegisterType(propInfo.PropertyType);
                getter = _CreateObjectGetAccessor(propInfo);
            }
            else return false;

            setter = _CreateSetAccessor(propInfo);

            return true;
        }

        V8NativeObjectPropertySetter _CreateSetAccessor(PropertyInfo propertyInfo)
        {
            //??var setMethod = propertyInfo.GetSetMethod();

            return (InternalHandle _this, string propertyName, InternalHandle value) =>
            {
                if (propertyInfo.CanWrite)
                {
                    if (_this.IsBinder)
                        propertyInfo.SetValue(_this.BoundObject, new TypeInfo(value, null, propertyInfo.PropertyType).ValueOrDefault, null);
                    else
                        return Engine.CreateError("The ObjectBinder is missing for property '" + propertyName + "' (" + propertyInfo.Name + ").", JSValueType.ExecutionError);
                }
                return value;
            };
        }

        V8NativeObjectPropertyGetter _CreateGetAccessor<T>(PropertyInfo propertyInfo)
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

        V8NativeObjectPropertyGetter _CreateObjectGetAccessor(PropertyInfo propertyInfo)
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
        internal bool _GetBindingForMethod(_MemberDetails memberDetails, out V8Function func, string className = null, Func<InternalHandle> genericTarget = null)
        {
            func = null;

            if (memberDetails == null) throw new ArgumentNullException("'memberDetails' is null or empty.");

            if (TypeTemplate == null) throw new InvalidOperationException("'TypeTemplate' is null.");
            if (BoundType == null) throw new InvalidOperationException("'BoundType' is null.");

            if (string.IsNullOrEmpty(className))
                className = memberDetails.MemberName;

            //??var methods = memberDetails.Members.Items.Select(i => i.Object).Cast<MethodInfo>().ToArray();

            //??bool[] hasVariableParams = new bool[methods.Length]; //?? Needed?
            //for (var mi = 0; mi < methods.Length; mi++)
            //{
            //    var parameters = methods[mi].GetParameters();
            //    hasVariableParams[mi] = parameters.Length > 0 && parameters[parameters.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);
            //}

            MethodInfo soloMethod = memberDetails.TotalMembers == 1 ? (MethodInfo)memberDetails.FirstMember : null;
            var expectedParameters = soloMethod != null ? soloMethod.GetParameters() : null;
            var expectedGenericTypes = soloMethod != null && soloMethod.IsGenericMethod ? soloMethod.GetGenericArguments() : null;

            Dictionary<int, object[]> convertedArgumentArrayCache = new Dictionary<int, object[]>(); // (a cache of argument arrays based on argument length to use for calling overloaded methods)
            object[] convertedArguments;

            // ... if we know the number of parameters (only one method) then create the argument array now ...
            if (expectedParameters != null)
                convertedArgumentArrayCache[expectedParameters.Length] = new object[expectedParameters.Length];

            var funcTemplate = Engine.CreateFunctionTemplate(className);

            func = funcTemplate.GetFunctionObject<V8Function>((V8Engine engine, bool isConstructCall, InternalHandle _this, InternalHandle[] args) =>
            {
                bool isGenericInvocation = ((MethodInfo)memberDetails.FirstMember).IsGenericMethodDefinition;
                InternalHandle[] typeArgs;
                TypeInfo[] genericTypeInfoArgs;
                TypeInfo[] typeInfoArgs;
                int paramIndex = -1;
                uint argOffset = 0;
                TypeInfo tInfo;

                try
                {
                    TypeLibrary<MemberInfo> members = memberDetails.Members;

                    if (!_this.IsBinder)
                        return Engine.CreateError("The ObjectBinder is missing for function '" + className + "' (" + memberDetails.MemberName + ").", JSValueType.ExecutionError);

                    if (isGenericInvocation)
                    {
                        // ... first argument should be an array of types ...
                        if (args.Length == 0 || !args[0].IsArray || args[0].ArrayLength == 0)
                            return Engine.CreateError("No types given: The first argument of a generic method must be an array of types.", JSValueType.ExecutionError);

                        typeArgs = new InternalHandle[args[0].ArrayLength]; // TODO: Create a faster way to extract an array of internal handles.

                        for (int i = 0; i < args[0].ArrayLength; i++)
                            typeArgs[i] = args[0].GetProperty(i);

                        genericTypeInfoArgs = TypeInfo.GetTypes(typeArgs, 0, expectedGenericTypes);
                        var genericSystemTypes = TypeInfo.GetSystemTypes(genericTypeInfoArgs);

                        if (memberDetails.ConstructedMembers == null)
                            memberDetails.ConstructedMembers = new TypeLibrary<TypeLibrary<MemberInfo>>();

                        TypeLibrary<MemberInfo> constructedMethodInfos = memberDetails.ConstructedMembers.Get(genericSystemTypes);

                        if (constructedMethodInfos == null)
                        {
                            // ... there can be multiple generic methods (overloads) with different parameters types, so we need to generate and cache each one to see the affect on the parameters ...

                            var genericMethodInfos = memberDetails.Members.Items.Select(i => (MethodInfo)i.Object).ToArray();
                            constructedMethodInfos = new TypeLibrary<MemberInfo>();
                            MethodInfo constructedMethod = null;

                            for (int i = 0; i < genericMethodInfos.Length; i++)
                            {
                                constructedMethod = genericMethodInfos[i].MakeGenericMethod(genericSystemTypes);
                                constructedMethodInfos.Set(constructedMethod, constructedMethod.GetParameters().Select(p => p.ParameterType).ToArray());
                                memberDetails.TotalConstructedMembers++;
                            }

                            // ... cache the array of constructed generic methods (any overloads with the same generic parameters), which will exist in a type
                            // library for quick lookup the next time this given types are supplied ...
                            memberDetails.ConstructedMembers.Set(constructedMethodInfos, genericSystemTypes);

                            if (memberDetails.TotalConstructedMembers == 1)
                            {
                                // ... update the closure details for the solo method ...
                                soloMethod = constructedMethod;
                                expectedParameters = soloMethod.GetParameters();
                            }

                        }

                        members = constructedMethodInfos;

                        // ... at this point the 'constructedMethodInfos' will have an array of methods that match the types supplied for this generic method call.
                        // Next, the arguments ????

                        argOffset = 1; // (skip first array argument)
                    }

                    /* ... get the converted parameters ... */
                    {
                        convertedArguments = null;

                        typeInfoArgs = TypeInfo.GetArguments(args, argOffset, expectedParameters);

                        // ... create/grow the converted arguments array if necessary ...
                        if (!convertedArgumentArrayCache.TryGetValue(typeInfoArgs.Length, out convertedArguments) || convertedArguments.Length < typeInfoArgs.Length)
                            convertedArgumentArrayCache[typeInfoArgs.Length] = convertedArguments = new object[typeInfoArgs.Length]; // (array is too small, so discard

                        for (paramIndex = 0; paramIndex < typeInfoArgs.Length; paramIndex++)
                        {
                            tInfo = typeInfoArgs[paramIndex];

                            if (tInfo.HasError) throw tInfo.Error;

                            convertedArguments[paramIndex] = tInfo.ValueOrDefault;
                        }

                        paramIndex = -1;
                    }

                    // ... invoke the method ...

                    if (soloMethod != null)
                    {
                        var result = soloMethod.Invoke(_this.BoundObject, convertedArguments);
                        return soloMethod.ReturnType == typeof(void) ? InternalHandle.Empty : Engine.CreateValue(result, _Recursive);
                    }
                    else // ... more than one method exists (overloads) ..
                    {
                        var systemTypes = TypeInfo.GetSystemTypes(typeInfoArgs);
                        var methodInfo = (MethodInfo)members.Get(systemTypes); // (note: this expects exact types matches!)
                        if (methodInfo == null)
                            throw new TargetInvocationException("There is no overloaded method with the given parameter types ("
                                + String.Join(", ", (from t in systemTypes select GetTypeName(t)).ToArray()) + ").", null);
                        var result = methodInfo.Invoke(_this.BoundObject, convertedArguments);
                        return methodInfo.ReturnType == typeof(void) ? InternalHandle.Empty : Engine.CreateValue(result, _Recursive);
                    }
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

        void _BindTypeMembers()
        {
            TypeFunction = TypeTemplate.GetFunctionObject<TypeBinderFunction>((engine, isConstructCall, _this, args) =>
            {
                InternalHandle handle;

                if (isConstructCall)
                    try
                    {
                        if (BoundType.IsAbstract)
                            handle = Engine.CreateError("The CLR type '" + BoundType.Name + "' is abstract - you cannot create instances from abstract types.", JSValueType.ExecutionError);
                        else
                        {
                            var _args = new object[args.Length];
                            for (var i = 0; i < args.Length; i++)
                                _args[i] = args[i].Value;

                            handle = Engine.CreateBinding(Activator.CreateInstance(BoundType, _args), null, _Recursive, _DefaultMemberAttributes);
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
                if (_GetBindingForDataMember(details, out getter, out setter) && details.Attributes >= 0)
                {
                    TypeFunction.SetAccessor(details.MemberName, getter, setter, details.Attributes); // TODO: Investigate need to add access control value.
                }

            foreach (var details in _PropertyDetails(BindingMode.Static))
                if (_GetBindingForDataMember(details, out getter, out setter) && details.Attributes >= 0)
                {
                    TypeFunction.SetAccessor(details.MemberName, getter, setter, details.Attributes); // TODO: Investigate need to add access control value.
                }

            V8Function func;

            foreach (var details in _MethodDetails(BindingMode.Static))
                if (_GetBindingForMethod(details, out func) && details.Attributes >= 0)
                {
                    TypeFunction.SetProperty(details.MemberName, func, details.Attributes); // TODO: Investigate need to add access control value.
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
                TypeBinder.Indexer.SetValue(_Object, new TypeBinder.TypeInfo(value, null, TypeBinder.Indexer.PropertyType).ValueOrDefault, new object[] { index });
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
            var memberDetails = TypeBinder._Members.Get(propertyName);
            if (memberDetails != null && memberDetails.Attributes != V8PropertyAttributes.Undefined) // (undefined = no access)
            {
                if (!memberDetails.ValueOverride.IsUndefined) return memberDetails.ValueOverride;

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
                            getter.Invoke(_Handle, propertyName);
                            break;
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
                            getter.Invoke(_Handle, propertyName);
                            break;
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
            var memberDetails = TypeBinder._Members.Get(propertyName);
            if (memberDetails != null && memberDetails.Attributes != V8PropertyAttributes.Undefined) // (undefined = no access)
            {
                if (!memberDetails.ValueOverride.IsUndefined)
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
                                setter.Invoke(_Handle, propertyName, value);
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
                            setter.Invoke(_Handle, propertyName, value);
                            break;
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
            var memberDetails = TypeBinder._Members.Get(propertyName);
            if (memberDetails != null && memberDetails.Attributes != V8PropertyAttributes.Undefined) // (undefined = no access)
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
            var memberDetails = TypeBinder._Members.Get(propertyName);
            if (memberDetails != null && memberDetails.Attributes != V8PropertyAttributes.Undefined) // (undefined = no access)
            {
                return memberDetails.Attributes;
            }
            return null;
        }

        public override InternalHandle NamedPropertyEnumerator()
        {
            string[] memberNames = TypeBinder._Members.Items.Select(i => i.Object.MemberName).ToArray();
            return Engine.CreateValue(memberNames);
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
        /// <param name="defaultMemberAttributes">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public TypeBinder RegisterType(Type type, string className = null, bool recursive = false, V8PropertyAttributes defaultMemberAttributes = V8PropertyAttributes.Undefined)
        {
            TypeBinder binder = null;
            lock (_Binders) { _Binders.TryGetValue(type, out binder); }
            if (binder != null && binder._Recursive == recursive && binder._DefaultMemberAttributes == defaultMemberAttributes)
                return binder;
            else
                return new TypeBinder(this, type, className, recursive, defaultMemberAttributes);
        }

        //public TypeBinder RenameTypeMember(string newName, MemberInfo memberInfo)
        //{
        //    if (newName.IsNullOrWhiteSpace()) throw new ArgumentNullException("newName");
        //    if (memberInfo == null) throw new ArgumentNullException("memberInfo");

        //    TypeBinder binder = null;
        //    lock (_Binders) { _Binders.TryGetValue(memberInfo.DeclaringType, out binder); }
        //    if (binder == null) binder = RegisterType(memberInfo.DeclaringType);

        //    // ... locate member information and change the key ...

        //    switch (memberInfo.MemberType)
        //    {
        //        case MemberTypes.
        //    }
        //}

        //public TypeBinder RenameTypeMember(Type type, string newName, string memberName, params Type[] parameterTypes)
        //{

        //}

        /// <summary>
        /// Registers a binding for the given type.  If a type already exists that doesn't match the given parameters, it is replaced.
        /// <para>This is done implicitly, so there's no need to register types before binding them; however, explicitly registering a type using this
        /// method gives the user more control over the behaviour of the binding process.</para>
        /// </summary>
        /// <typeparam name="T">The type to create and cache a binding for.</typeparam>
        /// <param name="className">A custom in-script function name for the specified type, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object is bound, only the object instance itself is bound (and not any reference members). If true, then nested object references are included.</param>
        /// <param name="defaultMemberAttributes">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public TypeBinder RegisterType<T>(string className = null, bool recursive = false, V8PropertyAttributes defaultMemberAttributes = V8PropertyAttributes.Undefined)
        { return RegisterType(typeof(T), className, recursive, defaultMemberAttributes); }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a binding for a given CLR type to expose it in the JavaScript environment.
        /// The type returned is a function (V8Function) object that can be used to create the underlying type.
        /// <para>Note: Creating bindings is a much slower process than creating your own function templates.</para>
        /// </summary>
        /// <param name="className">A custom type name, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object type is instantiate within JavaScript, only the object instance itself is bound (and not any reference members).
        /// <param name="memberAttributes">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        /// If true, then nested object references are included.</param>
        public InternalHandle CreateBinding(Type type, string className = null, bool recursive = false, V8PropertyAttributes memberAttributes = V8PropertyAttributes.Undefined)
        {
            var typeBinder = RegisterType(type, className, recursive, memberAttributes);
            return typeBinder.TypeFunction;
        }

        /// <summary>
        /// Creates a binding for a given CLR type to expose it in the JavaScript environment.
        /// The type returned is a function that can be used to create the underlying type.
        /// <para>Note: Creating bindings is a much slower process than creating your own function templates.</para>
        /// <param name="className">A custom type name, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">When an object type is instantiate within JavaScript, only the object itself is bound. If true, then nested object based properties are included.</param>
        /// <param name="memberAttributes">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public InternalHandle CreateBinding<T>(string className = null, bool recursive = false, V8PropertyAttributes memberAttributes = V8PropertyAttributes.Undefined)
        {
            return CreateBinding(typeof(T), className, recursive, memberAttributes);
        }

        /// <summary>
        /// Creates a binding for a given CLR object instance to expose it in the JavaScript environment (sub-object members are not bound however).
        /// The type returned is an object with property accessors for the object's public fields, properties, and methods.
        /// <para>Note: Creating bindings is a much slower process than creating your own 'V8NativeObject' types.</para>
        /// </summary>
        /// <param name="obj">The object to create a binder for.</param>
        /// <param name="className">A custom type name, or 'null' to use either the type name as is (the default), or any existing 'ScriptObject' attribute name.</param>
        /// <param name="recursive">For object types, if true, then nested objects are included, otherwise only the object itself is bound and returned.</param>
        /// <param name="memberAttributes">Default member attributes for members that don't have the 'ScriptMember' attribute.</param>
        public InternalHandle CreateBinding(object obj, string className = null, bool recursive = false, V8PropertyAttributes memberAttributes = V8PropertyAttributes.Undefined)
        {
            var objType = obj != null ? obj.GetType() : null;

            if (objType == null || !objType.IsClass || obj is IHandleBased)
                return CreateValue(obj, recursive, memberAttributes);

            var typeBinder = RegisterType(objType, className, recursive, memberAttributes);

            return typeBinder.CreateObject(obj);
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================
}
