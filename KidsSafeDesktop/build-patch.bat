@echo off
setlocal EnableExtensions

rem ==========================================================================
rem  Kids Safe Desktop - incremental patch builder
rem
rem  build-patch.bat windows -OldDir ..\_release\v1.20\win ^
rem      -NewDir bin\Release\net10.0-windows10.0.19041.0\win-x64\publish ^
rem      -Version 1.21 -VersionCode 22 -Base 1.20
rem
rem  All arguments are passed straight to Scripts\build-patch.ps1.
rem  Run "build-patch.bat -?" for the full list (see the .ps1 header).
rem
rem  This file is ASCII on purpose: cmd.exe decodes .bat with the console
rem  code page, so UTF-8 Chinese here would be mangled. Chinese output comes
rem  from the .ps1, which is saved as UTF-8 with BOM.
rem ==========================================================================

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

if not exist "%ROOT%\Scripts\build-patch.ps1" (
    echo [X] Scripts\build-patch.ps1 is missing. Do not copy this .bat alone.
    if not defined NO_PAUSE pause
    exit /b 1
)

for /f "tokens=2 delims=:" %%a in ('chcp') do set "_OLDCP=%%a"
set "_OLDCP=%_OLDCP: =%"

chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\Scripts\build-patch.ps1" %*
set "RC=%ERRORLEVEL%"

if defined _OLDCP chcp %_OLDCP% >nul

if not "%RC%"=="0" (
    echo [X] Patch build failed, exit code %RC%
    if not defined NO_PAUSE pause
)
exit /b %RC%
