using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace V8.Net
{
    // ========================================================================================================================

    /// <summary>
    /// Flags which Determine the script accessibility.
    /// </summary>
    public enum ScriptMemberSecurity
    {
        /// <summary>
        /// Used internally to prevent access to a script member.
        /// </summary>
        NoAcccess = -1,

        /// <summary>
        /// If this flag is set, then the property can be read and/or written to.
        /// <para>Note: This is the default behaviour, and doesn't need to be explicitly set.</para>
        /// </summary>
        ReadWrite = V8PropertyAttributes.None,

        /// <summary>
        /// If this flag is set, then the Property can only be read (takes precedence over 'ReadWrite').
        /// </summary>
        ReadOnly = V8PropertyAttributes.ReadOnly,

        /// <summary>
        /// If this flag is set, then the property is hidden from enumeration (but not from access).
        /// </summary>
        Hidden = V8PropertyAttributes.DontEnum,

        /// <summary>
        /// If this flag is set, then the property cannot be deleted (from within the script).
        /// </summary>
        Permanent = V8PropertyAttributes.DontDelete,

        /// <summary>
        /// If this flag is set, then the property cannot be deleted OR overwritten (from within the script; this is equal to setting both 'Permanent|ReadOnly' flags).
        /// </summary>
        Locked = V8PropertyAttributes.Locked
    };

    // ========================================================================================================================

    /// <summary>
    /// By default, public class members are NOT accessible by script for security reasons (unless the 'ScriptObject' attribute is used).
    /// This attribute allows controlling how class members are exposed to the scripting environment.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ScriptMember : Attribute
    {
        // --------------------------------------------------------------------------------------------------------------------

        public string InScriptName { get; private set; }

        public ScriptMemberSecurity Security { get { return _Security; } private set { _Security = value; } }
        ScriptMemberSecurity _Security = ScriptMemberSecurity.ReadWrite;

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Allows controlling how this class member is exposed to the scripting environment.
        /// </summary>
        /// <param name="inScriptName">The type name to expose to the scripting environment for this member (default is null/empty, which means use the member name as is).</param>
        /// <param name="security">The script access security for this member.</param>
        public ScriptMember(string inScriptName = null, ScriptMemberSecurity security = ScriptMemberSecurity.ReadWrite)
        {
            InScriptName = inScriptName;
            Security = security;
        }

        /// <summary>
        /// Allows controlling how this class member is exposed to the scripting environment.
        /// </summary>
        /// <param name="security">The script access security for this member.</param>
        public ScriptMember(ScriptMemberSecurity security)
        {
            Security = security;
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    /// <summary>
    /// This attribute allows specifying the default member access for all public members of a class at once.
    /// If just the attribute name is used, the security defaults to 'None' (the V8 default) for each member where possible.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ScriptObject : Attribute
    {
        // --------------------------------------------------------------------------------------------------------------------

        public string TypeName { get; private set; }

        public ScriptMemberSecurity Security { get { return _Security; } private set { _Security = value; } }
        ScriptMemberSecurity _Security = ScriptMemberSecurity.ReadWrite;

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Allows specifying the default member access for all public members of this class at once.
        /// </summary>
        /// <param name="typeName">The function name to use when exposing a class to the scripting environment (default is null/empty, which means use the class name as is).</param>
        /// <param name="security">The global default access for all public class members (default is read/write).</param>
        public ScriptObject(string typeName = null, ScriptMemberSecurity security = ScriptMemberSecurity.ReadWrite)
        {
            TypeName = typeName;
            Security = security;
        }

        /// <summary>
        /// Allows specifying the default member access for all public members of a class at once.
        /// </summary>
        /// <param name="security">The global default access for all public class members.</param>
        public ScriptObject(ScriptMemberSecurity security)
        {
            Security = security;
        }

        // --------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    //[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    [Obsolete("Use ScriptMemberAttribute with 'NoAccess' instead.")]
    public class NoScriptAccess { }

    // ========================================================================================================================
}
