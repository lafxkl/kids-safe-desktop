@echo off
setlocal EnableExtensions

rem ==========================================================================
rem  Kids Safe Desktop - website deploy entry
rem
rem  deploy.bat -Server root@1.2.3.4                 upload site files
rem  deploy.bat -Server root@1.2.3.4 -WithDownloads  also upload apk / zip
rem  deploy.bat -DryRun                              print the plan only
rem  deploy.bat -Server ... -SkipIndexNow            no IndexNow ping
rem
rem  No backup is taken: files are overwritten in place on the server.
rem
rem  This file is ASCII on purpose. cmd.exe decodes .bat files with the
rem  console code page (936/GBK on Chinese Windows), so any UTF-8 Chinese
rem  here would be mangled into broken commands. All Chinese output comes
rem  from Scripts\deploy.ps1, which is saved as UTF-8 with BOM.
rem ==========================================================================

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

echo ==========================================
echo    Kids Safe Desktop - Website Deploy
echo ==========================================
echo.

if not exist "%ROOT%\Scripts\deploy.ps1" (
    echo [X] Scripts\deploy.ps1 is missing. Do not copy this .bat alone.
    echo.
    if not defined NO_PAUSE pause
    exit /b 1
)

where ssh >nul 2>nul
if errorlevel 1 (
    echo [X] OpenSSH client not found. Install it via Settings - Apps -
    echo     Optional features - OpenSSH Client.
    echo.
    if not defined NO_PAUSE pause
    exit /b 1
)

rem Remember the current code page so the window is left as we found it
for /f "tokens=2 delims=:" %%a in ('chcp') do set "_OLDCP=%%a"
set "_OLDCP=%_OLDCP: =%"

chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\Scripts\deploy.ps1" %*
set "RC=%ERRORLEVEL%"

if defined _OLDCP chcp %_OLDCP% >nul

echo.
if not "%RC%"=="0" (
    echo [X] Deploy failed, exit code %RC%
) else (
    echo [OK] Done
)
echo.

if not defined NO_PAUSE pause
exit /b %RC%
