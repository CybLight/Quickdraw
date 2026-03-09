@echo off
setlocal

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

call "%~dp0build-installer-wrapper.cmd" x86 %CONFIG%
exit /b %ERRORLEVEL%
