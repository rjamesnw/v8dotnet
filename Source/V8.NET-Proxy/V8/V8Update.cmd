@echo off
setlocal

REM The target V8 folder where the V8 source should be downloaded.
REM !!!WARNING!!! The path should NOT have spaces in it.
set TARGET_V8_FOLDER=%ProgramData%\Google\V8Engine

set errorlevel=
set v8rev=889d205b523c19e34c75770ff045a4c65d530635
set GIT_CURL_VERBOSE=1
set DEPOT_TOOLS_WIN_TOOLCHAIN=0
set GYP_MSVS_VERSION=2015
set GYP_GENERATORS=msvs
REM set GYP_GENERATORS=ninja
set V8NETPROXYPATH=%~dp0..

REM (This tells depot_tools to use the locally installed version of Visual Studio, otherwise by default, depot_tools will try to use a google-internal version)
set DEPOT_TOOLS_WIN_TOOLCHAIN=0

REM Note: Leave 'v8rev' empty for the HEAD V8 revision.

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

REM (in order for "%errorlevel%" to expand correctly, we need to make sure there's no matching environment variable set with the same name)
set errorlevel=

if not exist "%V8NETPROXYPATH%\..\V8.NET-Proxy\V8" echo You must run this in the "V8.NET-Proxy\V8" project folder. & pause & goto :EOF

if exist "%TARGET_V8_FOLDER%" goto SetWD
echo The path "%TARGET_V8_FOLDER%" does not exist.
choice /C YN /M "Create it now? (Y/N)"
if errorlevel 2 echo A target V8 download location is required. Please set a proper location in&echo this batch file by updating 'TARGET_V8_FOLDER'.&pause&goto :eof
md "%TARGET_V8_FOLDER%"

:SetWD
set WorkingDir=%TARGET_V8_FOLDER%

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

:Restart

cd "%WorkingDir%"

cls

echo This command file is used to download the V8 source and build it.
echo The Goole Depot Tools are required (see ReadMe.txt).
where /q gclient.bat
if errorlevel 1 echo Warning: Depot Tools not detected in the environment path.
echo V8.NET-Proxy location: %V8NETPROXYPATH%
echo Working Location: %WorkingDir%
echo.
echo 1. Open ReadMe.txt
echo 2. Download V8 source
echo 3. Update build tools (gclient sync)
echo 4. Build the V8 source
echo 5. See list of V8 build configurations
echo 6. Open the command prompt with the existing environment
echo 7. Exit

choice /C 1234567 /M "Please select one:"
echo.

if errorlevel 7 goto :EOF
if errorlevel 6 cmd&goto Restart
if errorlevel 5 goto ListBuildConfigs
if errorlevel 4 goto CheckEnv
if errorlevel 3 goto UpdateTools
if errorlevel 2 goto GetSrc

notepad "%cd%\ReadMe.txt"

goto restart

:ListBuildConfigs
if exist v8 cd v8
if not exist tools/dev/v8gen.py echo You must download V8 and update the tools first.&pause&goto Restart
cmd /C python tools/dev/v8gen.py list
pause
goto Restart

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

:ToolsNotFound
set notools=true
echo You need to install the depot tools correctly first.
echo.
echo 1. Open the location now.
echo    * Please follow the instructions on the page so the environment is configured
echo      currently. Most importantly, python.bat must be found before python.exe in
echo      the path environment variable.
echo 2. Run 'gclient' now (needs to be run at least once first, if not already).
echo 3. Go back.
echo.
choice /C 123 /M "Please select one:"
if errorlevel 3 goto :eof
if errorlevel 2 goto trygclient
echo Opening https://www.chromium.org/developers/how-tos/install-depot-tools ...
start https://www.chromium.org/developers/how-tos/install-depot-tools
goto :eof

:trygclient
cmd /C gclient
pause
goto :eof

:VerifyTools

set notools=false
where gclient>nul
if errorlevel 1 echo 'gclient' not found.&goto ToolsNotFound
where python.bat>nul
if errorlevel 1 echo 'python.bat' not found.&goto ToolsNotFound
where python|find "python.bat">nul
if errorlevel 1 echo 'python.bat' was not found first. Please check your &goto ToolsNotFound
goto :EOF

:UpdateTools

call :VerifyTools
if "%notools%"=="true" goto restart
REM cmd /C gclient config https://chromium.googlesource.com/v8/v8.git
Echo Updating ...
REM (Attempt to change to the location of the client, which must be in a path without spaces [in case the current directory does have spaces])
REM for /f %%i in ('where gclient.bat') do cd "%%i\.."
echo Currently in %CD%.
REM (note: gclient sync must be run in the folder where the '.gclient' config file is)
cmd /C gclient sync
pause

goto restart

:GetSrc

call :VerifyTools

if "%notools%"=="true" goto restart
if "%v8rev%"=="" set v8rev=HEAD
REM echo V8 revision: %v8rev%
REM choice /M "Is the revision ok? If not, you have to update the revision at the top of this command file."
REM if errorlevel 2 goto restart

if not exist v8\ goto BeginSrcDownload

echo The V8 source files already exist. 
echo 1. Delete and redownload
echo 2. Update to the revision %v8rev%.
echo 3. Main Menu

choice /C 123
echo.

if errorlevel 3 goto Restart
if errorlevel 2 goto UpdateToRev

:redownload
echo Removing cloned V8 repository and related files ...
del .gclient*
rd /s /q v8

:BeginSrcDownload
echo Downloading V8 ...
cmd /C fetch --no-history v8>getv8.log
if errorlevel 1 goto Error
echo Completed.
pause
goto Restart

REM *** The grouped lines below are not used anymore at the moment ***
REM if not "%v8rev%"=="" git checkout %v8rev%
REM if errorlevel 1 goto Error
:UpdateToRev
cd v8
git reset --hard
git checkout %v8rev%
REM git pull --rebase origin master
pause
goto Restart

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

:CheckEnv

if not exist v8\ echo V8 Source not downloaded. & pause & goto restart

call :VerifyTools
if "%notools%"=="true" goto restart

if exist v8 cd v8

REM Check if we are running in the correct environment (supports VS2010-VS2015) ...
REM (note: more than one version of Visual Studio may be installed, so start from highest version, to least)

if not "%DevEnvDir%"=="" set VSTools=%DevEnvDir%..\Tools\ & goto BeginV8Compile

echo This command file should be run via the Visual Studio developer prompt.
echo Attempting to do so now ...

if not "%VS140COMNTOOLS%"=="" set VSTools=%VS140COMNTOOLS%& set VSVer=2015& goto SetVSEnv
if not "%VS120COMNTOOLS%"=="" set VSTools=%VS120COMNTOOLS%& set VSVer=2013& goto SetVSEnv
if not "%VS110COMNTOOLS%"=="" set VSTools=%VS110COMNTOOLS%& set VSVer=2012& goto SetVSEnv
if not "%VS100COMNTOOLS%"=="" set VSTools=%VS100COMNTOOLS%& set VSVer=2010& goto SetVSEnv

echo Failed to detect correct version of Visual Studio.  Please open the developer prompt and run the command file there.

goto Exit

:SetVSEnv
Echo Visual Studio %VSVer% detected ...
set GYP_MSVS_VERSION=%VSVer%
call "%VSTools%VsDevCmd.bat"
echo Visual Studio developer prompt environment was setup successfully!
echo.

:BeginV8Compile
cd %WorkingDir%
echo Visual Studio environment being used: %DevEnvDir%
echo.

:SetMode
set mode=%1
if "%mode%"=="" goto PromptReleaseMode
if /i "%mode%"=="debug" goto DebugMode
if /i "%mode%"=="release" goto ReleaseMode
echo %mode%: Invalid build mode; please specify "debug" or "release"
goto PromptReleaseMode
:DebugMode
set mode=debug
goto Start
:ReleaseMode
set mode=release
goto Start
:PromptReleaseMode
choice /C DR /M "Please choose a build mode: [D]ebug, or [R]elease"
echo.
if errorlevel 2 goto ReleaseMode
if errorlevel 1 goto DebugMode

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

:Start
echo Build mode: %mode%

:BuildV8

if exist v8 cd v8

echo Building 32-bit V8 ...

cmd /C python tools/dev/v8gen.py ia32.%mode%

if errorlevel 1 echo Failed to create build configuration.&goto Error

set LogFile=%CD%\build.log
cmd /C ninja -C out.gn/ia32.%mode%
if errorlevel 1 goto Error
set LogFile=

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

if not "%PROCESSOR_ARCHITECTURE%"=="x86" goto 64BitSupported
echo.
echo While compiling the 64-bit version of V8 on a 32-bit system will work, the last
echo build step will fail for some reason (even though the compile will succeed).
echo If you continue, an error may occur, but the 64-bit DLLs will be present.
echo Just restart this script a second time and skip this compile and the 64-bit
echo DLLs will be copied correctly.
choice /M "Attempt to build 64-bit version?"
echo.
if errorlevel 2 goto ImportLibs

:64BitSupported

echo Building 64-bit V8 ...

cmd /C python tools/dev/v8gen.py x64.%mode%

if errorlevel 1 echo Failed to create build configuration.&goto Error

set LogFile=%CD%\build.log
cmd /C ninja -C out.gn/x64.%mode%
if errorlevel 1 goto Error
set LogFile=


:ImportLibs
echo Importing V8 libraries ...

REM *** .NET 4.0 ***

xcopy out.gn\ia32.%mode%\*.dll "%V8NETPROXYPATH%\bin\%mode%\x86\" /Y >nul
if errorlevel 1 goto Error

xcopy out.gn\ia32.%mode%\*.pdb "%V8NETPROXYPATH%\bin\%mode%\x86\" /Y >nul
if errorlevel 1 goto Error

xcopy out.gn\x64.%mode%\*.dll "%V8NETPROXYPATH%\bin\%mode%\x64\" /Y >nul
if errorlevel 1 goto Error

xcopy out.gn\x64.%mode%\*.pdb "%V8NETPROXYPATH%\bin\%mode%\x64\"  /Y >nul
if errorlevel 1 goto Error


:success
echo Success!
pause
goto Restart

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

:Error
echo *** THE PREVIOUS STEP FAILED ***

:ErrorOptions
REM (in order for "%errorlevel%" to expand correctly, we need to make sure there's no matching environment variable set with the same name)
set errorlevel=
echo.
echo Options:
if not "%LogFile%"=="" echo   [L]og: Open the log file (%LogFile%)
echo   [C]md: Open the command prompt with the existing environment
echo   [R]estart
echo   [E]xit
echo.
if "%LogFile%"=="" choice /C ERCL & goto ErrOptResponse
if not "%LogFile%"=="" choice /C ERCL & goto ErrOptResponse
:ErrOptResponse
if "%errorlevel%"=="1" goto End
if "%errorlevel%"=="2" goto Restart
if "%errorlevel%"=="3" cmd
if "%errorlevel%"=="4" notepad "%LogFile%"
goto ErrorOptions

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

:End
REM Success! Change back to root working directory.
cd "%WorkingDir%"

REM _01234567890123456789012345678901234567890123456789012345678901234567890123456789___________________________________________________________________________________

:Exit
REM Possibly no success, so stay at current location for debug purposes.
endlocal
