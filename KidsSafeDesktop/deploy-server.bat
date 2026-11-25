@echo off
setlocal EnableExtensions

rem ==========================================================================
rem  Deploy KidsSafeServer (parent/update server) to the remote host.
rem  All arguments are passed to Scripts\deploy-server.ps1.
rem
rem    deploy-server.bat                     publish + upload + restart
rem    deploy-server.bat -SkipPublish        only re-upload the last build
rem
rem  ASCII only: cmd.exe decodes .bat with the console code page.
rem ==========================================================================

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

if not exist "%ROOT%\Scripts\deploy-server.ps1" (
    echo [X] Scripts\deploy-server.ps1 is missing.
    if not defined NO_PAUSE pause
    exit /b 1
)

for /f "tokens=2 delims=:" %%a in ('chcp') do set "_OLDCP=%%a"
set "_OLDCP=%_OLDCP: =%"

chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\Scripts\deploy-server.ps1" %*
set "RC=%ERRORLEVEL%"

if defined _OLDCP chcp %_OLDCP% >nul

if not "%RC%"=="0" (
    echo [X] Deploy failed, exit code %RC%
    if not defined NO_PAUSE pause
)
exit /b %RC%
