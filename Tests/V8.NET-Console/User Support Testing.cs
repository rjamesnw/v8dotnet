// file:	User Support Testing.cs
// summary:	Just scratch space to dump user code for testing and debugging. 
// This file should be cleared of any support code between the "USER CODE" region BEFORE committing changes.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using V8.Net;

#region USER CODE - TO BE REMOVED WHEN DONE
/// <summary>
/// X3DField is the abstract field type from which all single values field types are derived.
/// All fields derived from X3DField have names beginning with SF.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class X3DField<T> : ICloneable
{
    public T Value { get; protected set; }

    /// <summary>
    /// Creates a new field with the default value.
    /// </summary>
    public X3DField()
    {
        Value = default(T);
    }

    /// <summary>
    /// Creates a new field with a given value.
    /// </summary>
    /// <param name="value">The value of the new field.</param>
    public X3DField(T value)
    {
        Value = value;
    }

    /// <summary>
    /// Returns the current value of the field.
    /// </summary>
    /// <returns>The current value.</returns>
    public T GetValue()
    {
        return Value;
    }

    /// <summary>
    /// Sets the value of the field.
    /// </summary>
    /// <param name="value">The new value.</param>
    public void SetValue(T value)
    {
        Value = value;
    }

    /// <summary>
    /// Sets the value of the field.
    /// </summary>
    /// <param name="value">The field to copy the value from.</param>
    public void SetValue(X3DField<T> value)
    {
        SetValue(value.Value);
    }

    /// <summary>
    /// Creates a copy of the field.
    /// </summary>
    /// <returns></returns>
    public abstract object Clone();
}

public struct Vec2f
{
    public float X;
    public float Y;
}

/// <summary>
/// The SFVec2f field specifies a two-dimensional (2D) vector.
/// </summary>
[ScriptObject("SFVec2f", ScriptMemberSecurity.NoAcccess)]
public class SFVec2f : X3DField<Vec2f>
{
    /// <summary>
    /// The x component of the vector.
    /// </summary>
    [ScriptMember("x", ScriptMemberSecurity.ReadWrite)]
    public float X
    {
        set
        {
            Value = new Vec2f() { X = value, Y = Value.Y };
        }
        get
        {
            return Value.X;
        }
    }

    // Not working
    [ScriptMember("0", ScriptMemberSecurity.ReadWrite)]
    public float Idx0
    {
        get { return X; }
        set { X = value; }
    }

    /// <summary>
    /// The y component of the vector.
    /// </summary>
    [ScriptMember("y", ScriptMemberSecurity.ReadWrite)]
    public float Y
    {
        set
        {
            Value = new Vec2f() { X = Value.X, Y = value };
        }
        get
        {
            return Value.Y;
        }
    }

    /// <summary>
    /// Creates a new SFVec2f field whose vector value has x and y components of 0.
    /// </summary>
    public SFVec2f() :
        base(new Vec2f() { X = 0f, Y = 0f })
    {
    }

    /// <summary>
    /// Creates a new SFVec2f field with the given vector components.
    /// </summary>
    /// <param name="x">The x component of the vector.</param>
    /// <param name="y">The y component of the vector.</param>
    public SFVec2f(float x, float y) :
        base(new Vec2f() { X = x, Y = y })
    {
    }

    /// <summary>
    /// Sets the vector components of the SFVec2f field.
    /// </summary>
    /// <param name="x">The x component of the vector.</param>
    /// <param name="y">The y component of the vector.</param>
    public void SetValue(float x, float y)
    {
        Value = new Vec2f() { X = x, Y = y };
    }

    // Implements IAttributeField::SetValueFromString
    public void SetValueFromString(string value)
    {
        string[] values = value.Replace('.', ',').Split(' ');

        switch (values.Length)
        {
            case 2:
                SetValue(float.Parse(values[0]), float.Parse(values[1]));
                break;
            case 1:
                SetValue(float.Parse(values[0]), Value.Y);
                break;
        }
    }

    /// <summary>
    /// Converts the current value of the SFVec2f field to a string. The string consists of the x and the y components, separated by space.
    /// </summary>
    /// <returns>The string representation of the field.</returns>
    public override string ToString()
    {
        return X + " " + Y;
    }

    /// <summary>
    /// Creates a copy of the SFVec2f field. The copy has the same vector value as this field.
    /// </summary>
    /// <returns></returns>
    public override object Clone()
    {
        return new SFVec2f(X, Y);
    }
}
#endregion

namespace V8.Net
{
    public static class UserSupportTesting
    {
        /// <summary> Main entry-point for this test file. This is called just before the console main menu. </summary>
        /// <param name="engine"> A V8.Net Wrapper Engine instance. </param>
        public static void Main(V8Engine engine)
        {
            var Engine = engine;

            Engine.GlobalObject.SetProperty(typeof(Vec2f));
            Engine.GlobalObject.SetProperty(typeof(SFVec2f));

            Engine.Execute
            (@"
		        //function hello() {
			        var v = new SFVec2f(5, 8);
			        var sum = v.x + v.y;
			        Console.WriteLine('Hello World!' + ' x=' + v[0] + '; sum=' + sum); // Output: Hello World! x=undefined; sum=13
			        Console.WriteLine('Hello World!' + ' x=' + v['0'] + '; sum=' + sum); // Output: Hello World! x=undefined; sum=13
		        //};

		        //hello();
	        ", throwExceptionOnError: true);

            Console.WriteLine("EXECUTE TEST FINISHED!");
        }
    }
}
