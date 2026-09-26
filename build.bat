@echo off
title EasyDNS Modern Build & Runner
color 0b
echo ========================================================
echo               EasyDNS Modern Build & Runner
echo ========================================================
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Modern .NET SDK was not found on this system.
    echo Please install the .NET 8.0 SDK or newer from https://dot.net
    pause
    exit /b 1
)

echo [*] Compiling EasyDNS (.NET 8 Windows WPF)...
dotnet build "%~dp0EasyDNS.csproj" -c Release --nologo
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Compilation failed!
    pause
    exit /b %ERRORLEVEL%
)

set EXE_PATH="%~dp0bin\Release\net8.0-windows\EasyDNS.exe"

echo.
echo [✓] Build Succeeded! Executable ready at:
echo     %EXE_PATH%
echo.

set /p RUN="Do you want to launch EasyDNS now? (Y/N): "
if /i "%RUN%"=="Y" (
    echo [*] Starting EasyDNS...
    start "" %EXE_PATH%
)

exit /b 0
