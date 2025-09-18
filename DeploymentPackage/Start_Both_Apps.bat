@echo off
echo Starting Employee Shift Management System...
echo.
echo Starting Management App...
cd /d "%~dp0ManagementApp"
start "" "ManagementApp.exe"
echo.
echo Starting Display App...
cd /d "%~dp0DisplayApp"
start "" "DisplayApp.exe"
echo.
echo Both applications started successfully!
echo Management App: Employee management and scheduling
echo Display App: Real-time data visualization
echo.
pause
