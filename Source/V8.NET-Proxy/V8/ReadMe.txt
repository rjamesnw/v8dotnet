---------------
I. Introduction
---------------

Welcome to V8.NET!

V8.NET is a pure managed library that allows you to add scripting to your .NET
applications. The entire system is designed with pure p/invokes to make it
easy to port to other systems using Mono.  The proxy C++ library is also based
on pure native coding patterns to allow recompiling on non-windows systems!

V8.NET wraps Google's V8 engine - a high-performance open-source JavaScript
engine.

-------------------
II. Building V8.NET
-------------------

The provided project and solution files require Visual Studio 2012. They
produce architecture-neutral managed libraries that target .NET Framework 4.5,
although V8.NET has been tested with .NET Framework 4.0 as well. It does
not support older environments.

In order to build V8.NET support, you must first acquire, build, and import V8:

1. NOTE: This procedure and the update script are provided for your convenience.
   V8.NET does not managed the V8 source code, nor does it come with any
   third-party software required to download and build V8. Rights to V8 and its
   prerequisites are provided by their rights holders.

2. Install Git (http://subversion.apache.org/packages.html) and add it
   to your executable path. This will allow the automation of the V8 source
   download and builds.

3. Download and install the Windows SDK (as of Dec 8, 2016, this is required in
   order to build the source).

4. Make sure the tools that are required to setup the build environment are
   available (depot_tools specifically). See here: https://goo.gl/3jemcG
   Also, make sure to run "gclient" from the command line (with no parameters)
   at least once for it to initialize and download some other tools.

5. Navigate to "V8.NET\Source\V8.NET Proxy\V8" and double click "V8Update.cmd".
   Simply follow the easy to read prompts to download and build V8!!!
   We have made this extremely simple for you - and you're welcome. 8)

   a) Download the V8 dsource.
   b) Update the build tools.
   c) Build V8 as needed (debug/release).

   This script calls other google python scripts to download the latest version
   of V8 and related prerequisites, and build the 32-bit and 64-bit V8 shared
   libraries. The source requires approximately 2+GB of additional disk space.

   If you'd like to use a specific version of V8 instead of the latest one, set
   an environment variable named V8REV to the desired V8 trunk revision number
   before running the script. See http://code.google.com/p/v8/source/list.
   

You are now ready to build the full V8.NET solution using Visual Studio!

--------------------------
III. Debugging with V8.NET (NOTE: not yet completed - a work in progress)
--------------------------

V8 does not support standard Windows script debugging. Instead, it implements
its own TCP/IP-based debugging protocol. A convenient way to debug JavaScript
code running in V8 is to use the open-source Eclipse IDE:

1. Install Eclipse:

    http://www.eclipse.org/downloads/

2. Install Google Chrome Developer Tools for Java:

    a. Launch Eclipse and click "Help" -> "Install New Software...".
    b. Paste the following URL into the "Work with:" field:

        http://chromedevtools.googlecode.com/svn/update/dev/

    c. Select "Google Chrome Developer Tools" and complete the dialog.
    d. Restart Eclipse.

3. Enable script debugging in your application by invoking the V8ScriptEngine
   constructor with V8ScriptEngineFlags.EnableDebugging and an available TCP/IP
   port number. The default port number is 9222.

4. Attach the Eclipse debugger to your application:

    a. In Eclipse, select "Run" -> "Debug Configurations...".
    b. Right-click on "Standalone V8 VM" and select "New".
    c. Fill in the correct port number and click "Debug".

Note that you can also attach Visual Studio to your application for
simultaneous debugging of script, managed, and native code.

-------------------------------------
IV. V8 Known Differences From JScript
-------------------------------------

JScript is Microsoft's JavaScript engine.  The following are some differences:

1. V8 doesn't support indexers - properties with one or more parameters. Given
   the general syntax "A.B(C,D) = E" where A is an external object, JScript
   performs a single operation that assigns E to A's property B with index
   arguments C and D. This syntax allows for multiple indices and arbitrary
   index types. V8 interprets it as an attempt to use a value on the left side
   of an assignment - something that makes no sense in JavaScript. JScript's
   behavior appears to be an extension, but it's a convenient one because
   indexers are common in the CLR.

   WORKAROUND: Create a 'set' function yourself, such as "A.B.set(C,D,E)".

2. V8 doesn't support default properties. This is only an issue in conjunction
   with (1). The problematic syntax is of the form "A(B, C) = D", which in
   JScript means "assign D to external object A's default property with index
   arguments B and C".

   WORKAROUND: Create a 'set' function yourself, such as "A.set(B,C,D)".

3. V8 treats properties and methods identically. A method call is simply the
   invocation of a property. This causes ambiguity when an object has both a
   property and a method with the same name. An example of this in the CLR is
   an instance of System.Collections.Generic.List with LINQ extensions; such
   an object has both a property and a method named Count.

   WORKAROUND: None.
