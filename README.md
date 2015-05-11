V8.NET
======

**Summary:** A fairly non-abstracted wrapper for Google's V8 JavaScript engine.

What does that mean? Well, most other existing wrappers abstract most of the Google V8 engine's abilities away from you.  That's fine for simple tasks, but wouldn't you rather have full control over the power of the V8 engine from managed code?

I've carefully crafted a C++ proxy wrapper to help marshal fast data transfers between the V8 engine and the managed side.  One of the biggest challenges (which actually turned out to be simple in the end) was storing a field pointer in V8 objects to reference managed objects on call-backs (using reverse P/Invoke).  A special custom C# class was created to manage objects in an indexed array in an O(1) design that is extremely fast in locating managed objects representing V8 ones.

Interesting note: I was carefully considering future portability to the Mono framework as well for this project, so great care was made to make the transition as seamless/painless as possible. ;)

*Note: The project is hosted at [CodePlex](https://v8dotnet.codeplex.com/) officially, so the files here are only updated after posting there first.*

*License Clarification*

The license is LGPL.  In a nutshell, this means that you can link to the libraries from your own proprietary code (including code for commercial use), but if you modify the source files for anything in this project, the modified source and executables from it must also be made freely available as well (and you must clearly state you modified the code).
