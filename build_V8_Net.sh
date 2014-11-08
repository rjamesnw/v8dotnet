#!/bin/bash
 printf '\e[1;34m%-6s\e[m \n' "++++++++++++++++++++++++++++++++++++++++++++++++++++"
 printf '\e[1;34m%-6s\e[m \n' "V8 Build"
 printf '\e[1;34m%-6s\e[m \n' "++++++++++++++++++++++++++++++++++++++++++++++++++++"
 printf '\e[1;34m%-6s\e[m \n' "Update V8 Submodule"
git submodule update --init --recursive
#project root dir
currentDir=`pwd`
mkdir BuildOutput
cd BuildOutput
mkdir Release
mkdir Debug
cd ../Source/V8.NET-Proxy/V8/
printf '\e[1;34m%-6s\e[m \n' "Building V8 "
printf '\e[1;34m%-6s\e[m \n' "Version 3.30.13 (based on bleeding_edge revision r24708) "
printf '\e[1;34m%-6s\e[m \n' "commit 9a58807030208121b7e9f01aca2b932eb52e249f "
make builddeps
make native library=shared
cd ../
mkdir out
printf '\e[1;34m%-6s\e[m \n' "Build V8 Proxy"
ls | grep cpp | awk -F. '{ system("g++  -std=c++11   -w -fpermissive -fPIC  -lstdc++ -Wl,--gc-sections   -c -IV8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/ "$1".cpp -o out/"$1".o ") }'
cd out
cp ../V8/out/native/lib.target/libicui18n.so .
cp ../V8/out/native/lib.target/libicuuc.so .
cp ../V8/out/native/lib.target/libv8.so .
g++ -Wall -std=c++11 -shared  -fPIC -I../ -I../V8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/   -Wl,-soname,libV8_Net_Proxy.so  -o libV8_Net_Proxy.so *.o ../V8/out/native/obj.host/testing/libgtest.a ../V8/out/native/obj.target/testing/libgmock.a ../V8/out/native/obj.target/testing/libgtest.a ../V8/out/native/obj.target/third_party/icu/libicudata.a ../V8/out/native/obj.target/tools/gyp/libv8_base.a ../V8/out/native/obj.target/tools/gyp/libv8_libbase.a ../V8/out/native/obj.target/tools/gyp/libv8_libplatform.a ../V8/out/native/obj.target/tools/gyp/libv8_nosnapshot.a ../V8/out/native/obj.target/tools/gyp/libv8_snapshot.a  -Wl,-rpath,. -L. -L../  -lpthread  -lstdc++ -licui18n -licuuc -lv8 -lglib-2.0 -lrt  -Wl,--verbose
cp *.so ../../../BuildOutput/Debug
cp *.so ../../../BuildOutput/Release
#project root dir
cd currentDir
mdtool -v build "--configuration:Release" "Source/V8.Net.MonoDevelop.sln"
mdtool -v build "--configuration:Debug" "Source/V8.Net.MonoDevelop.sln"
cp Source/V8.NET-Console/bin/Debug/* BuildOutput/Debug/
cp Source/V8.NET-Console/bin/Release/* BuildOutput/Release/




