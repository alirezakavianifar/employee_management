@echo off
echo Starting Employee Management System...
echo.

REM Get the current directory
set "PROJECT_ROOT=%~dp0"

REM Start Management App in a new window
echo Starting Management App...
start "Management App" cmd /k "cd /d "%PROJECT_ROOT%ManagementApp" && dotnet run"

REM Wait a moment for the Management App to start
timeout /t 3 /nobreak >nul

REM Start Display App in a new window
echo Starting Display App...
start "Display App" cmd /k "cd /d "%PROJECT_ROOT%DisplayApp" && dotnet run"

echo.
echo Both applications are starting...
echo - Management App: For managing employees and groups
echo - Display App: For displaying all groups side by side
echo.
echo Press any key to exit this launcher...
pause >nul
