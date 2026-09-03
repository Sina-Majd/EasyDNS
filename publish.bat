@echo off
title EasyDNS Standalone Release Publisher
color 0b
echo ========================================================
echo          EasyDNS Standalone Release Publisher
echo ========================================================
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] dotnet CLI is required for single-file standalone publishing.
    pause
    exit /b 1
)

if not exist "%~dp0bin" mkdir "%~dp0bin"
if exist "%~dp0bin\Publish" rmdir /s /q "%~dp0bin\Publish"

echo [*] Publishing Standalone Single-File Executable (.NET 8 Win-x64)...
dotnet publish "%~dp0EasyDNS.csproj" -c Release -f net8.0-windows -r win-x64 -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -p:IncludeNativeLibrariesForSelfExtract=true --self-contained false -o "%~dp0bin\Publish" --nologo

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Publishing failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [✓] Standalone executable successfully published to:
echo     %~dp0bin\Publish\EasyDNS.exe
echo.

set /p RUN="Do you want to test run the published executable? (Y/N): "
if /i "%RUN%"=="Y" (
    echo [*] Starting published EasyDNS.exe...
    start "" "%~dp0bin\Publish\EasyDNS.exe"
)

exit /b 0
