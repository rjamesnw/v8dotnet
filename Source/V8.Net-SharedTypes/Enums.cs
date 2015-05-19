using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace V8.Net
{
    // ========================================================================================================================

    /// <summary>
    /// Flags that describe JavaScript properties.  They must be 'OR'd together as needed.
    /// </summary>
    [Flags]
    public enum V8PropertyAttributes
    {
        /// <summary>
        /// No valid attribute exists (unlike 'None', which *explicitly* defines that no attributes are set).
        /// The native PROXY (not V8) will interpret this to mean "let V8 continue on to the default behavior".
        /// <para>Warning: This cannot be bitwise "OR"ed with the other enum values. Also, this is not supported on the native V8 side.</para>
        /// </summary>
        Undefined = -1,

        /// <summary>
        /// No attribute is set - keeps the default behaviour of all attributes NOT set.
        /// <para>Note: When checking object properties for attributes, V8 will return 'None' if a property doesn't exist. 'Undefined' is for V8.Net usage only
        /// (used with interceptor callbacks/hooks).</para>
        /// </summary>
        None = 0,

        /// <summary>
        /// The property can only be read from.
        /// </summary>
        ReadOnly = 1 << 0,

        /// <summary>
        /// The property is visible, but should not be returned during enumeration.
        /// </summary>
        DontEnum = 1 << 1,

        /// <summary>
        /// The property cannot be deleted.
        /// </summary>
        DontDelete = 1 << 2,

        /// <summary>
        /// This is equal to "ReadOnly | DontDelete", and is a V8.NET specific attribute name.
        /// It's here because locking down properties is a common operation when adding some fixed global types and objects.
        /// </summary>
        Locked = ReadOnly | DontDelete
    };

    // ========================================================================================================================

    /// <summary>
    /// V8 Access control specification flags.  If some accessors should be accessible across native V8 contexts, then these accessors need an explicit access
    /// control parameter set which specifies the kind of cross-context access that should be allowed.
    /// </summary>
    [Flags]
    public enum V8AccessControl
    {
        /// <summary>
        /// No valid access value exists (unlike 'Default', which *explicitly* defines to use the default behavior).
        /// <para>Note: This value is used internally by V8.NET and is not part of the native V8 system.</para>
        /// </summary>
        Undefined = -1,

        /// <summary>
        /// Keeps the default behaviour of "no flags set".
        /// </summary>
        Default = 0,

        /// <summary>
        /// The property can be read from all contexts.
        /// </summary>
        AllCanRead = 1 << 0,

        /// <summary>
        /// The property can be written to by all contexts.
        /// </summary>
        AllCanWrite = 1 << 1,

        /// <summary>
        /// The accessor cannot be replaced by other contexts.
        /// </summary>
        ProhibitsOverwriting = 1 << 2
    };

    // ========================================================================================================================

    /// <summary>
    /// The type of JavaScript values marshalled from the native side.
    /// </summary>
    public enum JSValueType : int
    {
        /// <summary>
        /// An error has occurred while attempting to execute the compiled script.
        /// </summary>
        ExecutionError = -2,

        /// <summary>
        /// An error has occurred compiling the script (usually a syntax error).
        /// </summary>
        CompilerError = -3,

        /// <summary>
        /// An internal error has occurred (before or after script execution).
        /// </summary>
        InternalError = -1,

        /// <summary>
        /// The value has not been read yet from the native V8 handle, so a call to 'V8NetProxy.UpdateHandleValue(_HandleProxy)' is required.
        /// </summary>
        Uninitialized = 0,

        /// <summary>
        /// The value is undefined (no value set).  This is NOT the same as 'null'.
        /// </summary>
        Undefined,

        /// <summary>
        /// The handle proxy represents pre-compiled JavaScript.
        /// </summary>
        Script,

        /// <summary>
        /// The value is null (a null object reference).  This is NOT the same as 'undefined' (no value set).
        /// </summary>
        Null,

        /// <summary>
        /// The value is a Boolean, as supported within JavaScript for true/false conditions.
        /// </summary>
        Bool,

        /// <summary>
        /// The value is a Boolean object (object reference), as supported within JavaScript when executing "new Boolean()".
        /// </summary>
        BoolObject,

        /// <summary>
        /// The value is a 32-bit Integer, as supported within JavaScript for bit operations.
        /// </summary>
        Int32,

        /// <summary>
        /// The value is a JavaScript 64-bit number.
        /// </summary>
        Number,

        /// <summary>
        /// The value is a JavaScript 64-bit Number object (object reference), as supported within JavaScript when executing "new Number()".
        /// </summary>
        NumberObject,

        /// <summary>
        /// The value is a UTF16 string.
        /// </summary>
        String,

        /// <summary>
        /// The value is a JavaScript String object (object reference), as supported within JavaScript when executing "new String()".
        /// </summary>
        StringObject,

        /// <summary>
        /// The value is a non-value (object reference).
        /// </summary>
        Object,

        /// <summary>
        /// The value is a reference to a JavaScript function (object reference).
        /// </summary>
        Function,

        /// <summary>
        /// The date value is the number of milliseconds since epoch [1970-01-01 00:00:00 UTC+00] (a double value stored in 'Number').
        /// </summary>
        Date,

        /// <summary>
        /// The value proxy represents a JavaScript array of various values.
        /// </summary>
        Array,

        /// <summary>
        /// The value proxy represents a JavaScript regular expression object (object reference).
        /// </summary>
        RegExp,
    }

    // ========================================================================================================================

    /// <summary>
    /// Type of native proxy object (for native class instances only).
    /// </summary>
    public enum ProxyObjectType
    {
        Undefined,
        ObjectTemplateProxyClass,
        FunctionTemplateProxyClass,
        V8EngineProxyClass,
        HandleProxyClass
    };

    // ========================================================================================================================
}
