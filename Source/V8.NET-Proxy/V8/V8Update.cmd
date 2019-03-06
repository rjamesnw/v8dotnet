@echo off

set installto=C:\ProgramData\Google\V8\src\
set v8proxydir=%~dp0
cd "%v8proxydir%"
Echo Current path is: "%CD%"
REM if not exist "v8update.cmd" echo The working directory must be the same path where this command file exists. && goto :eof
Echo The target install location is: %installto%

echo This command file will try to download and build the source automatically.
pause

Echo.
Echo Checking for dependencies ...
Echo.

where git
if not %errorlevel%==0 echo "git" was not found; make sure it is installed and in the path. && goto :eof

where gclient
if not %errorlevel%==0 echo Google Depot Tools are not installed. See readme.txt. && goto :eof

where fetch
if not %errorlevel%==0 echo Google depot tool "fetch" not found. Make sure the paths are correct; See readme.txt. && goto :eof

if not "%DEPOT_TOOLS_WIN_TOOLCHAIN%"=="0" echo You forgot to set DEPOT_TOOLS_WIN_TOOLCHAIN=0 in the system environment variables.&&Echo After update you'll need to open a NEW command window. && goto :eof

if "%GYP_MSVS_VERSION%"=="" echo You forgot to set GYP_MSVS_VERSION=2017 (or greater) in the system environment variables.&&Echo After update you'll need to open a NEW command window. && goto :eof

:gitconfig
Echo.
Echo Configuring the client settings ...
Echo Warning: This updates global git configurations, which is required by Depot Tools.
Echo Please review these (pressing enter will use defaults):
Echo.
set /P name=Enter your name:
set /P email=Enter your email:
Echo.
if "%name%"=="" set name=%USERNAME%
if "%email%"=="" if not "%USERDNSDOMAIN%"=="" set email=%USERNAME%@%USERDNSDOMAIN%
if "%email%"=="" if not "%USERDOMAIN%"=="" set email=%USERNAME%@%USERDOMAIN%
if "%email%"=="" if not "%WORKSTATION%"=="" set email=%USERNAME%@%WORKSTATION%
if "%email%"=="" set email=%USERNAME%@%COMPUTERNAME%
Echo * git config --global user.name "%name%"
Echo * git config --global user.email "%email%"
Echo * git config --global core.autocrlf false
Echo * git config --global core.filemode false
Echo * git config --global color.ui true
Echo.
Echo If any are not correct then you need to run the ones you need manually.
choice /C ARC /M "Press A to abort, R to re-enter your details, or C to continue:"
if %errorlevel%==1 goto :eof
if %errorlevel%==2 goto gitconfig
Echo.

git config --global user.name "%name%"
git config --global user.email "%email%"
git config --global core.autocrlf false
git config --global core.filemode false
git config --global color.ui true

Echo.
Echo Manking sure all tools are up to date ...
Echo.

call gclient

if not %errorlevel%==0 echo Error running "gclient". && goto :eof

where python.bat
if not %errorlevel%==0 echo "python.bat" from depot tools was not found; make sure the paths are correct (depot tools must be first). && goto :eof

Echo.
Echo Downloading the source ...
Echo.

if not exist "%installto%" md "%installto%"
if not %errorlevel%==0 Echo Failed to create directory "%installto%".&&Echo Make sure to run within an administrator prompt.&&goto :eof
cd "%installto%"
Echo (if this fails and you need to return to "%v8proxydir%" type "cd %%v8proxydir%%")
Echo.
REM Note: cd. clears the error level.
cd.
if exist "v8\" Echo "src\V8\" already exists, so skipping "fetch". && choice /C YN /M "Continue?" /T 30 /D Y
if %errorlevel%==1 goto updatedep
if %errorlevel%==2 goto :eof
call fetch v8 && if not %errorlevel%==0 echo Error fetching V8 source. && goto :eof

:updatedep
Echo.
Echo Updating V8 dependencies ...
Echo.

if exist "v8\" cd v8
call gclient sync

if not %errorlevel%==0 echo Error running "gclient sync". Try running "gclient revert" and try again. && choice /C YN /M "Ignore the errors?"
if %errorlevel%==2 goto :eof

Echo.
Echo Generating the build files (if you wish to regenerate them you have to remove them first) ...
Echo.
cd.

if not exist "out.gn/x64.debug/" (call python tools\dev\v8gen.py x64.debug) else (echo "out.gn/x64.debug/" already exists; skipped.)
if not %errorlevel%==0 echo Error running "python tools\dev\v8gen.py x64.debug". && goto :eof
if not exist "out.gn/x64.release/" (call python tools\dev\v8gen.py x64.release) else (echo "out.gn/x64.release/" already exists; skipped.)
if not %errorlevel%==0 echo Error running "python tools\dev\v8gen.py x64.release". && goto :eof
if not exist "out.gn/ia32.debug/" (call python tools\dev\v8gen.py ia32.debug) else (echo "out.gn/ia32.debug/" already exists; skipped.)
if not %errorlevel%==0 echo Error running "python tools\dev\v8gen.py ia32.debug". && goto :eof
if not exist "out.gn/ia32.release/" (call python tools\dev\v8gen.py ia32.release) else (echo "out.gn/ia32.release/" already exists; skipped.)
if not %errorlevel%==0 echo Error running "python tools\dev\v8gen.py ia32.release". && goto :eof
 
Echo.
Echo Updating build arguments ...
Echo.

set baseArgs=is_component_build=false v8_static_library=true use_custom_libcxx=false use_custom_libcxx_for_host=false v8_use_external_startup_data=false is_clang=false

call gn gen out.gn/x64.debug --args="is_debug=true target_cpu=""x64"" %baseArgs%"
call gn gen out.gn/x64.release --args="is_debug=false target_cpu=""x64"" %baseArgs%"
call gn gen out.gn/ia32.debug --args="is_debug=true target_cpu=""x86"" %baseArgs%"
call gn gen out.gn/ia32.release --args="is_debug=false target_cpu=""x86"" %baseArgs%"

if not %errorlevel%==0 echo Error running "gn gen" to create arguments. && goto :eof

Echo.
Echo Building the source ...
choice /C YN /T 10 /D Y /M "Continue? (auto starts in 10 seconds)"
if %errorlevel%==2 goto :eof
Echo.

ninja -C out.gn/x64.debug v8
if not %errorlevel%==0 echo Error running "ninja -C out.gn/x64.debug v8". && goto :eof
ninja -C out.gn/ia32.debug v8
if not %errorlevel%==0 echo Error running "ninja -C out.gn/ia32.debug v8". && goto :eof
ninja -C out.gn/x64.release v8
if not %errorlevel%==0 echo Error running "ninja -C out.gn/x64.release v8". && goto :eof
ninja -C out.gn/ia32.release v8
if not %errorlevel%==0 echo Error running "ninja -C out.gn/ia32.release v8". && goto :eof

Echo.
echo Done!
pause
