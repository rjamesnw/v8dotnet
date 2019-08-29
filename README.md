V8.NET
======

*Project Description*

A fairly non-abstracted wrapper for Google's V8 JavaScript engine.

What does that mean? Well, most other existing wrappers abstract most of the Google V8 engine's abilities away from you.  That's fine for simple tasks, but wouldn't you rather have full control over the power of the V8 engine from managed code? This gives you a lot of power, which means understanding how V8 works will go a long way to understanding how this wrapper works.

I've carefully crafted a C++ proxy wrapper to help marshal fast data transfers between the V8 engine and the managed side.  One of the biggest challenges (which actually turned out to be simple in the end) was storing a field pointer in V8 objects to reference managed objects on call-backs (using reverse P/Invoke).  A special custom C# class was created to manage objects in an indexed array in an O(1) design that is extremely fast in locating managed objects representing V8 ones.

The documentation can be found here: https://github.com/rjamesnw/v8dotnet/wiki

*Installation*

Now on NugGet! Support for Net Standard targeting .Net 4.6.1+ and Net Standard 2.0+

[https://www.nuget.org/packages/V8.Net/ ![nuget](https://img.shields.io/nuget/v/V8.Net.svg)](https://www.nuget.org/packages/V8.Net/)

*Building The Source*

To build you need the V8 Source ([follow these steps](https://github.com/rjamesnw/v8dotnet/tree/master/Source/V8.NET-Proxy/V8)) and Visual Studio 2017.

*License Clarification*

The license is LGPL.  In a nutshell, this means that you can link to the libraries from your own proprietary code (including code for commercial use), but if you modify the source files for anything in this project, the modified source and executables from it must also be made freely available as well (and you must clearly state you modified the code).
