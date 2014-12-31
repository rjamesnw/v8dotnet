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
	
    printf '\e[1;34m%-6s\e[m \n' "Create Directories BuildResult/{Debug,Release}"
	mkdir -p BuildResult/{Debug,Release}

	printf '\e[1;34m%-6s\e[m \n' "Build V8.Net native proxy (libV8_Net_Proxy.so )"

    ./gyp/gyp  -Dbase_dir=`pwd` -Dtarget_arch="x64" -Dbuild_option="release" -f make --depth=. v8dotnet.gyp  --generator-output=./Build/x64.release/makefiles
     V=1 make -C ./Build/x64.release/makefiles
     currentFunction="make x64.release"
     buildResult
    
    ./gyp/gyp -debug -Dbase_dir=`pwd` -Dtarget_arch="x64" -Dbuild_option="debug" -f make --depth=. v8dotnet.gyp  --generator-output=./Build/x64.debug/makefiles
 	 V=1 make -C ./Build/x64.debug/makefiles
     currentFunction="make x64.debug"
     buildResult
	
	#copy resulting files
	#Release
	cp Build/x64.release/makefiles/out/Release/lib.target/*.so BuildResult/Release
	cp Build/x64.release/makefiles/*.so BuildResult/Release
	#Debug
	cp Build/x64.debug/makefiles/out/Release/lib.target/*.so BuildResult/Debug
	cp Build/x64.debug/makefiles/*.so BuildResult/Debug
	currentFunction="Build V8 native Proxy"
}

 buildV8DotNetWrapper (){
	currentFunction="Build V8.Net Wrapper"
	cd $currentDir

	 printf '\e[1;34m%-6s\e[m \n' "Create Directories"
	 mkdir -p BuildResult/{Debug,Release}
	
	xbuild /p:Configuration=Release Source/V8.Net.MonoDevelop.sln /verbosity:detailed
	xbuild /p:Configuration=Debug Source/V8.Net.MonoDevelop.sln /verbosity:detailed
	cp Source/V8.NET-Console/bin/Debug/* BuildResult/Debug/
	cp Source/V8.NET-Console/bin/Release/* BuildResult/Release/
}

buildV8DotNetNuget () {
	currentFunction="Build V8.Net Nuget"
    printf '\e[1;34m%-6s\e[m \n' "Create Directories BuildResult/{Debug,Release}"
	mkdir -p BuildResult/{Debug,Release}
	mkdir -p Build/V8dotNetNuget

	cp -r BuildResult/Release Build/V8dotNetNuget/
	cp -r Source/V8.Net.Mono.Nuget/* Build/V8dotNetNuget/

	if [ ! -f Build/V8dotNetNuget/nuget.exe ]
	  then
	    wget --directory-prefix=Build/V8dotNetNuget/ http://nuget.org/nuget.exe
	fi
	
	 mono Build/V8dotNetNuget/nuget.exe pack Build/V8dotNetNuget/v8dotnet.nuspec   -OutputDirectory "${currentDir}/Build/V8dotNetNuget/" -Verbosity detailed 
	 rm V8.Net.Mono.*.nupkg
	if [ ! -f Build/V8dotNetNuget/*.nupkg ]
	  then
	    cp Build/V8dotNetNuget/V8.Net.Mono.*.nupkg BuildResult
	  else
	  	exit 1
	fi	 
}

  helptext (){
     echo -e $USAGE
    exit 1;
}


USAGE="$(basename "$0") [-h] [-l] [-d cores] [-v8 cores] -- program to build V8.Net\n\n

Use: [ $(basename "$0") --default 4 ] to build all with 4 cores \n\n

where:\n
    -h,  --help:     \t Show this help text\n
    -l,  --lib:      \t Build V8.Net native proxy (libV8_Net_Proxy.so )\n
    -v8, --v8:       \t Build Google V8 only \n
    -w, --wrapper:\t Build V8.Net (managed wrapper)\n
    -d, --default:\t Build all (V8.Net and Google V8)\n\n

    param jobs: specifies the number of parallel build processes. Set it (roughly) to the number of CPU cores your machine has."
    
    
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
    buildV8DotNetNuget
    buildResult
    shift
    ;;
    -v8|--v8)
	JOBSV8=$1
    buildV8
    buildResult
    shift
    ;;
    -w|--wrapper)
    buildV8DotNetWrapper
    buildResult
    shift
    ;;
    -n|--nugget)
    buildV8DotNetNuget
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