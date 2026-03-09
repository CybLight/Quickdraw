@echo off
setlocal

set "PROFILE=%~1"
if "%PROFILE%"=="" set "PROFILE=all"
set "CONFIG=%~2"
if "%CONFIG%"=="" set "CONFIG=Release"

powershell -ExecutionPolicy Bypass -File "%~dp0build-installer-wrapper.ps1" -Profile %PROFILE% -Configuration %CONFIG%
exit /b %ERRORLEVEL%
