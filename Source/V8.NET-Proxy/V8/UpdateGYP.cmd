@echo off

SET added=false
set filepath=%~1
set GYPFile=%filepath%\v8.gyp
set tempGYPFile=%filepath%\~v8.gyp
set "searchText='target_name': 'v8'"
set "newLineText=      'product_name': 'v8-' + '{{(v8_target_arch)',"

IF EXIST "%GYPFile%.old" SET added=exists
IF NOT EXIST "%GYPFile%.old" CALL AddLine "%GYPFile%" "%tempGYPFile%" "%searchText%" "%newLineText%"

REM On success 'added' will be set to 'true', if already updated it will be 'exists', otherwise it will be 'false'.
IF "%added%"=="true" ECHO "Success!" && rename "%GYPFile%" "v8.gyp.old" && rename "%tempGYPFile%" "v8.gyp"
IF "%added%"=="exists" ECHO "'%GYPFile%' is already updated."
IF "%added%"=="false" ECHO "Failed to update file '%GYPFile%'."

