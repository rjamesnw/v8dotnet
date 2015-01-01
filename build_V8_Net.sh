#!/bin/bash
currentDir=`pwd`
currentFunction=""
error=1
JOBSV8=1
TARGETARCHITECTURE="x64.release"
v8_net_target="x64"
v8_net__mode="release"


debugInfo (){
	case $1 in 
		0)
		printf '\e[1;32m%-6s\e[m \n' "+++ $2 successfull +++"
		;;
		1)
		printf '\e[1;31m%-6s\e[m \n' "+++ $2 failed +++"
		exit $1
		;;
		2)
		printf '\e[1;34m%-6s\e[m \n' "+++ $2 +++"
		;;
		esac	  
		}

buildV8 (){

	debugInfo 2 "Build V8 Javascript Engine"
	cd $currentDir

	debugInfo 2 "Init V8 Submodule"
	git submodule update --init --recursive
	
	debugInfo 2 "Building V8 \n
	Version 3.29.40 (based on bleeding_edge revision r23628)\n
	commit 21d700eedcdd6570eff22ece724b63a5eefe78cb\n
	make builddeps -j ${JOBSV8}"
	cd Source/V8.NET-Proxy/V8/
	make builddeps -j ${JOBSV8}
	debugInfo 2 "make ${v8_net_target}.${v8_net__mode} library=shared gdbjit=on -j ${JOBSV8}"
	make  "${v8_net_target}.${v8_net__mode}" library=shared -j ${JOBSV8}
	debugInfo $? "make ${v8_net_target}.${v8_net__mode} library=shared Build V8 Javascript Engine"

	cd $currentDir
}

buildV8Proxy (){

	debugInfo 2 "Build V8.Net native Proxy ${v8_net_target}.${v8_net__mode}"
	cd $currentDir
	
	debugInfo 2 "Create Directories BuildResult/{Debug,Release}"
	mkdir -p BuildResult/{Debug,Release}

	debugInfo 2 "Build V8.Net native proxy (libV8_Net_Proxy.so )"
	debugFlag=" "

	if [ "${v8_net__mode}" == "debug" ]
		then 	./gyp/gyp -debug -Dbase_dir=`pwd` -Dtarget_arch="${v8_net_target}" -Dbuild_option="${v8_net__mode}"  -f make --depth=. v8dotnet.gyp  --generator-output="./Build/${v8_net_target}.${v8_net__mode}/makefiles"
	fi

	./gyp/gyp  -Dbase_dir=`pwd` -Dtarget_arch="${v8_net_target}" -Dbuild_option="${v8_net__mode}"  -f make --depth=. v8dotnet.gyp  --generator-output="./Build/${v8_net_target}.${v8_net__mode}/makefiles"
	make -C "./Build/${v8_net_target}.${v8_net__mode}/makefiles"
	echo $?
	debugInfo $? "make V8.Net Proxy for ${v8_net_target}.${v8_net__mode}"
	
	#copy resulting files
	if ! cp "Build/${v8_net_target}.${v8_net__mode}/makefiles/out/Release"/lib.target/*.so BuildResult/Release
		then  debugInfo 1 "Copy libV8_Net_Proxy.so"
	fi

	if ! cp "Build/${v8_net_target}.${v8_net__mode}"/makefiles/*.so BuildResult/Release
		then  debugInfo 1 "Copy V8 natives: libicui18n.so libicuuc.so libv8.so"
	fi

	debugInfo 0 "Build V8.Net native Proxy"

}

buildV8DotNetWrapper (){

	debugInfo 2  "Build V8.Net Wrapper"
	cd $currentDir

	printf '\e[1;34m%-6s\e[m \n' "Create Directories"
	mkdir -p BuildResult/{Debug,Release}
	
	xbuild /p:Configuration=Release Source/V8.Net.MonoDevelop.sln
	debugInfo $? "xbuild release" 
	xbuild /p:Configuration=Debug Source/V8.Net.MonoDevelop.sln
	debugInfo $? "xbuild debug" 
	
	if ! cp Source/V8.NET-Console/bin/Debug/* BuildResult/Debug/
		then  debugInfo 1 "Copy V8 natives: libicui18n.so libicuuc.so libv8.so"
	fi

	if ! cp Source/V8.NET-Console/bin/Release/* BuildResult/Release/
		then  debugInfo 1 "Copy V8 natives: libicui18n.so libicuuc.so libv8.so"
	fi
	
}

buildV8DotNetNuget () {

	debugInfo 2 "Build V8.Net Nuget"
	cd $currentDir
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
	debugInfo $? "Pack Nuget"

	if [ -f Build/V8dotNetNuget/*.nupkg ]
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


USAGE="$(basename "$0") [-h] [-l mode ] [-n ][-d mode cores] [-v8 mode cores] -- program to build V8.Net\n\n

Use: [ $(basename "$0") --default 4 ] to build all with 4 cores \n\n

where:\n
-h,  --help:     \t Show this help text\n
-l,  --lib:      \t Build V8.Net native proxy (libV8_Net_Proxy.so )\n
-v8, --v8:       \t Build Google V8 only \n
-w, --wrapper:\t Build V8.Net (managed wrapper)\n
-d, --default:\t Build all (V8.Net and Google V8)\n
-n, --nuget:\t Pack V8.Net Nuget \n\n

param core:\t specifies the number of parallel build processes.\n \t\tSet it (roughly) to the number of CPU cores your machine has.\n\n
param mode:\t specifies the target and mode for each architecture.\n \t\t (ia32, x64, arm, arm64) and mode (debug or release)\n \t\t eg. x64.release, ia32.debug."


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
		TARGETARCHITECTURE=$1
		v8_net_target="${TARGETARCHITECTURE%.*}"
		v8_net__mode="${TARGETARCHITECTURE##*.}"
		buildV8Proxy
		shift
		;;
		-d|--default)
		TARGETARCHITECTURE=$1
		v8_net_target="${TARGETARCHITECTURE%.*}"
		v8_net__mode="${TARGETARCHITECTURE##*.}"
		JOBSV8=$2
		buildV8
		buildV8Proxy
		buildV8DotNetWrapper
		buildV8DotNetNuget
		cd BuildResult/Release/
		mono V8.Net-Console.exe \all
		cd $currentDir
		shift
		;;
		-v8|--v8)
		TARGETARCHITECTURE=$1
		v8_net_target="${TARGETARCHITECTURE%.*}"
		v8_net__mode="${TARGETARCHITECTURE##*.}"
		JOBSV8=$2
		buildV8
		shift
		;;
		-w|--wrapper)
		buildV8DotNetWrapper
		shift
		;;
		-n|--nugget)
		buildV8DotNetNuget
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
