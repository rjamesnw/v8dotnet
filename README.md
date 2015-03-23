V8.NET
======


1. [Compilation and Installation](https://github.com/chrisber/v8dotnet/tree/development-mono#v8net-v8dotnet----mono-runtime-3100)
2. [Using V8.Net with Mono](https://github.com/chrisber/v8dotnet/tree/development-mono#use-v8net-from-binaries)
3. [Configuration Options](https://github.com/chrisber/v8dotnet/tree/development-mono#building-with-the-v8net-buildscript)



**Summary:** A fairly non-abstracted wrapper for Google's V8 JavaScript engine.

*Note: The project is hosted at [CodePlex](https://v8dotnet.codeplex.com/) officially, so the files here are only updated after posting there first.*

What does that mean? Well, most other existing wrappers abstract most of the Google V8 engine's abilities away from you.  That's fine for simple tasks, but wouldn't you rather have full control over the power of the V8 engine from managed code?

I've carefully crafted a C++ proxy wrapper to help marshal fast data transfers between the V8 engine and the managed side.  One of the biggest challenges (which actually turned out to be simple in the end) was storing a field pointer in V8 objects to reference managed objects on call-backs (using reverse P/Invoke).  A special custom C# class was created to manage objects in an indexed array in an O(1) design that is extremely fast in locating managed objects representing V8 ones.

Interesting note: I was carefully considering future portability to the Mono framework as well for this project, so great care was made to make the transition as seamless/painless as possible. ;)

*License Clarification*

The license is LGPL.  In a nutshell, this means that you can link to the libraries from your own proprietary code, but if you modify the source files for anything in this project, the modified source and executables from it must also be made freely available as well (and you must clearly state you modified the code).

*Coming in Next Release / WIP (updated on Jan 19th, 2015):*
(NOTE: If I'm posting up coming changes here, they are usually in the latest development-branch release binaries.  Those binary releases will continue to be the bleeding edge releases.)

_(Focus is currently on a new VPL {"[visual programming language]"} based scripting solution called "FlowScript" as a browser IDE for JavaScript development;  However, I'll make minor updates as needed.)_

*Completed Updates This Release*

* Fixed a bug where '{V8Engine}.call()' doesn't catch and return any exceptions.  Previously, exceptions would cause 'undefined' to be returned.

* Apparently Google has changed a lot of how the API works, and rearranged code in V8 namespaces, so those looking to build from the source had issues.  I've fixed it for the most part (in the latest dev branch, including the binaries).

* Because V8.Net is part of a larger project vision, I'm focusing on the other projects now.  I'll be coming back to it off and on as required, and will try to get in some requests/fixes as soon as possible.  Thanks, and have a great day!  8)

_Previous Updates:_

* Breaking change: While working on DreamSpace it occurred to me that running actions in scopes is just a pain in the @$$. ;)  I did a speed check and found this:
** > Using action callbacks ...
** >  20000000 loops @ 32679ms total = 0.00163395 ms each pass.
** >  Using native stack scopes ...
** >  20000000 loops @ 21378ms total = 0.0010689 ms each pass.
** It's clear there's really not much benefit because I think the majority of V8.NET users are creating non-linear calls to the engine, and thus it's faster to stick with the native V8 side scopes on a per P/Invoke call basis (which I sort of suspected - though the goal was to mimic V8 at the time). Besides, I think it will also help cut down on a lot of bugs if in case someone forgets to use a scope. :)

* "{ObjectTemplate}.RegisterInvokeHandler()" should take a JSFunction as a callback, and not the native callback signature.  This has been corrected.

* Bug fixes (see master branch history for details).

* Fixed a bug in the garbage collection process where managed functions from function templates might disappear, causing the native script call to fail.

* Many bugs fixed in the new binding system, which is much more efficient!  Security has also been enhanced, so implicit binding of types, by default, will not show any properties unless you explicitly register the type using "{V8Engine}.RegisterType()".

* Breaking change:  Generic invocation will change from the form {"'SomeMethod$#(types...)(params...)'"} (where '#'  is the number of expected types) to {"'SomeMethod$#([types...], params...)'"}, which is more efficient, and allows faster binding and invoking. Same number of characters actually, and the first argument to a generic method must be an array of types (or values to get types from).

* Added "{ObjectTemplate}.RegisterInvokeHandler()" to allow invoking non-function objects, created from templates, like function objects.

* Added "{TypeBinder}.ChangeMemberSecurity()" to modify security on types you don't have control over.

* Refactoring the files to better support the coming Mono port. 8)  Special thanks to rryk for getting the ball rolling.  To this end, I'll also be converting the source repository to Git! :) You're welcome.

* *Breaking change:* (already!? yes. ;) ) After some retrospecting, I think it's better to update an ObjectTemplate to spit out objects for a given types more quickly than having the type binder set accessors for each new bound instance.  This is a small change, and simply requires to call "RegisterType()" on the engine instance instead.

* Added "Prototype" to InternalHandle and ObjectHandle.

* Added 'SetProperty()' and 'SetAccessor()' to 'ObjectTemplate' to call the corresponding "Set()" and "SetAccessor()" functions on the native ObjectTemplate instance.  This allows setting up your own properties on the template without needing to implement a custom 'V8ManagedObject' instance.

* Static types are now supported when binding CLR objects to JavaScript.

* Spaces removed from paths and file names to better support cross-platform compatibility with IDEs such as MonoDevelop.  The test project was also removed (was never used anyhow - testing is done in script form via V8).

* Very easy to deal with accessing nested properties/objects.

* Added methods to make it easier to bind existing .NET object instances and types to the V8 JS environment.

* Created a new function '{V8Engine}.LoadScript(string scriptFile)' to make it more convenient to load JS files.

* *Breaking change 1:*  Some handle property names were refactored, and some added so that handles can have full access to the native objects without having to create V8NativeObject instances (too much extra overhead I wanted to avoid).  In fact, V8NativeObject now wraps a Handle and redirects dynamic requests to it, and both Handle and InternalHandle implement the same methods for working on the native JavaScript objects (so that's all you need to access/update in-script objects!).  This was done to allow dynamic property access on the handles without having to create another object to do it.  This change slightly affects the members and functionality of the V8NativeObject - but mostly behind the scenes. This allows accessing objects easily using a chain of property names, such as '((dynamic){someHandle}).a.b.c.d...' or '{object}.AsDynamic.a.b.c.d...'.

* *Breaking change 2:*  V8NativeObject will now have a generic object (V8NativeObject<T>) version to allow _injecting_ your own objects into it instead of deriving from it (deriving is recommended however).  I wanted to get rid of the internal "_ObjectInfo' objects that were holding member data that really should be in the object itself.  This will mainly affect only those who need to implement the interface (IV8NativeObject) instead of inheriting from V8NativeObject.  Under the new system, when the "Initialize()" virtual method is called, you just cache a pointer to the 'V8NativeObject' instance wrapping your object and use that instead.

* Added support for .NET 3.5.  I used some fancy build configurations to compile both in the same solution. ;) The only difference, as it pertains to this project, is that .NET 3.5 and under does not support "DynamicObject" (nor the dynamic type), and default parameters are not supported.

* Looks like some people have issues with the DLLs loading.  I've made this better and have a more descriptive error to help correct the issues. :)

* I spent a lot of time on the performance of the system and have been able to increased it quiet a bit. I have some simple garbage collection and performance testing scripts now that can be run from the console.

*Future Ideas*
I'll also be looking into the WebRTC SDK in the near future as well to help support networkable servers that are compatible to the supported browsers (currently Chrome and Firefox).


V8.Net (v8dotnet) -- Mono runtime 3.10.0
================


##Build Status
Linux |  Mac OS X
------------- | -------------
[![Build Status](https://travis-ci.org/chrisber/v8dotnet.svg?branch=development-mono)](https://travis-ci.org/chrisber/v8dotnet)  | Working


#Compilation and Installation
The Repository contains the following branches:
- Master (stable releases for windows)
- development (bleeding edge development changes)
- development-mono (bleeding edge development changes for mono)   

##Project structure

- Build (Native V8 Proxy build directory)
- BuildResult (Binaries)
- gyp (Build Engine for the native proxy)
- Source
  - V8.Net (Manage Wrapper)
  - V8.NET-Console (Test App)
  - V8.Net.Mono.Nuget
  - V8.NET-Proxy (Native files)
    - V8 (Google V8)
  - V8.NET-ProxyInterface (Managed Interface for p/invoke)
  - V8.Net-SharedTypes (Managed defined types)


##Use V8.Net from Binaries   
Download the [Binaries here:](https://github.com/chrisber/v8dotnet/releases)   
Run the  V8.Net Console with following command:   
`mono V8.Net-Console.exe`   
or    
`LD_LIBRARY_PATH="pwd" MONO_LOG_LEVEL=debug MONO_LOG_MASK=all mono V8.Net-Console.exe`



##Build Dependencies
Ubuntu 14.04.01
```
sudo aptitude install build-essential subversion git git-svn
sudo apt-get install g++-multilib
```
OpenSuse
```
sudo zypper install --type pattern devel_basis
sudo zypper in gcc48-32bit libstdc++48-devel-32bit  compat-32bit
```
Mac OSX 10.10
```
brew install gcc49
xcode-select --install
Install Xcode IDE 6.x
```

V8dotnet is based on the Mono Runtime <= 3.10.0. To install the Runtime 3.10.0 together with [Monodevelop](http://www.monodevelop.com/download/) on Ubuntu


##Building with the V8.Net Buildscript
Add gyp and c/c++/linking configuration to your environment.

For debian systems export:   
```
export GYP_DEFINES="clang=1"
export CXX="`which clang++`       -v -std=c++11 -stdlib=libstdc++"
export CC="`which clang`          -v "
export CPP="`which clang`      -E -v "
export LINK="`which clang++`      -v -std=c++11 -stdlib=libstdc++"
export CXX_host="`which clang++`  -v "
export CC_host="`which clang`     -v "
export CPP_host="`which clang` -E -v "
export LINK_host="`which clang++` -v "
```   

For mac osx:      
```
export CXX=/usr/local/bin/g++-4.9 
export LINK=/usr/local/bin/g++-4.9 
```

The build script defines a number of targets for each target architecture (ia32, x64, ) and mode (debug, release). The basic commands for building are:   
`cd v8dotnet`   

`./build_V8_Net.sh --default x64.release 2`      (where 2 stands for the available core to build V8 )  
or   
`./build_V8_Net.sh --default ia32.debug 4`   
Note:
The mono runtime on OSX is only available with the x86 architecture to not run into the error (An attempt was made to load a program with an incorrect format) compile ia32 for osx.  
Also only build v8dotnet with stdlibc++ because libc++ is incompatible to mono when it comes to handling p/invoke calls;
`./build_V8_Net.sh --default ia32.release 2`   
Start the V8 Console app with:   
`cd BuildResutl/Release`   
and   
`LD_LIBRARY_PATH="pwd" MONO_LOG_LEVEL=debug MONO_LOG_MASK=all mono V8.Net-Console.exe`   
Form more build option use: `./build_V8_Net.sh --help`



##Building V8.Net manually

This project contains Csharp and cpp projects. We are using MonoDevelop to build the Csharp projects and gyp to build the cpp project.

1. Clone the project
   - `git clone git@github.com:chrisber/v8dotnet.git` 
   - `cd v8dotnet`
   - `git submodule update --init --recursive`

2. Building Google V8
  - `cd Source/V8.NET-Proxy/V8/`
  - `make builddeps  -j 2` (2 equals the number of cpu cores available)
  - `make library=shared -j 2` 

5. Build V8.NET-Proxy this step build the native library. On Windows OS the library is called `V8_Net_Proxy_x64.dll` on Linux it is called `libV8_Net_Proxy.so`.
The g++ option to compile `libV8_Net_Proxy.so` are:   
  `'cflags':[ '-Werror -Wall -std=c++11 -w -fpermissive -fPIC -c',],`
The linke options are:
`'ldflags':[ '-Wall -std=c++11 -shared -fPIC',],`   
`'libraries:['-Wl,-rpath,. -L. -L../ -lpthread -lstdc++ -lv8 -licui18n -licuuc -lglib-2.0 -lrt libgmock.a ...*a']`  
Compiling:
 ```
ls | grep cpp | awk -F. '{ system("g++  -std=c++11 -w -fpermissive -fPIC -Wl,--gc-sections-c -IV8/ "$1".cpp -o out/"$1".o ") }
```   
Linking:
```g++ -Wall -std=c++11 -shared  -fPIC -I../ -I../V8/ -Wl,-soname,libV8_Net_Proxy.so  -o libV8_Net_Proxy.so *.o ../V8/out/native/obj.host/testing/libgtest.a ../V8/out/native/obj.target/testing/libgmock.a ../V8/out/native/obj.target/testing/libgtest.a ../V8/out/native/obj.target/third_party/icu/libicudata.a ../V8/out/native/obj.target/tools/gyp/libv8_base.a ../V8/out/native/obj.target/tools/gyp/libv8_libbase.a ../V8/out/native/obj.target/tools/gyp/libv8_libplatform.a ../V8/out/native/obj.target/tools/gyp/libv8_nosnapshot.a ../V8/out/native/obj.target/tools/gyp/libv8_snapshot.a  -Wl,-rpath,. -L. -L../  -lpthread  -lstdc++ -licui18n -licuuc -lv8 -lglib-2.0 -lrt  -Wl,--verbose```   

Or use the provided v8dotnet.gyp file for compiling and linking the shared library.   
```
    ./gyp/gyp  -Dbase_dir=`pwd` -Dtarget_arch="x64" -Dbuild_option="release" -f make --depth=. v8dotnet.gyp  --generator-output=./Build/x64.release/makefiles
     V=1 make -C ./Build/x64.release/makefiles
```   

6. Now we can build the C# projects. Build the `V8.Net.MonoDevelop.sln` via MonoDevelop or with the command:

7.  `xbuild /p:Configuration=Release Source/V8.Net.MonoDevelop.sln"`

8. The last step is to copy all files into one directory

9. Release Directory
    - libicui18n.so
    - libicuuc.so
    - libv8.so
    - libV8_Net_Proxy.so
    - V8.Net.dll
    - V8.Net.Proxy.Interface.dll
    - V8.Net.SharedTypes.dll
    - V8.Net-Console.exe
10. Start it with `mono V8.Net-Console.exe`.
11. For debugging errors these commands can be helpful.
    - `LD_LIBRARY_PATH="pwd" MONO_LOG_LEVEL=debug MONO_LOG_MASK=all mono V8.Net-Console.exe` for checking if the library gets loaded.
    - `nm -u -C libV8_Net_Proxy.so` checking for undefined symboles.

#Using V8.Net with Mono
###Loading the libV8_Net_Proxy.so library
There are three possibilities to load the library
- adding it in the same place where the executable is. For instance:
    - V8.Net-Console.exe
    - libicui18n.so
    - libicuuc.so
    - libv8.so
    - libV8_Net_Proxy.so
- adding it in the /usr/lib directory
    - libicui18n.so
    - libicuuc.so
    - libv8.so
    - libV8_Net_Proxy.so
- or setting the `LD_LIBRARY_PATH="pwd" path.
    

**License Clarification**

The license is LGPL.  In a nutshell, this means that you can link to the libraries from your own proprietary code, but if you modify the source files for anything in this project, the modified source and executables from it must also be made freely available as well (and you must clearly state you modified the code).

