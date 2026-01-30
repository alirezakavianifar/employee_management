@echo off
echo Starting Employee Management System - Management App...
cd /d "%~dp0ManagementApp"

REM Check if ManagementApp.exe exists
if not exist "ManagementApp.exe" (
    echo ERROR: ManagementApp.exe not found in %CD%
    echo Please make sure the application is built and deployed correctly.
    pause
    exit /b 1
)

REM Start the application
echo Launching ManagementApp.exe...
start "" ManagementApp.exe

REM Wait a moment for the app to start
timeout /t 2 /nobreak >nul

REM Check if the process is running
tasklist /FI "IMAGENAME eq ManagementApp.exe" 2>NUL | find /I /N "ManagementApp.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo Management App started successfully!
    echo The application window should be visible now.
) else (
    echo WARNING: Management App process not found after startup.
    echo The application may have crashed or failed to start.
    echo Please check the log file at: ManagementApp\Data\Logs\management_app.log
)

echo.
echo Press any key to close this window...
pause >nul

