@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%scripts\Build-WindowsApp.ps1"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
echo.
pause
exit /b %BUILD_EXIT_CODE%
