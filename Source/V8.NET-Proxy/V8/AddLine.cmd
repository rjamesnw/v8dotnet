@echo off

REM This routine converts {{ to <, }} to >, and {'} to " in the "search" and "newline" parameters.
REM Note: The "???Safe" variables are used for working with values that don't contain troublesome characters.

SET errorlevel=
SET added=false
SET file=%~1
SET newFile=%~2
SET search=%~3
SET newline=%~4
SET "newlineUnsafe=%newline:{{=<%
SET "newlineUnsafe=%newlineUnsafe:}}=>%
SET "newlineUnsafe=%newlineUnsafe:{'}=\"%
SET ""="

ECHO Current Directory: %CD%
ECHO Adding line "%newline%" after line "%search%" using file "%file%" and saving to "%newFile%" ...

IF "%file%"=="%newFile%" echo Error: New file and source file are the same! && goto :EOF

REM Already done?
find "%newLineUnsafe%" "%file%">nul
IF "%errorlevel%"=="0" SET added=exists&&GOTO finished

IF EXIST "%newFile%" del "%newFile%"

FOR /F "tokens=1* delims=]" %%A in ('type "%file%" ^| find /V /N ""') DO (
  SET "line=%%B"
  CALL :doline
)

goto :EOF

:doline
  setlocal ENABLEEXTENSIONS ENABLEDELAYEDEXPANSION

  SET "lineSafe=%line:"='%

  IF "%lineSafe%"=="" ECHO.>>"%newFile%" && GOTO :EOF

  SET "lineSafe=%lineSafe:<={{%
  SET "lineSafe=%lineSafe:>=}}%
  SET lineSafe=%lineSafe:"={'}%

  REM ECHO "%line%"
  REM ECHO SAFE?: %lineSafe%
  ECHO !lineSafe! | FIND "!search!" >nul
  REM ECHO Errorlevel: %errorlevel%

  IF NOT "%lineSafe%"=="" ECHO !line!>>"%newFile%"
  if "%errorlevel%" == "0" ECHO !newlineUnsafe!>>"%newFile%" && ECHO Found and added^^! ...
  endlocal
  if "%errorlevel%" == "0" SET added=true
goto :EOF
