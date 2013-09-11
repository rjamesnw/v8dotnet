@echo off

cd "%0\..\"
echo Current Folder: %CD%
echo This script will update the release binaries with the latest successful release build.
pause

xcopy /Y /D ..\Source\bin\Release\*.* ".NET 4.0\"
xcopy /Y /D ..\Source\bin\Release\x64\*.* ".NET 4.0\x64\"
xcopy /Y /D ..\Source\bin\Release\x86\*.* ".NET 4.0\x86\"

xcopy /Y /D ..\Source\bin\3.5\Release\*.* ".NET 3.5\"
xcopy /Y /D ..\Source\bin\3.5\Release\x64\*.* ".NET 3.5\x64\"
xcopy /Y /D ..\Source\bin\3.5\Release\x86\*.* ".NET 3.5\x86\"

echo Completed.
pause