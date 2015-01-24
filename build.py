#!/usr/bin/python3
from __future__ import print_function
import argparse, sys, os,platform, shutil, subprocess
import itertools
 
v8_jobs=1
v8_target="ia32"
v8_mode="release"
base_dir = os.getcwd() # get current directory
v8_build_dir = base_dir + os.path.join ("/Source/V8.NET-Proxy/V8/") #v8 base dir
VSTools= "C:\\Program Files (x86)\\Microsoft Visual Studio 12.0\\Common7\\Tools\\VsDevCmd.bat"
VSVer=2013
choice=["ia32.debug", "ia32.release", "x64.release","x64.debug"]
msbuild_arc = {"ia32":"Win32", "x64":"x64"}


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


def validate_pair(ob):
    try:
        if not (len(ob) == 2):
            print("Unexpected result:", ob, file=sys.stderr)
            raise ValueError
    except:
        return False
    return True

def consume(iter):
    try:
        while True: next(iter)
    except StopIteration:
        pass

def get_environment_from_batch_command(env_cmd, initial=None):
    """
    Take a command (either a single command or list of arguments)
    and return the environment created after running that command.
    Note that if the command must be a batch file or .cmd file, or the
    changes to the environment will not be captured.

    If initial is supplied, it is used as the initial environment passed
    to the child process.
    """
    if not isinstance(env_cmd, (list, tuple)):
        env_cmd = [env_cmd]
    # construct the command that will alter the environment
    env_cmd = subprocess.list2cmdline(env_cmd)
    # create a tag so we can tell in the output when the proc is done
    tag = 'Done running command'
    # construct a cmd.exe command to do accomplish this
    cmd = 'cmd.exe /s /c "{env_cmd} && echo "{tag}" && set"'.format(**vars())
    # launch the process
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, env=initial)
    # parse the output sent to stdout
    lines = proc.stdout
    # consume whatever output occurs until the tag is reached
    consume(itertools.takewhile(lambda l: tag not in l, lines))
    # define a way to handle each KEY=VALUE line
    handle_line = lambda l: l.rstrip().split('=',1)
    # parse key/values into pairs
    pairs = map(handle_line, lines)
    # make sure the pairs are valid
    valid_pairs = filter(validate_pair, pairs)
    # construct a dictionary of the pairs
    result = dict(valid_pairs)
    # let the process finish
    proc.communicate()
    return result



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
    elif  platform.system() == 'Windows':
        if not os.environ["VS120COMNTOOLS"] == "": 
            VSTools=os.environ["VS120COMNTOOLS"] 
            VSVer="2013"
            os.environ["GYP_MSVS_VERSION"]=VSVer
            print("Visual Studio Version: " + VSVer)
        elif not os.environ["VS110COMNTOOLS"] == "": 
            VSTools=os.environ["VS110COMNTOOLS"] 
            VSVer="2012"
            os.environ["GYP_MSVS_VERSION"]= VSVer
            print("Visual Studio Version: " + VSVer)
        elif not  os.environ["VS100COMNTOOLS"] == "": 
            VSTools=os.environ["VS100COMNTOOLS"] 
            VSVer="2010"
            os.environ["GYP_MSVS_VERSION"] = VSVer
            print("Visual Studio Version: " + VSVer)
        else:
            raise Exception( "Failed to detect correct version of Visual Studio.\
              Please open the developer prompt and run the command file there.")
    if not platform.system() == 'Windows':    
        print('GYP_DEFINES=' + os.environ['GYP_DEFINES']);
        print('CXX=' + os.environ['CXX']);
        print('CC=' + os.environ['CC']);
        print('CPP=' + os.environ['CPP']);
        print('LINK=' + os.environ['LINK']);
        print('CXX_host=' + os.environ['CXX_host']);
        print('CC_host=' + os.environ['CC_host']);
        print('CPP_host=' + os.environ['CPP_host']);
        print('LINK_host=' + os.environ['LINK_host']);

def execute(command ):
    debug.info("Execute: " + command)
    pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
    error = pr.communicate()
    if pr.poll()  != 0: 
        raise Exception( "Faild to execute command:\n %s " % (error[1].decode('utf-8')))

def executeMsbuild(command ):
    env = get_environment_from_batch_command(VSTools)
    debug.info("Execute: " + command)
    pr = subprocess.Popen( command, env=env,  shell = True,  stderr = subprocess.PIPE )
    error = pr.communicate()
    if pr.poll()  != 0: 
        raise Exception( "Faild to execute command:\n %s " % (error[1].decode('utf-8')))

def clone (library, command):
    debug.info("Downloading  %s ..." % (library))
    if(os.path.isdir(command.split( )[3])==0):
        pr = subprocess.Popen( command,  shell = True,  stderr = subprocess.PIPE )
        error = pr.communicate()
        if pr.poll()  != 0: 
            raise Exception( "Faild Downloading %s ...  " % (error[1].decode('utf-8')))



def makeBuildDeps ():
    debug.info("Downloading V8 build dependencies")

    libraries = {  'GYP': 'git clone https://chromium.googlesource.com/external/gyp ' + os.path.join(v8_build_dir,  'build/gyp') , 
                'Cygwin': 'git clone https://chromium.googlesource.com/chromium/deps/cygwin ' + os.path.join(v8_build_dir,  'third_party/cygwin'),
                'python_26' : 'git clone https://chromium.googlesource.com/chromium/deps/python_26 ' + os.path.join(v8_build_dir,  'third_party/python_26'),
                'ICU' : 'git clone https://chromium.googlesource.com/chromium/deps/icu52 ' + os.path.join(v8_build_dir,  'third_party/icu'),
                'GTest' : 'git clone https://chromium.googlesource.com/chromium/testing/gtest  ' + os.path.join(v8_build_dir,  'testing/gtest'),
                'GMock' : 'git clone https://chromium.googlesource.com/external/gmock  ' + os.path.join(v8_build_dir,  'testing/gmock'),
            }
    for library, command in libraries.items():
        clone (library, command)
        



def buildV8 ():
    debug.info("Build V8 Javascript Engine")
    
    debug.info("Init V8 Submodule")
    pr = os.system("git submodule update --init --recursive")
    makeBuildDeps()

    
    
    debug.info("Create project V8 \n\
    Version 3.29.40 (based on bleeding_edge revision r23628)\n\
    commit 21d700eedcdd6570eff22ece724b63a5eefe78cb\n")
    
 
    if platform.system() == 'Windows':
        #create the solution
        if v8_mode == 'debug':
            command  = os.path.join(v8_build_dir, "third_party/python_26/python build/gyp_v8")  + " -debug -Dtarget_arch=%s \
             -Dcomponent=shared_library  -Dv8_use_snapshot=true -Dv8_enable_i18n_support=false " % (v8_target ) 
        else:
            command  =os.path.join(v8_build_dir, "third_party/python_26/python build/gyp_v8")  + " -Dtarget_arch=%s \
             -Dcomponent=shared_library  -Dv8_use_snapshot=true -Dv8_enable_i18n_support=false  " % (v8_target)
        os.chdir(v8_build_dir)
        execute(command)
        #build from solution
        

        command = "msbuild /v:detailed /p:Configuration=%s /p:Platform=%s /p:TreatWarningsAsErrors=false %s" % (v8_mode.title(), msbuild_arc[v8_target], os.path.join (v8_build_dir , "tools/gyp/v8.sln"))
        executeMsbuild(command)
        os.chdir(base_dir)
    else:
        os.chdir(v8_build_dir)
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
    if platform.system() == 'Windows':
        if v8_mode == 'debug':
            command =    "./Source/V8.NET-Proxy/V8/build/gyp/gyp -debug -Dbase_dir=%s -Dtarget_arch=%s -Dbuild_option=%s \
            -f make --depth=. v8dotnet.gyp  --generator-output=./Build/%s.%s/makefiles" % (base_dir, v8_target, v8_mode,v8_target, v8_mode)
        else:
            command ="/Source/V8.NET-Proxy/V8/build/gyp/gyp  -Dbase_dir=%s -Dtarget_arch=%s -Dbuild_option=%s \
            -f make --depth=. v8dotnet.gyp  --generator-output=./Build/%s.%s/makefiles" % (base_dir, v8_target, v8_mode,v8_target, v8_mode)
    else:
        if v8_mode == 'debug':
            command =    "./Source/V8.NET-Proxy/V8/build/gyp/gyp -debug -Dbase_dir=%s -Dtarget_arch=%s -Dbuild_option=%s \
              -f make --depth=. v8dotnet.gyp  --generator-output=./Build/%s.%s/makefiles" % (base_dir, v8_target, v8_mode,v8_target, v8_mode)
        else:
            command ="/Source/V8.NET-Proxy/V8/build/gyp/gyp  -Dbase_dir=%s -Dtarget_arch=%s -Dbuild_option=%s \
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
