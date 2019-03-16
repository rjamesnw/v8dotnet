V8.NET
======

News (March 4, 2019): I'm resurrecting this project to use it as a server for my .Net Core project FlowScript (https://github.com/rjamesnw/FlowScript), which is a Visual-Text hybrid VPL IDE for building web sites and services without having to write lots of code. The server end will be powered by this wrapper to V8.  At the moment V8.Net only supports .Net 4.0+, but I will soon add a Net Standard one and try to integrate it there also.

*Project Description*

A fairly non-abstracted wrapper for Google's V8 JavaScript engine.

What does that mean? Well, most other existing wrappers abstract most of the Google V8 engine's abilities away from you.  That's fine for simple tasks, but wouldn't you rather have full control over the power of the V8 engine from managed code?

I've carefully crafted a C++ proxy wrapper to help marshal fast data transfers between the V8 engine and the managed side.  One of the biggest challenges (which actually turned out to be simple in the end) was storing a field pointer in V8 objects to reference managed objects on call-backs (using reverse P/Invoke).  A special custom C# class was created to manage objects in an indexed array in an O(1) design that is extremely fast in locating managed objects representing V8 ones.

The documentation can be found here: https://github.com/rjamesnw/v8dotnet/wiki

*Installation*

Now on NugGet! Support for Net Standard targeting .Net 4.6.1+ and Net Standard 2.0+

https://www.nuget.org/packages/V8.Net/ ![nuget](https://img.shields.io/nuget/v/V8.Net.svg)

*Building The Source*

To build you need the V8 Source ([follow these steps](https://github.com/rjamesnw/v8dotnet/tree/master/Source/V8.NET-Proxy/V8)) and Visual Studio 2017.

*License Clarification*

The license is LGPL.  In a nutshell, this means that you can link to the libraries from your own proprietary code (including code for commercial use), but if you modify the source files for anything in this project, the modified source and executables from it must also be made freely available as well (and you must clearly state you modified the code).
