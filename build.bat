@echo off
setlocal

REM ============================================================
REM  KioskWin -- build and package script (build.bat)
REM
REM  Usage:
REM    Double-click, or run in cmd:
REM      build.bat            restore, test, publish, zip
REM      build.bat notest     skip tests, publish and zip only
REM
REM  Output:
REM    .\publish\              self-contained deploy directory
REM    .\KioskWin-win-x64.zip  packaged archive
REM ============================================================

set "ROOT=%~dp0"
set "SLN=%ROOT%KioskWin.sln"
set "PROJ=%ROOT%src\KioskWin\KioskWin.csproj"
set "OUT=%ROOT%publish"
set "ZIP=%ROOT%KioskWin-win-x64.zip"
set "CONFIG=Release"
set "RID=win-x64"

if /i "%~1"=="notest" (set "RUNTESTS=0") else (set "RUNTESTS=1")

echo ============================================================
echo   KioskWin Build / Package
echo   Solution: %SLN%
echo   Output  : %OUT%
echo   Archive : %ZIP%
echo ============================================================

REM --- 0. Environment check ---
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet not found. Install .NET SDK:
    echo         https://dotnet.microsoft.com/download
    goto :fail
)

if not exist "%SLN%" (
    echo [ERROR] Solution file not found: %SLN%
    echo         Please scaffold the project per the implementation plan.
    goto :fail
)

REM --- 1. Restore ---
echo.
echo [1/4] Restoring NuGet packages...
dotnet restore "%SLN%"
if errorlevel 1 goto :fail

REM --- 2. Unit tests ---
if "%RUNTESTS%"=="0" goto :skip_tests
echo.
echo [2/4] Running unit tests (Core)...
dotnet test "%SLN%" -c %CONFIG% --no-restore --nologo
if errorlevel 1 goto :fail
goto :after_tests

:skip_tests
echo.
echo [2/4] Tests skipped (notest)
:after_tests

REM --- 3. Publish self-contained single-file ---
echo.
echo [3/4] Publishing self-contained single-file (rid=%RID%)...
if exist "%OUT%" rmdir /s /q "%OUT%"
dotnet publish "%PROJ%" ^
  -c %CONFIG% ^
  -r %RID% ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o "%OUT%"
if errorlevel 1 goto :fail

REM --- 4. Package as zip ---
echo.
echo [4/4] Packaging as zip...
powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%' -Force"
if not errorlevel 1 goto :zip_ok
echo [WARNING] Zip packaging failed, but publish dir is ready: %OUT%
goto :after_zip

:zip_ok
echo Archive created: %ZIP%
:after_zip

REM --- Done ---
echo.
echo ============================================================
echo   Build complete.
echo   Deploy dir : %OUT%
echo   Archive    : %ZIP%
echo.
echo   Deploy steps:
echo     1) Copy publish dir to target machine
echo     2) Edit appsettings.json to set Url
echo     3) Set AdminPassword in appsettings.json
echo     4) Run install-shortcut.ps1 for startup shortcut
echo     5) Run uninstall-shortcut.ps1 to remove startup shortcut
echo ============================================================
echo.
pause
exit /b 0

:fail
echo.
echo ============================================================
echo   [FAILED] Build/package failed. See output above.
echo ============================================================
pause
exit /b 1
