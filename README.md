V8.Net (v8dotnet) -- Mono runtime 3.10.0
================

The Repository contains the following branches:
- Master (stable releases for windows)
- development (bleeding edge development changes)
- development-mono (bleeding edge development changes for mono)

#Use V8.Net from Binaries
Install the current Monodevelop and Runtime for (x)Ubuntu from [tpokorra](http://software.opensuse.org/download/package?project=home:tpokorra:mono&package=monodevelop-opt):

`wget http://download.opensuse.org/repositories/home:tpokorra:mono/xUbuntu_14.10/Release.key`
`sudo apt-key add - < Release.key`

`sudo sh -c "echo 'deb http://download.opensuse.org/repositories/home:/tpokorra:/mono/xUbuntu_14.10/ /' >> /etc/apt/sources.list.d/monodevelop-opt.list"`

`sudo apt-get update`
and
`sudo apt-get install mono-opt`

Run the  V8.Net Console with following command:
`/opt/mono/bin/mono V8.Net-Console.exe`
or 
`LD_LIBRARY_PATH="pwd" MONO_LOG_LEVEL=debug MONO_LOG_MASK=all /opt/mono/bin/mono V8.Net-Console.exe`



#Build Dependencies

```
sudo aptitude install build-essential subversion git git-svn
sudo apt-get install libglib2.0-dev
```
V8dotnet is based on the Mono Runtime 3.10.0. To install the Runtime 3.10.0 together with Monodevelop on Ubuntu, the fastest way is to use the following link:
- [Install Monodevelop and Runtime  ](http://software.opensuse.org/download/package?project=home:tpokorra:mono&package=monodevelop-opt)


#Building with the V8.Net Buildscript

`cd v8dotnet`
`./build_V8_Net.sh --default 2`      (where 2 stands for the available core to build V8 )
Start the V8 Console app with:
`cd BuildResutl/Release` 
and 
`LD_LIBRARY_PATH="pwd" MONO_LOG_LEVEL=debug MONO_LOG_MASK=all /opt/mono/bin/mono V8.Net-Console.exe`



#Building V8.Net manually

This project contains Csharp and cpp projects. We using MonoDevelop to build the Csharp projects and g++ via commandline to build the cpp project.
To speed up this process you can also use the `build_V8_Net.sh`. 
 To build the projects execute the script **within** the `v8dotnet` directory.

The build steps are:

1. Clone the project
   - `git clone git@github.com:chrisber/v8dotnet.git` 
   - `cd v8dotnet`
   - `git submodule update --init --recursive`

2. Building Google V8
  - `cd Source/V8.NET-Proxy/V8/`
  - `make builddeps`
  - `make native library=shared`

5. Build V8.NET-Proxy library, on windows the library is called `V8_Net_Proxy_x64.dll` on Linux it is called `libV8_Net_Proxy.so`. We start by compiling the cpp from `/Source/V8.NET-Proxy/` to object files into an output directory called `/Source/V8.NET-Proxy/out`.
You can use the following command for it:   

        ls | grep cpp | awk -F. '{ system("g++  -std=c++11   -w -fpermissive -fPIC  -lstdc++ -Wl,--gc-sections   -c -IV8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/ "$1".cpp -o out/"$1".o ") }


7. After we have the object files we need to build a shared library called `libV8_Net_Proxy.so`. Copy the following files from `/Source/V8.NET-Proxy/V8/out/native/lib.target/*.so` into your output  directory `/Source/V8.NET-Proxy/out`.
    - libicui18n.so
    - libicuuc.so 
    - libv8.so

8. Compile the shared library with the following command. Execute it within your output directory. 

        g++ -Wall -std=c++11 -shared  -fPIC -I../ -I../V8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/   -Wl,-soname,libV8_Net_Proxy.so  -o libV8_Net_Proxy.so *.o ../V8/out/native/obj.host/testing/libgtest.a ../V8/out/native/obj.target/testing/libgmock.a ../V8/out/native/obj.target/testing/libgtest.a ../V8/out/native/obj.target/third_party/icu/libicudata.a ../V8/out/native/obj.target/tools/gyp/libv8_base.a ../V8/out/native/obj.target/tools/gyp/libv8_libbase.a ../V8/out/native/obj.target/tools/gyp/libv8_libplatform.a ../V8/out/native/obj.target/tools/gyp/libv8_nosnapshot.a ../V8/out/native/obj.target/tools/gyp/libv8_snapshot.a  -Wl,-rpath,. -L. -L../  -lpthread  -lstdc++ -licui18n -licuuc -lv8 -lglib-2.0 -lrt  -Wl,--verbose


8. Copy all compiled `*.so` files into your release directory.
    - libicui18n.so
    - libicuuc.so 
    - libv8.so 
    - libV8_Net_Proxy.so

9. Now we can build the C# projects. Build the `V8.Net.MonoDevelop.sln` via MonoDevelop or with the command:

10.  `mdtool -v build "--configuration:Release" "Source/V8.Net.MonoDevelop.sln"`

11. The last step is to copy all files into one directory

12. Release Directory
    - libicui18n.so
    - libicuuc.so
    - libv8.so
    - libV8_Net_Proxy.so
    - V8.Net.dll
    - V8.Net.Proxy.Interface.dll
    - V8.Net.SharedTypes.dll
    - V8.Net-Console.exe
    - V8.Net-Console.exe.config
13. Start it with `mono 8.Net-Console.exe`.
14. For debugging errors these commands can be helpful.
    - `LD_LIBRARY_PATH="pwd" MONO_LOG_LEVEL=debug MONO_LOG_MASK=all mono V8.Net-Console.exe` for checking if the library gets loaded.

    - `nm -u -C libV8_Net_Proxy.so` checking for undefined symboles.

### Loading the `libV8_Net_Proxy.so` library
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
    





**Summary:** A fairly non-abstracted wrapper for Google's V8 JavaScript engine.

What does that mean? Well, most other existing wrappers abstract most of the Google V8 engine's abilities away from you.  That's fine for simple tasks, but wouldn't you rather have full control over the power of the V8 engine from managed code?

I've carefully crafted a C++ proxy wrapper to help marshal fast communication between the V8 engine and the managed side.  One of the biggest challenges (which actually turned out to be simple in the end) was storing a field pointer in V8 objects to reference managed objects on call-backs (using reverse P/Invoke).  A special custom C# class was created to manage objects in an indexed array in an O(1) design that is extremely fast in locating managed objects representing V8 ones.

Interesting note: I was carefully considering future portability to the Mono framework as well for this project, so great care was made to make the transition as seamless/painless as possible. ;)

**License Clarification**

The license is LGPL.  In a nutshell, this means that you can link to the libraries from your own proprietary code, but if you modify the source files for anything in this project, the modified source and executables from it must also be made freely available as well (and you must clearly state you modified the code).

**Coming in Next Release (updated on August 7, 2013):**

  * Attempting to make it easier to deal with accessing nested properties/objects.
  * Looking into creating a system to allow easy binding (exposing) of C#/.NET objects to the JavaScript environment.
  * Created a new function '{V8Engine}.LoadScript(string scriptFile)' to make it easier to load JS files. :)
  * Integrating V8.NET into the client/server solution "DreamSpaceJS/Studio" (my answer to Microsoft abandoning Silverlight).  This will also help refine the V8.NET system in the process.  Scirra (Construct 2) has donated a license to help the project.
  * Added methods to make it easier to bind existing .NET object instances and types to the V8 JS environment.
  * **Possible Breaking changes:  Handles now have full access to the native objects without having to create a V8NativeObject instance.  In fact, V8NativeObject now wraps a Handle and redirects dynamic requests to it, and both Handle and InternalHandle implement the same methods for working on the native JavaScript objects.  This was done to allow dynamic property access on the handles without having to create another object to do it.  This change slightly affects the members and functionality of the V8NativeObject - but mostly behind the scenes.  This is still a WIP, so more things may break, but the end goal is to allow accessing objects easily using a chain of property names, such as 'a.b.c.d...'.**
  * V8NativeObject will now have a generic object (V8NativeObject<T>) version to allow injecting your own objects into it instead.  I want to get rid of the internal "_ObjectInfo' objects that hold member data that really should be in the object itself.  This will mainly affect only those who need to implement the interface (IV8NativeObject) instead of inheriting from V8NativeObject.  Under the new system, when initialized, you just cache a pointer to the 'V8NativeObject' instance wrapping your object.

**Completed Updates**

Added support for .NET 3.5.  I used some fancy build configurations to compile both in the same solution. ;) The only difference, as it pertains to this project, is that .NET 3.5 and under does not support "DynamicObject" (nor the dynamic type), and default parameters are not supported.

Looks like some people have issues with the DLLs loading.  I've made this better and have a more descriptive error to help correct the issues. :)

I spent a lot of time on the performance of the system and have been able to increased it quiet a bit. I have some simple garbage collection and performance testing scripts now that can be run from the console.

I'll also be looking into the WebRTC SDK in the near future as well to help support networkable servers that are compatible to the supported browsers (currently Chrome and Firefox).
