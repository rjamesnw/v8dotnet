using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace V8.Net
{
    // ========================================================================================================================

    public struct ReaderLock : IDisposable
    {
        ReaderWriterLock _RWLock;
        public ReaderLock(ReaderWriterLock rwlock, Int32 timeout)
        {
            _RWLock = rwlock;
            _RWLock.AcquireReaderLock(timeout);
        }
        public void Dispose()
        {
            _RWLock.ReleaseReaderLock();
        }
    }

    public struct WriterLock : IDisposable
    {
        ReaderWriterLock _RWLock;
        public WriterLock(ReaderWriterLock rwlock, Int32 timeout)
        {
            _RWLock = rwlock;
            _RWLock.AcquireWriterLock(timeout);
        }
        public void Dispose()
        {
            _RWLock.ReleaseWriterLock();
        }
    }

    // ========================================================================================================================

    public static class ExtensionMethods
    {
#if (V1_1 || V2 || V3 || V3_5)
        public static bool HasFlag(this Enum value, Enum flag) { var f = Convert.ToInt32(flag); return (Convert.ToInt32(value) & f) == f; }
#endif

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            if (dictionary.TryGetValue(key, out value))
                return value;
            else
                return defaultValue;
        }

        /// <summary>
        /// '{Type}.IsConstructedGenericType' is only supported in .NET 4.5+, so this is a cross-version supported implementation.
        /// </summary>
        public static bool IsConstructedGenericType(this Type type)
        {
#if (V1_1 || V2 || V3 || V3_5 || V4)
            if (!type.IsGenericType) return false;
            return (from a in type.GetGenericArguments() where a.IsGenericParameter select a).Count() == 0;
#else
            return type.IsConstructedGenericType;
#endif
        }

        /// <summary>
        /// Convert a list of enumerable items into strings and return the concatenated result.
        /// </summary>
        public static string Join(this IEnumerable values, string separator)
        {
#if (V1_1 || V2 || V3 || V3_5)
            string s = "";
            int i = 0;
            foreach (var item in values)
            {
                if (i++ > 0) s += separator;
                s += item != null ? item.ToString() : String.Empty;
            }
            return s;
#else
            return String.Join(separator, values);
#endif
        }

        public static ReaderLock ReadLock(this ReaderWriterLock _this, Int32 timeout = Int32.MaxValue)
        {
            return new ReaderLock(_this, timeout);
        }
        public static WriterLock WriteLock(this ReaderWriterLock _this, Int32 timeout = Int32.MaxValue)
        {
            return new WriterLock(_this, timeout);
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

    // =========================================================================================================================

    public static partial class Strings
    {
        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns true if the given object is null, or its string conversion results in an empty/null string.
        /// </summary>
        public static bool IsNullOrEmpty(object value) { return (value == null || string.IsNullOrEmpty(value.ToString())); }

        /// <summary>
        /// Returns true if the string value is null or contains white space (contains all characters less than or equal Unicode value 32).
        /// </summary>
        public static bool IsNullOrWhiteSpace(this string str)
        {
#if V2 || V3 || V3_5 // (this method exists in .NET 4.0+ as a method of the string class)
            if (str == null || str.Length == 0) return true;
            for (var i = 0; i < str.Length; i++)
                if ((int)str[i] <= 32) return true;
            return false;
#else
            return string.IsNullOrWhiteSpace(str);
#endif
        }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Selects the first non-null/empty string found in the parameter order given, and returns a default value if
        /// both are null/empty.
        /// </summary>
        public static string SelectNonEmptyString(string str1, string str2, string defaultValue)
        {
            return str1.IsNullOrWhiteSpace() ? (str2.IsNullOrWhiteSpace() ? defaultValue : str2) : str1;
        }
        public static string SelectNonEmptyString(string str1, string str2) { return SelectNonEmptyString(str1, str2, null); }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Convert a list of objects into strings and return the concatenated result.
        /// </summary>
        public static string Join(string separator, object[] objects)
        {
            string s = "";
            int i = 0;
            foreach (object o in objects)
            {
                if (i++ > 0) s += separator;
                if (o != null)
                    s += o.ToString();
            }
            return s;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Join two strings arrays into one big array. The new array is returned.
        /// </summary>
        public static string[] Join(string[] sa1, string[] sa2)
        {
            string[] strings = new string[sa1.Length + sa2.Length];
            CopyTo(sa1, strings, 0);
            CopyTo(sa2, strings, sa1.Length);
            return strings;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Copies a given source string array into another (destination), returning the destination array.
        /// </summary>
        /// <param name="src">The array to copy.</param>
        /// <param name="dest">The target of the copy.</param>
        /// <param name="destIndex">The array index into the destination in which copy starts.</param>
        public static string[] CopyTo(string[] src, string[] dest, int destIndex)
        {
            for (int i = 0; i < src.Length; i++)
                dest[destIndex + i] = src[i];
            return dest;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Copies the given string and string array to a new array. The new array is returned.
        /// </summary>
        public static string[] Add(string s, string[] strings)
        {
            string[] newStringArray = new string[strings.Length + 1];
            CopyTo(strings, newStringArray, 0);
            newStringArray[strings.Length] = s;
            return newStringArray;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        public static string FormatNumber(int n, string format)
        {
            return n.ToString(format);
        }

        public static string FormatNumber(double n, string format)
        {
            return n.ToString(format);
        }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the singular or plural of a word based on a numerical value.
        /// </summary>
        /// <param name="value">Number value.</param>
        /// <param name="word">Base word, singular.</param>
        /// <param name="suffix_if_plural">Suffix to use if "value" is not 1.</param>
        /// <param name="numberFormatting">The number format, if any (optional).</param>
        public static string S(int value, string word, string suffix_if_plural, string numberFormatting)
        {
            if (value != 1) return (numberFormatting != null ? FormatNumber(value, numberFormatting) : value.ToString()) + " " + word + suffix_if_plural;
            return value + " " + word;
        }
        public static string S(int value, string word, string suffix_if_plural) { return S(value, word, suffix_if_plural, null); }

        /// <summary>
        /// Returns the singular or plural of a word based on a numerical value.
        /// </summary>
        /// <param name="value">Number value.</param>
        /// <param name="word">Base word, singular.</param>
        /// <param name="suffix_if_plural">Suffix to use if "value" is not 1.</param>
        /// <param name="numberFormatting">The number format, if any (optional).</param>
        public static string S(double value, string word, string suffix_if_plural, string numberFormatting)
        {
            if (value != 1) return (numberFormatting != null ? FormatNumber(value, numberFormatting) : value.ToString()) + " " + word + suffix_if_plural;
            return value + " " + word;
        }
        public static string S(double value, string word, string suffix_if_plural) { return S(value, word, suffix_if_plural, null); }
        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Appends the source string to the target string and returns the result.
        /// If 'target' and 'source' are both not empty, then the delimiter is inserted between them, and the resulting string returned.
        /// </summary>
        /// <param name="target">The string to append to.</param>
        /// <param name="source">The string to append.</param>
        /// <param name="delimiter">If specified, the delimiter is placed between the target and source if the target is NOT empty.</param>
        /// <param name="onlyAddDelimiterIfMissing">Only inserts the delimiter if it is missing from the end of the target and beginning of the source.</param>
        /// <returns>The new string.</returns>
        public static string Append(string target, string source, string delimiter, bool onlyAddDelimiterIfMissing)
        {
            if (target == null) target = "";
            else if (delimiter != null && !string.IsNullOrEmpty(target) && source != null)
                if (!onlyAddDelimiterIfMissing || !target.EndsWith(delimiter) && !source.StartsWith(delimiter))
                    target += delimiter;
            if (source != null) target += source;
            return target;
        }
        public static string Append(string target, string source) { return Append(target, source, null, false); }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the number of occurrences of the given character in the given string.
        /// </summary>
        /// <param name="str">The string to look in.</param>
        /// <param name="chr">The character to count.</param>
        public static int CharCount(string str, char chr)
        {
            int count = 0;
            if (!string.IsNullOrEmpty(str))
                for (int i = 0; i < str.Length; i++)
                    if (str[i] == chr) count++;
            return count;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Performs a textual comparison, where the letter casing is ignored, and returns 'true' if the specified strings are a match.
        /// </summary>
        /// <param name="strA">The first string to compare.</param>
        /// <param name="strB">The second string to compare.</param>
        public static bool TextEqual(string strA, string strB)
        {
            return string.Compare(strA, strB, StringComparison.CurrentCultureIgnoreCase) == 0;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        public static int GetChecksum(string str)
        {
            int checksum = 0;
            for (int i = 0; i < str.Length; i++)
                checksum += str[i];
            return checksum;
        }

        // ---------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the given string up to a maximum of 'maxlength' characters.
        /// If more than 'maxlength' characters exist, an ellipse character is appended to the returned substring.
        /// </summary>
        public static string Limit(string text, uint maxLength, bool includeElipseInMaxLength)
        {
            if (maxLength == 0) return "";
            if (text.Length <= maxLength) return text;
            return text.Substring(0, (int)maxLength - (includeElipseInMaxLength ? 1 : 0)) + "…";
        }
        public static string Limit(string text, uint maxLength) { return Limit(text, maxLength, false); }

        // ---------------------------------------------------------------------------------------------------------------------
    }

    // =========================================================================================================================

    public static partial class Arrays
    {
        /// <summary>
        /// Concatenate a list of arrays. Specify one array for each parameter.
        /// To concatenate one list of arrays, use Join().
        /// </summary>
        /// <typeparam name="T">Array type for each argument.</typeparam>
        /// <param name="args">A concatenated array made form the specified arrays.</param>
        /// <returns></returns>
        public static T[] Concat<T>(params T[][] args)
        {
            return Join<T>(args);
        }
        /// <summary>
        /// Concatenate a list of arrays.
        /// </summary>
        /// <typeparam name="T">Array type for each argument.</typeparam>
        /// <param name="arrays">A concatenated array made form the specified arrays.</param>
        /// <returns></returns>
        public static T[] Join<T>(T[][] arrays)
        {
            if (arrays.Length == 0) return null;
            Int32 newLength = 0, i;
            for (i = 0; i < arrays.Length; i++)
                newLength += arrays[i].Length;
            T[] newArray = new T[newLength];
            T[] array;
            Int32 writeIndex = 0;
            for (i = 0; i < arrays.Length; i++)
            {
                array = arrays[i];
                Array.Copy(array, 0, newArray, writeIndex, array.Length);
                writeIndex += array.Length;
            }
            return newArray;
        }
        public static string Join<T>(IEnumerable<T> list)
        {
            string s = "";
            foreach (T item in list)
                s += item != null ? item.ToString() : "";
            return s;
        }

        public static T[] Convert<T>(IList array)
        {
            if (array == null) return null;
            T[] convertedItems = new T[array.Count];
            for (int i = 0; i < array.Count; i++)
                convertedItems[i] = (T)System.Convert.ChangeType(array[i], typeof(T), System.Threading.Thread.CurrentThread.CurrentCulture);
            return convertedItems;
        }

        public static T[] ConvertWithDefaults<T>(IList array)
        {
            if (array == null) return null;
            T[] convertedItems = new T[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                try { convertedItems[i] = (T)System.Convert.ChangeType(array[i], typeof(T), System.Threading.Thread.CurrentThread.CurrentCulture); }
                catch { convertedItems[i] = default(T); }
            }
            return convertedItems;
        }

        /// <summary>
        /// Select an item from the end of the array.
        /// </summary>
        /// <typeparam name="T">Array type.</typeparam>
        /// <param name="items">The array.</param>
        /// <param name="index">0, or a negative value, that is the offset of the item to retrieve.</param>
        public static T FromEnd<T>(this T[] items, int index)
        {
            return items[items.Length - 1 + index];
        }
        /// <summary>
        /// Select an item from the end of the list.
        /// </summary>
        /// <typeparam name="T">List type.</typeparam>
        /// <param name="items">The list.</param>
        /// <param name="index">0, or a negative value, that is the offset of the item to retrieve.</param>
        public static T FromEnd<T>(this IList<T> items, int index)
        {
            return items[items.Count - 1 + index];
        }
    }

    // =========================================================================================================================

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
