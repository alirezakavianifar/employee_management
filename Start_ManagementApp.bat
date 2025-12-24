@echo off
echo Starting Management App...
echo.

REM Get the current directory
set "PROJECT_ROOT=%~dp0"

REM Start Management App
echo Starting Management App...
cd /d "%PROJECT_ROOT%ManagementApp"
dotnet run

pause
