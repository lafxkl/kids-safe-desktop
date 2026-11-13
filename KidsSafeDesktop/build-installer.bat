@echo off
setlocal EnableExtensions

rem ==========================================================================
rem  Kids Safe Desktop - Windows installer build entry
rem
rem  build-installer.bat            use the version in the csproj
rem  build-installer.bat 1.19       build a specific version
rem
rem  Produces  Website\downloads\KidsSafeDesktop-<version>-setup.exe
rem
rem  Difference between the two Windows packages:
rem    -win.zip        portable, just unzip and run, writes nothing to the
rem                    registry, therefore NO desktop right-click entry
rem    -setup.exe      installed per user, adds Start Menu + desktop
rem                    right-click entry, can be uninstalled
rem
rem  Requires Inno Setup 6 (ISCC.exe). Install it with:
rem    winget install --id JRSoftware.InnoSetup --source winget
rem
rem  This file is ASCII on purpose. cmd.exe decodes .bat files with the
rem  console code page (936/GBK on Chinese Windows), so any UTF-8 Chinese
rem  here would be mangled into broken commands. All Chinese output comes
rem  from Scripts\build-installer.ps1, which is saved as UTF-8 with BOM.
rem ==========================================================================

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

echo ==========================================
echo    Kids Safe Desktop - Installer Build
echo ==========================================
echo.

if not exist "%ROOT%\Scripts\build-installer.ps1" (
    echo [X] Scripts\build-installer.ps1 is missing. Do not copy this .bat alone.
    echo.
    if not defined NO_PAUSE pause
    exit /b 1
)

for /f "tokens=2 delims=:" %%a in ('chcp') do set "_OLDCP=%%a"
set "_OLDCP=%_OLDCP: =%"

chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%\Scripts\build-installer.ps1" %*
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
