@echo off

echo Current working directory is: %CD%
echo.

if not exist "bin\netstandard\Release\" echo The path "bin\netstandard\Release\" does not exist from the current working directory.&&goto :eof

cd bin\netstandard\Release\
if not %errorlevel%==0 echo Failed to change to path "bin\netstandard\Release\".&&goto :eof

:startover
echo.
echo This command file will attempt to push a new version of V8.Net.
if not "%key%"=="" echo Current key: %key% (press enter to use this one again)
set /P key=Please enter the Nuget API key (enter 'exit' to quit):
if "%key%"=="exit" goto :eof
echo.
echo These are the packages in the release folder ...
echo.
dir *.nupkg /B
echo.
set /P version=Please enter the verion of the one to publish (example: 1.0.0):
echo.

if "%key%"=="" echo You did not enter a key.&&goto :eof

set filename=V8.Net.%version%.nupkg

if not exist "%filename%" echo There is no package by the filename '%filename%'!&&pause&&goto startover

nuget push %filename% oy2dgb333j35kjjybt4a4yzxo7hjyloera4anxn4ivcvle -Source https://api.nuget.org/v3/index.json
echo.
echo Done.
pause