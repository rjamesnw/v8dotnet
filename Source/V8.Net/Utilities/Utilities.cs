using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace V8.Net
{
    // ========================================================================================================================

    public static class ExtensionMethods
    {
        public static bool IsNullOrWhiteSpace(this string str)
        {
#if V2 || V3 || V3_5
            if (str == null) return true;
            for (var i = 0; i < str.Length; i++)
                if (str[i] <= ' ') return true;
            return false;
#else
            return string.IsNullOrWhiteSpace(str);
#endif
        }
    }

    // ========================================================================================================================

    public static unsafe class Utilities
    {
        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Allocates native memory.
        /// </summary>
        public static IntPtr AllocNativeMemory(int size)
        {
            return size > 0 ? Marshal.AllocHGlobal(size) : IntPtr.Zero;
        }

        /// <summary>
        /// Allocates native memory for storing pointers.
        /// </summary>
        public static IntPtr AllocPointerArray(int length)
        {
            return length > 0 ? AllocNativeMemory(Marshal.SizeOf(typeof(IntPtr)) * length) : IntPtr.Zero;
        }

        /// <summary>
        /// Frees native memory allocated with any of the 'Utilities.Alloc???()' methods.
        /// </summary>
        public static void FreeNativeMemory(IntPtr memPtr)
        {
            Marshal.FreeHGlobal(memPtr);
        }

        /// <summary>
        /// Allocates native memory to marshal an array of proxy handles.
        /// Uses 'Utilities.AllocPointerArray()', so be sure to call 'Utilities.FreeNativeMemory()' when done.
        /// </summary>
        public static HandleProxy** MakeHandleProxyArray(InternalHandle[] items)
        {
            HandleProxy** nativeArrayMem = (HandleProxy**)Utilities.AllocPointerArray(items.Length);

            for (var i = 0; i < items.Length; i++)
                nativeArrayMem[i] = (HandleProxy*)items[i];

            return nativeArrayMem;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Null Default (used mostly with database related data): Returns the value passed, or a default 
        /// value if the value passed is null, or equal to DBNull.Value.
        /// </summary>
        /// <param name="val">Value to check.</param>
        /// <param name="default_val">New value if "val" is null or DBNull.Value.</param>
        /// <returns></returns>
        public static string ND(object val, string default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : ((val is string) ? (string)val : val.ToString()); }

        public static T ND<T>(object val, T default_val) where T : class
        { return (val == DBNull.Value || val == null) ? (default_val) : ((T)val); }
        public static Int16 ND(object val, Int16 default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : (ToInt16(val, default_val) ?? default_val); }
        public static Int32 ND(object val, Int32 default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : (ToInt32(val, default_val) ?? default_val); }
        public static Int64 ND(object val, Int64 default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : (ToInt64(val, default_val) ?? default_val); }
        public static float ND(object val, float default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : (ToSingle(val, default_val) ?? default_val); }
        public static decimal ND(object val, decimal default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : (ToDecimal(val, default_val) ?? default_val); }
        public static bool ND(object val, bool default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : (ToBoolean(val, default_val) ?? default_val); }
        public static double ND(object val, double default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : (ToDouble(val, default_val) ?? default_val); }
        public static DateTime ND(object val, DateTime default_val)
        { return (val == DBNull.Value || val == null) ? (default_val) : (ToDateTime(val, default_val) ?? default_val); }

        // ... more of the same, but using nullable parameters ...
        public static bool ND(object val, bool? default_val) { return ND(val, default_val ?? false); }
        public static double ND(object val, double? default_val) { return ND(val, default_val ?? 0d); }
        public static decimal ND(object val, decimal? default_val) { return ND(val, default_val ?? 0m); }
        public static float ND(object val, float? default_val) { return ND(val, default_val ?? 0f); }
        public static Int16 ND(object val, Int16? default_val) { return ND(val, default_val ?? 0); }
        public static Int32 ND(object val, Int32? default_val) { return ND(val, default_val ?? 0); }
        public static Int64 ND(object val, Int64? default_val) { return ND(val, default_val ?? 0); }
        public static DateTime ND(object val, DateTime? default_val) { return ND(val, default_val ?? DateTime.MinValue); }

        // --------------------------------------------------------------------------------------------------------------------

        public static bool? ToBoolean(object value, bool? defaultValue)
        {
            if (value is bool) return (bool)value;
            string txt = ND(value, "").ToLower(); // (convert to string and test for 'true' state equivalent)
            if (txt == "true" || txt == "t" || txt == "yes" || txt == "y" || txt == "1" || txt == "ok" || txt == "pass") return true;
            if (txt == "false" || txt == "f" || txt == "no" || txt == "n" || txt == "0" || txt == "cancel" || txt == "fail") return false;
            return defaultValue;
        }
        public static Int16? ToInt16(object value, Int16? defaultValue)
        {
            if (value is Int16) return (Int16)value;
            string txt = ND(value, ""); // (convert to string, and then convert to expected type)
            Int16 convertedValue;
            if (Int16.TryParse(txt, out convertedValue))
                return convertedValue;
            return defaultValue;
        }
        public static Int32? ToInt32(object value, Int32? defaultValue)
        {
            if (value is Int32) return (Int32)value;
            string txt = ND(value, ""); // (convert to string, and then convert to expected type)
            Int32 convertedValue;
            if (Int32.TryParse(txt, out convertedValue))
                return convertedValue;
            return defaultValue;
        }
        public static Int64? ToInt64(object value, Int64? defaultValue)
        {
            if (value is Int64) return (Int64)value;
            string txt = ND(value, ""); // (convert to string, and then convert to expected type)
            Int64 convertedValue;
            if (Int64.TryParse(txt, out convertedValue))
                return convertedValue;
            return defaultValue;
        }
        public static Single? ToSingle(object value, Single? defaultValue)
        {
            if (value is Single) return (Single)value;
            string txt = ND(value, ""); // (convert to string, and then convert to expected type)
            Single convertedValue;
            if (Single.TryParse(txt, out convertedValue))
                return convertedValue;
            return defaultValue;
        }
        public static Double? ToDouble(object value, Double? defaultValue)
        {
            if (value is Double) return (Double)value;
            string txt = ND(value, ""); // (convert to string, and then convert to expected type)
            Double convertedValue;
            if (Double.TryParse(txt, out convertedValue))
                return convertedValue;
            return defaultValue;
        }
        public static Decimal? ToDecimal(object value, Decimal? defaultValue)
        {
            if (value is Decimal) return (Decimal)value;
            string txt = ND(value, ""); // (convert to string, and then convert to expected type)
            Decimal convertedValue;
            if (Decimal.TryParse(txt, out convertedValue))
                return convertedValue;
            return defaultValue;
        }
        public static DateTime? ToDateTime(object value, DateTime? defaultValue)
        {
            if (value is DateTime) return (DateTime)value;
            string txt = ND(value, ""); // (convert to string, and then convert to expected type)
            DateTime convertedValue;
            if (DateTime.TryParse(txt, out convertedValue))
                return convertedValue;
            return defaultValue;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        public static bool IsBoolean(Type t)
        {
            return (t == typeof(bool) || t == typeof(Boolean));
        }

        public static bool IsDateTime(Type t)
        {
            return (t == typeof(DateTime));
        }
        public static bool IsDateTime(string text)
        {
            DateTime dt; return DateTime.TryParse(text, out dt);
        }

        public static bool IsInt(Type t)
        {
            return (t == typeof(SByte) || t == typeof(int) || t == typeof(Int16) || t == typeof(Int32) || t == typeof(Int64));
        }
        public static bool IsInt64(string text)
        {
            Int64 i; return Int64.TryParse(text, out i);
        }
        public static bool IsInt(string text)
        {
            int i; return int.TryParse(text, out i);
        }

        public static bool IsUInt(Type t)
        {
            return (t == typeof(Byte) || t == typeof(uint) || t == typeof(UInt16) || t == typeof(UInt32) || t == typeof(UInt64));
        }

        public static bool IsFloat(Type t)
        {
            return (t == typeof(float) || t == typeof(double) || t == typeof(decimal));
        }

        public static bool IsNumeric(Type t)
        {
            return (IsInt(t) || IsUInt(t) || IsFloat(t));
        }
        public static bool IsNumeric(string text)
        {
            return Regex.IsMatch(text, @"^[+|-]?\d+\.?\d*$");
            //decimal d; return decimal.TryParse(text, out d);
        }
        public static bool IsSimpleNumeric(string text)
        {
            // http://derekslager.com/blog/posts/2007/09/a-better-dotnet-regular-expression-tester.ashx
            return Regex.IsMatch(text, @"^(?:\+|\-)?\d+\.?\d*$");
        }

        public static bool IsString(Type t)
        {
            return (t == typeof(string) || t == typeof(String));
        }

        // ---------------------------------------------------------------------------------------------------------------------
    }

    // ========================================================================================================================

    /// <summary>
    /// Provides utility methods for types.
    /// This class was originally created to support the 'ThreadController" class's "Dispatch()" methods.
    /// </summary>
    public static partial class Types
    {
        // --------------------------------------------------------------------------------------------------------------------

        public static object ChangeType(object value, Type targetType, IFormatProvider provider = null)
        {
            if (targetType == null)
                throw new ArgumentNullException("targetType");

            if (value == DBNull.Value) value = null;

            var valueType = value != null ? value.GetType() : typeof(object);

            if (valueType == targetType || targetType.IsAssignableFrom(valueType)) return value; // (same type as target! [or at least compatible])

            if (provider == null) provider = Thread.CurrentThread.CurrentCulture;

            var targetUnderlyingType = Nullable.GetUnderlyingType(targetType);

            if (targetUnderlyingType != null)
            {
                if (value == null) return value;
                // ... this is a nullable type target, so need to convert to underlying type first, then to a nullable type ...
                value = ChangeType(value, targetUnderlyingType, provider); // (recursive call to convert to the underlying nullable type)
                return Activator.CreateInstance(targetType, value);
            }
            else if (targetType == typeof(string)) return value != null ? value.ToString() : "";
            else if (targetType == typeof(Boolean))
            {
                if (value == null || value is string && ((string)value).IsNullOrWhiteSpace()) // (null or empty strings will be treated as 'false', but explicit text will try to be converted)
                    value = false;
                else if (Utilities.IsNumeric(valueType))
                    value = Convert.ToDouble(value) != 0; // (assume any value other than 0 is true)
                else if ((value = Utilities.ToBoolean(value, null)) == null)
                    throw new InvalidCastException(string.Format("Types.ChangeType(): Cannot convert string value \"{0}\" to a Boolean.", value));
                return value; // (this has the correct type already, so just return now)
            }
            else if (targetType.IsValueType && targetType.IsPrimitive)
            {
                if (value == null || value is string && ((string)value).IsNullOrWhiteSpace())
                {
                    // ... cannot set values to 'null' or empty strings, so translate this to a value type before conversion ...
                    if (targetType == typeof(bool))
                        value = false;
                    else if (targetType == typeof(DateTime))
                        value = DateTime.MinValue;
                    else
                        value = 0;
                }
                else if (value is string)
                {
                    // ... a value type is expected, but 'value' is a string, so try converting the string to a number value first in preparation ...
                    double d;
                    if (double.TryParse((string)value, System.Globalization.NumberStyles.Any, provider, out d)) value = d;
                }
            }
            else if (value == null) return null;

            try
            {
                return Convert.ChangeType(value, targetType, provider);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(string.Format("Types.ChangeType(): Cannot convert value \"{0}\" (type: '{1}') to type '{2}'. If you are developing the source type yourself, implement the 'IConvertible' interface.", Utilities.ND(value, ""), value.GetType().FullName, targetType.FullName), ex);
            }
        }

        public static TargetType ChangeType<TargetType>(object value, IFormatProvider provider = null)
        { return (TargetType)ChangeType(value, typeof(TargetType), provider); }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A structure which represents a 'typed' null value.
        /// This is required for cases where a type is just 'object', in which 'null' may be passed,
        /// but the type still needs to be known. An example usage is with methods that accept variable
        /// number of parameters, but need to know the argument type, even if null.
        /// </summary>
        public struct Null
        {
            public readonly Type Type;
            public Null(Type type)
            { if (type == null) throw new ArgumentNullException("type"); Type = type; }
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// If not null, returns either the argument, otherwise returns argument's 'null' type.
        /// This is needed in cases where an argument is null, but the argument type is needed.
        /// <para>
        /// Example: MyMethod(typeof(DateTime).Arg(value)); - If 'value' is null, then the type is passed instead as 'Types.Null'
        /// </para>
        /// </summary>
        /// <param name="type">Argument type.</param>
        /// <param name="value">Argument value.</param>
        /// <returns>Argument value, or the type if null.</returns>
        public static object Arg(this Type type, object value)
        {
            if (type == null) throw new ArgumentNullException("type");
            if (value != null)
            {
                if (!type.IsAssignableFrom(value.GetType()))
                    throw new InvalidOperationException("Types.Arg(): Type of 'value' cannot be cast to '" + type.FullName + "'.");
                return value;
            }
            else return new Null(type);
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Attempts to get the types of the values passed.
        /// If a value is 'null', then the call will fail, and 'null' will be returned.
        /// Note: This method recognizes Types.Null values.
        /// </summary>
        /// <param name="args">Argument values to get types for.</param>
        public static Type[] GetTypes(params object[] args)
        {
            if (args == null || args.Length == 0) return null;
            foreach (object arg in args)
                if (arg == null) return null;
            Type[] argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = (args[i] is Null) ? ((Null)args[i]).Type : args[i].GetType();
            return argTypes;
        }

        // --------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Converts any Types.Null objects into simple 'null' references.
        /// This is helpful after using Types.GetTypes() on the same items - once the types are
        /// retrieved, this method helps to convert Types.Null items back to 'null'.
        /// </summary>
        public static object[] ConvertNullsToNullReferences(object[] items)
        {
            if (items != null)
                for (int i = 0; i < items.Length; i++)
                    if (items[i] is Null) items[i] = null;
            return items;
        }
    }

    // ========================================================================================================================
}
