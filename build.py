#!/usr/bin/python3
import argparse, sys, os,platform, shutil, subprocess
 


class Debug:
    """Info ouput"""
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'
    def info(_self,s):
        print (Debug.HEADER + s + Debug.ENDC)
    def command(_self,s):
        print (Debug.OKBLUE + s + Debug.ENDC)    

debug = Debug()
choice=["ia32.debug", "ia32.release", "x64.release","x64.debug"]
base_dir = os.getcwd() # get current directory
v8_jobs=1
v8_target="ia32"
v8_mode="release"

def exportVariables( ):
    if platform.system() == 'Linux':
        debug.info("Export Linux defines")
        result = subprocess.check_output(["which","clang++"])
        clangpp = result.decode('utf-8').rstrip('\r\n')
        result = subprocess.check_output(["which","clang"])
        clang = result.decode('utf-8').rstrip('\r\n')
        os.environ['GYP_DEFINES'] = 'clang=1'
        os.environ['CXX'] = clangpp + "  -v -std=c++11 -stdlib=libstdc++"
        os.environ['CC'] = clang + "  -v"
        os.environ['CPP'] = clang + " -E  -v"
        os.environ['LINK'] = clangpp + " -v -std=c++11 -stdlib=libstdc++"
        os.environ['CXX_host'] = clangpp + " -v"
        os.environ['CC_host'] = clang + "  -v"
        os.environ['CPP_host'] = clang + "  -E -v"
        os.environ['LINK_host'] = clangpp + "   -v"
    elif platform.system() == 'Darwin':
        debug.info("Export Mac defines")
        result = subprocess.check_output(["which","clang++"])
        clangpp = result.decode('utf-8').rstrip('\r\n')
        result = subprocess.check_output(["which","clang"])
        clang = result.decode('utf-8').rstrip('\r\n')
        os.environ['GYP_DEFINES'] = 'clang=1  mac_deployment_target=10.10'
        os.environ['CXX'] = clangpp + "  -v -std=c++11 -stdlib=libc++"
        os.environ['CC'] = clang + "  -v"
        os.environ['CPP'] = clang + " -E  -v"
        os.environ['LINK'] = clangpp + " -v -std=c++11 -stdlib=libc++"
        os.environ['CXX_host'] = clangpp + " -v"
        os.environ['CC_host'] = clang + "  -v"
        os.environ['CPP_host'] = clang + "  -E -v"
        os.environ['LINK_host'] = clangpp + "   -v"
    print('GYP_DEFINES=' + os.environ['GYP_DEFINES']);
    print('CXX=' + os.environ['CXX']);
    print('CC=' + os.environ['CC']);
    print('CPP=' + os.environ['CPP']);
    print('LINK=' + os.environ['LINK']);
    print('CXX_host=' + os.environ['CXX_host']);
    print('CC_host=' + os.environ['CC_host']);
    print('CPP_host=' + os.environ['CPP_host']);
    print('LINK_host=' + os.environ['LINK_host']);




def clone (library, command):
    debug.info("Downloading  %s ..." % (library))
    if(os.path.isdir(command.split( )[3])==0):
        pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
        error = pr.communicate()
        if pr.poll()  != 0: 
            raise Exception( "Faild Downloading %s ...  " % (error[1].decode('utf-8')))



def makeBuildDeps ():
    debug.info("Downloading V8 build dependencies")
    os.chdir('Source/V8.NET-Proxy/V8/')
    os.chdir(base_dir)

    libraries = {  'GYP': 'git clone https://chromium.googlesource.com/external/gyp  build/gyp', 
                'Cygwin': 'git clone https://chromium.googlesource.com/chromium/deps/cygwin third_party/cygwin',
                'python_26' : 'git clone https://chromium.googlesource.com/chromium/deps/python_26 third_party/python_26',
                'ICU' : 'git clone https://chromium.googlesource.com/chromium/deps/icu52 third_party/icu',
                'GTest' : 'git clone https://chromium.googlesource.com/chromium/testing/gtest  testing/gtest',
                'GMock' : 'git clone https://chromium.googlesource.com/external/gmock  testing/gmock',
            }
    for library, command in libraries.items():
        clone (library, command)
        
    os.chdir(base_dir)


def buildV8 ():
    debug.info("Build V8 Javascript Engine")

    debug.info("Init V8 Submodule")
    pr = os.system("git submodule update --init --recursive")
    debug.info("Building V8 \n\
    Version 3.29.40 (based on bleeding_edge revision r23628)\n\
    commit 21d700eedcdd6570eff22ece724b63a5eefe78cb\n")
    os.chdir('Source/V8.NET-Proxy/V8/')
 
    if platform.system() == 'Windows':
        makeBuildDeps()
        if v8_mode == 'debug':
            command  = "third_party/python_26/python build/gyp_v8 -debug -Dtarget_arch=%s \
             -Dcomponent=shared_library  -Dv8_use_snapshot=true -Dv8_enable_i18n_support=false " % (v8_target ) 
        else:
            command  = "third_party/python_26/python build/gyp_v8 -Dtarget_arch=%s \
             -Dcomponent=shared_library  -Dv8_use_snapshot=true -Dv8_enable_i18n_support=false  " % (v8_target)
       
        debug.command(command)
        pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
        error = pr.communicate()
        if pr.poll()  != 0: 
            raise Exception( "Faild  %s ...  " % (error[1].decode('utf-8')))
    else:
        command = "make builddeps -j %s " % (v8_jobs)
        debug.command(command)
        pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
        error = pr.communicate()
        if pr.poll()  != 0: 
            raise Exception( "Faild Downloading %s ...  " % (error[1].decode('utf-8')))
        command = "make %s.%s library=shared snapshot=yes i18nsupport=off -j %s"  % (v8_target,v8_mode, v8_jobs)
        debug.command(command)
        pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
        error = pr.communicate()
        if pr.poll()  != 0: 
            raise Exception( "Faild Downloading %s ...  " % (error[1].decode('utf-8')))
    os.chdir(base_dir)


def buildV8Proxy ():
    srcDir = "Build/%s.%s/makefiles/out/Default/lib.target/"% (v8_target, v8_mode)
    destDir = "BuildResult/" + v8_mode

    debug.info('Build V8.Net native Proxy %s.%s' % (v8_target, v8_mode))
    if not os.path.exists(destDir):
        os.makedirs(destDir)
     
    makeBuildDeps()

    if v8_mode == 'debug':
        command =    "./Source/V8.NET-Proxy/V8/build/gyp/gyp -debug -Dbase_dir=%s -Dtarget_arch=%s -Dbuild_option=%s \
          -f make --depth=. v8dotnet.gyp  --generator-output=./Build/%s.%s/makefiles" % (base_dir, v8_target, v8_mode,v8_target, v8_mode)
    else:
        command ="./Source/V8.NET-Proxy/V8/build/gyp/gyp  -Dbase_dir=%s -Dtarget_arch=%s -Dbuild_option=%s \
          -f make --depth=. v8dotnet.gyp  --generator-output=./Build/%s.%s/makefiles" % (base_dir, v8_target, v8_mode,v8_target, v8_mode)
    
    debug.command(command)
    pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
    error = pr.communicate()
    if pr.poll()  != 0: 
        raise Exception( "Create Project Faild  %s ...  " % (error[1].decode('utf-8')))

    command = "V=1 make -C ./Build/%s.%s/makefiles" % (v8_target, v8_mode)
    debug.command(command)
    pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
    error = pr.communicate()
    if pr.poll()  != 0: 
        raise Exception( "Building Faild  %s ...  " % (error[1].decode('utf-8')))

    if  os.path.isfile(srcDir + "libV8_Net_Proxy.so"):
        shutil.copy2(srcDir + "libV8_Net_Proxy.so", destDir)
    if  os.path.isfile(srcDir + "libV8_Net_Proxy.dylib"):
        shutil.copy2(srcDir + "libV8_Net_Proxy.dylib", destDir)


def buildV8DotNetWrapper ():

    debug.info("Building V8.Net Wrapper....")
    destDir = "BuildResult/" + v8_mode
    if not os.path.exists(destDir):
        os.makedirs(destDir)

    if  platform.system() == 'Windows':
        command = "msbuild /p:Configuration=%s /p:Platform=%s /p:TreatWarningsAsErrors=false V8.Net.sln" % (v8_mode, v8_target)
    else:
        command = "xbuild /p:Configuration=%s Source/V8.Net.MonoDevelop.sln" % (v8_mode)
            
    debug.command(command)
    pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
    error = pr.communicate()
    if pr.poll()  != 0: 
        raise Exception( "Building Faild  %s ...  " % (error[1].decode('utf-8')))

    
    src = "Tests/V8.NET-Console/bin/Debug/"
    src_files = os.listdir(src)
    for file_name in src_files:
        full_file_name = os.path.join(src, file_name)
        if (os.path.isfile(full_file_name)):
            shutil.copy(full_file_name, "BuildResult/rebug/")

    src = "Tests/V8.NET-Console/bin/Release/"
    src_files = os.listdir(src)
    for file_name in src_files:
        full_file_name = os.path.join(src, file_name)
        if (os.path.isfile(full_file_name)):
            shutil.copy(full_file_name, "BuildResult/release/")









parser = argparse.ArgumentParser(description='Script to build V8.Net')
parser.add_argument('-d', '--default',  nargs=1,  choices=choice, help='Default builds all. Use "-d ia32.debug -j 2"  to build V8, V8 Proxy, V8 Wrapper and Nuget. Supported options are: ' + ',' .join(choice) , metavar='')
parser.add_argument('-v8','--v8',       nargs=1,  choices=choice, help='Build V8. Use "-v8 ia32.debug -j 2" Supported options are: ' + ',' .join(choice), metavar='')
parser.add_argument('-l', '--lib',      nargs=1,  choices=choice, help='Build V8.Net native proxy. Use "-l ia32.debug" Supported options are: ' + ',' .join(choice), metavar='')
parser.add_argument('-w', '--wrapper', action='store_true', help='Build V8.Net Wrapper')
parser.add_argument('-n', '--nuget',   action='store_true', help='Pack V8.Net Nuget')
parser.add_argument('-j', '-j',         help='Number of cores')
args = parser.parse_args()
if args.default:
    v8_jobs=args.j
    v8_target, v8_mode = args.default[0].split('.')
    exportVariables()
    buildV8()
    buildV8Proxy()

elif args.v8:
    exportVariables()
    v8_target, v8_mode = args.v8[0].split('.')
elif args.lib:
    v8_target, v8_mode = args.lib[0].split('.')
    exportVariables()
    buildV8Proxy()
elif args.wrapper:
    buildV8DotNetWrapper()
elif args.nuget:
    print("command nuget: " + u);
