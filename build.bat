@echo off
title EasyDNS Build & Launch Tool
color 0b
echo ========================================================
echo               EasyDNS Build & Runner
echo ========================================================
echo.

set MSBUILD="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"

if not exist %MSBUILD% (
    echo [ERROR] MSBuild was not found at %MSBUILD%
    pause
    exit /b 1
)

echo [*] Compiling EasyDNS (Standalone Single-File Executable)...
if exist "%~dp0bin\Release" rmdir /s /q "%~dp0bin\Release"
%MSBUILD% "%~dp0EasyDNS.csproj" /t:Rebuild /p:Configuration=Release /nologo /v:m

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Compilation failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [✓] Build Succeeded! Executable created at:
echo     %~dp0bin\Release\EasyDNS.exe
echo.

set /p RUN="Do you want to launch EasyDNS now? (Y/N): "
if /i "%RUN%"=="Y" (
    echo [*] Starting EasyDNS...
    start "" "%~dp0bin\Release\EasyDNS.exe"
)

exit /b 0
