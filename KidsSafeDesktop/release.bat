@echo off
setlocal EnableExtensions

rem ==========================================================================
rem  Kids Safe Desktop - release build entry
rem
rem  release.bat                  rebuild with the version in the csproj
rem  release.bat 1.17 18          bump to v1.17 / versionCode 18
rem  release.bat 1.17             version only, versionCode auto +1
rem  release.bat -SkipBuild       package existing output, no compile
rem  release.bat -Clean           clean bin / obj before building
rem  release.bat --help           show full usage
rem
rem  This file is ASCII on purpose. cmd.exe decodes .bat files with the
rem  console code page (936/GBK on Chinese Windows), so any UTF-8 Chinese
rem  here would be mangled into broken commands. All Chinese output comes
rem  from Scripts\release.ps1, which is saved as UTF-8 with BOM.
rem ==========================================================================

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

echo ==========================================
echo    Kids Safe Desktop - Release Build
echo ==========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [X] dotnet not found. Install the .NET 10 SDK and add it to PATH.
    echo.
    if not defined NO_PAUSE pause
    exit /b 1
)

if not exist "%ROOT%\Scripts\release.ps1" (
    echo [X] Scripts\release.ps1 is missing. Do not copy this .bat alone.
    echo.
    if not defined NO_PAUSE pause
    exit /b 1
)

rem Remember the current code page so the window is left as we found it
for /f "tokens=2 delims=:" %%a in ('chcp') do set "_OLDCP=%%a"
set "_OLDCP=%_OLDCP: =%"

chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\Scripts\release.ps1" %*
set "RC=%ERRORLEVEL%"

if defined _OLDCP chcp %_OLDCP% >nul

echo.
if not "%RC%"=="0" (
    echo [X] Build failed, exit code %RC%
) else (
    echo [OK] Done
)
echo.

if not defined NO_PAUSE pause
exit /b %RC%
