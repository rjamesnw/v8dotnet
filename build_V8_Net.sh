#!/bin/bash
currentDir=`pwd`
currentFunction=""

 buildResult(){
	if [ $? -eq 0 ]
	then
	  printf '\e[1;32m%-6s\e[m \n' "+++ $currentFunction Successfull +++"
	else
	  printf '\e[1;31m%-6s\e[m \n' "+++ Error +++" >&2
	  exit $?
	fi
 }

 buildV8 (){
	currentFunction=" Build V8 Javascript Engine "
 	 printf '\e[1;34m%-6s\e[m \n' "Create Directories"
	 mkdir -p BuildOutput/{Debug,Release}
	 mkdir -p Source/V8.NET-Proxy/out

	printf '\e[1;34m%-6s\e[m \n' "Init V8 Submodule"
 	git submodule update --init --recursive
	cd $currentDir
	cd Source/V8.NET-Proxy/V8/
	printf '\e[1;34m%-6s\e[m \n' "Building V8 "
	printf '\e[1;34m%-6s\e[m \n' "Version 3.29.40 (based on bleeding_edge revision r23628)"
	printf '\e[1;34m%-6s\e[m \n' "commit 21d700eedcdd6570eff22ece724b63a5eefe78cb"
	printf '\e[1;34m%-6s\e[m \n' "make builddeps -j ${JOBSV8=$1}"
	make builddeps -j ${JOBSV8=$1}
	printf '\e[1;34m%-6s\e[m \n' "make library=shared gdbjit=on -j ${JOBSV8=$1}"
	#Debug
	#make native library=shared gdbjit=on -g -j ${JOBSV8=$1}
	make library=shared -j ${JOBSV8=$1}
}

 buildV8Proxy (){
	currentFunction="Build V8 native Proxy"
	cd $currentDir
	
     printf '\e[1;34m%-6s\e[m \n' "Create Directories"
	 mkdir -p BuildOutput/{Debug,Release}
	 mkdir -p Source/V8.NET-Proxy/out

	cd Source/V8.NET-Proxy/
	printf '\e[1;34m%-6s\e[m \n' "Build V8DotNet Proxy"
	#Debug
	# 	ls | grep cpp | awk -F. '{ system("g++ -g -std=c++11 -w -fpermissive -fPIC  -lstdc++ -Wl,--gc-sections   -c -IV8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/ "$1".cpp -o out/"$1".o ") }'
	ls | grep cpp | awk -F. '{ system("g++ -std=c++11 -w -fpermissive -fPIC  -lstdc++ -Wl,--gc-sections   -c -IV8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/ "$1".cpp -o out/"$1".o ") }'
	cd out
	cp ../V8/out/native/lib.target/libicui18n.so .
	cp ../V8/out/native/lib.target/libicuuc.so .
	cp ../V8/out/native/lib.target/libv8.so .
	#Debug
	# 	g++  -g -Wall -std=c++11 -shared -fPIC -I../ -I../V8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/   -Wl,-soname,libV8_Net_Proxy.so  -o libV8_Net_Proxy.so *.o ../V8/out/native/obj.host/testing/libgtest.a ../V8/out/native/obj.target/testing/libgmock.a ../V8/out/native/obj.target/testing/libgtest.a ../V8/out/native/obj.target/third_party/icu/libicudata.a ../V8/out/native/obj.target/tools/gyp/libv8_base.a ../V8/out/native/obj.target/tools/gyp/libv8_libbase.a ../V8/out/native/obj.target/tools/gyp/libv8_libplatform.a ../V8/out/native/obj.target/tools/gyp/libv8_nosnapshot.a ../V8/out/native/obj.target/tools/gyp/libv8_snapshot.a  -Wl,-rpath,. -L. -L../  -lpthread  -lstdc++ -licui18n -licuuc -lv8 -lglib-2.0 -lrt  -Wl,--verbose
	g++ -Wall -std=c++11 -shared -fPIC -I../ -I../V8/ -I/usr/include/glib-2.0/ -I/usr/lib/x86_64-linux-gnu/glib-2.0/include/   -Wl,-soname,libV8_Net_Proxy.so  -o libV8_Net_Proxy.so *.o ../V8/out/native/obj.host/testing/libgtest.a ../V8/out/native/obj.target/testing/libgmock.a ../V8/out/native/obj.target/testing/libgtest.a ../V8/out/native/obj.target/third_party/icu/libicudata.a ../V8/out/native/obj.target/tools/gyp/libv8_base.a ../V8/out/native/obj.target/tools/gyp/libv8_libbase.a ../V8/out/native/obj.target/tools/gyp/libv8_libplatform.a ../V8/out/native/obj.target/tools/gyp/libv8_nosnapshot.a ../V8/out/native/obj.target/tools/gyp/libv8_snapshot.a  -Wl,-rpath,. -L. -L../  -lpthread  -lstdc++ -licui18n -licuuc -lv8 -lglib-2.0 -lrt  -Wl,--verbose
	cp *.so ../../../BuildOutput/Debug
	cp *.so ../../../BuildOutput/Release
}

 buildV8DotNetWrapper (){
	currentFunction="Build V8DotNet Wrapper"
	cd $currentDir

	 printf '\e[1;34m%-6s\e[m \n' "Create Directories"
	 mkdir -p BuildOutput/{Debug,Release}
	 mkdir -p Source/V8.NET-Proxy/out
	
	mdtool -v build "--configuration:Release" "Source/V8.Net.MonoDevelop.sln"
	mdtool -v build "--configuration:Debug" "Source/V8.Net.MonoDevelop.sln"
	cp Source/V8.NET-Console/bin/Debug/* BuildOutput/Debug/
	cp Source/V8.NET-Console/bin/Release/* BuildOutput/Release/
}
 function helptext {
     echo -e $USAGE
    exit 1;
}


USAGE="$(basename "$0") [-h] [-l] [-d jobs] [-v8 jobs] -- program to build V8DotNet\n\n

Use: [$(basename "$0") --default 4] to build all\n\n

where:\n
    -h,  --help:     \t Show this help text\n
    -l,  --lib:      \t libV8_Net_Proxy.so only\n
    -v8, --v8:       \t Build Google V8 only \n
    -w, --w:         \t Build v8 wrapper \n
    -d, --default:   Build V8DotNet with extern Google V8\n\n

    param jobs: specifies the number of parallel build processes. Set it (roughly) to the number of CPU cores your machine has. 
    The GYP/make based V8 build also supports distcc, so you can compile with -j100 or so, provided you have enough machines around." 
    
if [ $# == 0 ] ; then
    echo -e $USAGE
    exit 1;
fi
#######################################

while [[ $# > 0 ]]
do
key="$1"
shift

case $key in
    -l|--lib)
    buildV8Proxy
    buildResult
    shift
    ;;
    -d|--default)
	JOBSV8=$1
    buildV8
    buildResult
    buildV8Proxy
    buildResult
    buildV8DotNetWrapper
    buildResult
    shift
    ;;
    -v8|--v8)
	JOBSV8=$1
    buildV8
    buildResult
    shift
    ;;
    -w|--w)
    buildV8DotNetWrapper
    buildResult
    shift
    ;;
    -h|--help)
    helptext
    shift
    ;;
    *)
            # unknown option
    ;;
esac
done