@echo off
echo ========================================
echo Employee Management System Launcher
echo ========================================
echo.

REM Get the directory where this batch file is located
set "DEPLOY_DIR=%~dp0"
echo Deployment directory: %DEPLOY_DIR%

REM Set environment variables for relative paths
set "DATA_DIRECTORY=%DEPLOY_DIR%SharedData"
set "REPORTS_DIRECTORY=%DEPLOY_DIR%SharedData\Reports"
set "IMAGES_DIRECTORY=%DEPLOY_DIR%SharedData\Images"
set "LOGS_DIRECTORY=%DEPLOY_DIR%SharedData\Logs"

echo Data directory: %DATA_DIRECTORY%

REM Verify SharedData directory exists
if not exist "%DATA_DIRECTORY%" (
    echo ERROR: SharedData directory not found!
    echo Please ensure the deployment package is extracted correctly.
    pause
    exit /b 1
)

REM Verify sample data exists
if not exist "%DATA_DIRECTORY%\sample_employees.csv" (
    echo WARNING: Sample employees CSV not found!
    echo The application may not load sample data correctly.
    echo.
)

echo.
echo Starting Management Application...
cd /d "%DEPLOY_DIR%ManagementApp"
start "" "ManagementApp.exe"

timeout /t 3 /nobreak >nul

echo Starting Display Application...
cd /d "%DEPLOY_DIR%DisplayApp"
start "" "DisplayApp.exe"

echo.
echo ========================================
echo Both applications have been launched!
echo ========================================
echo.
echo Management App: Employee management and scheduling
echo Display App: Real-time data visualization
echo.
echo You can close this window now.
pause
