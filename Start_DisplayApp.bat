@echo off
echo Starting Display App for testing...
echo.

REM Get the current directory
set "PROJECT_ROOT=%~dp0"

REM Start Display App
echo Starting Display App...
cd /d "%PROJECT_ROOT%DisplayApp"
dotnet run

pause
