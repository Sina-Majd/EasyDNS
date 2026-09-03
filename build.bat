@echo off
title EasyDNS Build & Launch Tool
color 0b
echo ========================================================
echo               EasyDNS Modern Build & Runner
echo ========================================================
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo [*] Detected modern .NET SDK. Compiling with dotnet CLI...
    dotnet build "%~dp0EasyDNS.csproj" -c Release --nologo
    if %ERRORLEVEL% EQU 0 (
        set EXE_PATH="%~dp0bin\Release\net8.0-windows\EasyDNS.exe"
        goto :success
    )
    echo [WARNING] dotnet build returned an error, attempting MSBuild fallback...
)

set MSBUILD="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist %MSBUILD% (
    echo [ERROR] Neither dotnet SDK nor MSBuild was found on this system.
    pause
    exit /b 1
)

echo [*] Compiling via legacy MSBuild...
%MSBUILD% "%~dp0EasyDNS.csproj" /t:Build /p:Configuration=Release /nologo /v:m

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Compilation failed!
    pause
    exit /b %ERRORLEVEL%
)
set EXE_PATH="%~dp0bin\Release\net48\EasyDNS.exe"

:success
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
