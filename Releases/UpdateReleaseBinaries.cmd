@echo off

cd "%0\..\"
echo Current Folder: %CD%
echo This script will update the release binaries with the latest successful release build.
pause

set /P version=Enter version (press enter on empy line to abort):

if "%version%"=="" goto :eof

if not exist "%version%" md "%version%"
cd "%version%"

if not %errorlevel%==0 echo Failed to create a directory with that version as a name.&&goto :eof

xcopy /Y /D ..\..\Source\bin\Release\*.* ".NET 4.0\"
xcopy /Y /D ..\..\Source\bin\Debug\*.* ".NET 4.0 - Debug\"

echo Completed.
pause