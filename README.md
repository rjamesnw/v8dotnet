V8.Net (v8dotnet) -- Mono runtime 3.10.0
================

1. [Compilation and Installation](#Compilation and Installation)
2. [Using V8.Net with Mono](#Using V8.Net with Mono)
3. [Configuration Options]()

**Summary:** A fairly non-abstracted wrapper for Google's V8 JavaScript engine.

What does that mean? Well, most other existing wrappers abstract most of the Google V8 engine's abilities away from you.  That's fine for simple tasks, but wouldn't you rather have full control over the power of the V8 engine from managed code?

I've carefully crafted a C++ proxy wrapper to help marshal fast communication between the V8 engine and the managed side.  One of the biggest challenges (which actually turned out to be simple in the end) was storing a field pointer in V8 objects to reference managed objects on call-backs (using reverse P/Invoke).  A special custom C# class was created to manage objects in an indexed array in an O(1) design that is extremely fast in locating managed objects representing V8 ones.

Interesting note: I was carefully considering future portability to the Mono framework as well for this project, so great care was made to make the transition as seamless/painless as possible. ;)

[V8.Net created by  James Wilkins](https://v8dotnet.codeplex.com/)


##Build Status
Linux |  Mac OS X
------------- | -------------
[![Build Status](https://travis-ci.org/chrisber/v8dotnet.svg?branch=development-mono)](https://travis-ci.org/chrisber/v8dotnet)  | In progress


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
xcode-select --install
Install Xcode IDE
```

V8dotnet is based on the Mono Runtime 3.10.0. To install the Runtime 3.10.0 together with [Monodevelop](http://www.monodevelop.com/download/) on Ubuntu


##Building with the V8.Net Buildscript
Add gyp and c/c++/linking configuration to your environment, we use clang for compiling V8 and V8 proxy libaries
```
export CXX="`which clang++`       -v -std=c++11 -stdlib=libc++"
export CC="`which clang`          -v "
export CPP="`which clang`      -E -v "
export LINK="`which clang++`      -v -std=c++11 -stdlib=libc++"
export CXX_host="`which clang++`  -v "
export CC_host="`which clang`     -v "
export CPP_host="`which clang` -E -v "
export LINK_host="`which clang++` -v "
```
For debian systems export also:   
```
export GYP_DEFINES="clang=1"
```
For mac osx:   
```
export GYP_DEFINES="clang=1  mac_deployment_target=10.10"
```

The build script defines a number of targets for each target architecture (ia32, x64, ) and mode (debug or release). So your basic command for building is:   
`cd v8dotnet`   
`./build_V8_Net.sh --default x64.release 2`      (where 2 stands for the available core to build V8 )   
or   
`./build_V8_Net.sh --default ia32.debug 4`      
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
ls | grep cpp | awk -F. '{ system("g++  -std=c++11 -w -fpermissive -fPIC -Wl,--gc-sections-c -IV8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/ "$1".cpp -o out/"$1".o ") }
```   
Linking:
```g++ -Wall -std=c++11 -shared  -fPIC -I../ -I../V8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/   -Wl,-soname,libV8_Net_Proxy.so  -o libV8_Net_Proxy.so *.o ../V8/out/native/obj.host/testing/libgtest.a ../V8/out/native/obj.target/testing/libgmock.a ../V8/out/native/obj.target/testing/libgtest.a ../V8/out/native/obj.target/third_party/icu/libicudata.a ../V8/out/native/obj.target/tools/gyp/libv8_base.a ../V8/out/native/obj.target/tools/gyp/libv8_libbase.a ../V8/out/native/obj.target/tools/gyp/libv8_libplatform.a ../V8/out/native/obj.target/tools/gyp/libv8_nosnapshot.a ../V8/out/native/obj.target/tools/gyp/libv8_snapshot.a  -Wl,-rpath,. -L. -L../  -lpthread  -lstdc++ -licui18n -licuuc -lv8 -lglib-2.0 -lrt  -Wl,--verbose```   

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

