@echo off
REM One-click build launcher for ASU. Passes all arguments to build.ps1.
REM Usage: build.bat [-Configuration Debug^|Release] [-Clean] [-Run]

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
set EXITCODE=%ERRORLEVEL%

echo.
if not "%EXITCODE%"=="0" echo Build FAILED with exit code %EXITCODE%.
pause
exit /b %EXITCODE%