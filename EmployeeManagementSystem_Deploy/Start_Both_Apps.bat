@echo off
echo Starting Employee Management System - Both Applications...
echo.
echo Starting Management App...
cd /d "%~dp0ManagementApp"
start ManagementApp.exe
echo.
echo Starting Display App...
cd /d "%~dp0DisplayApp"
start DisplayApp.exe
echo.
echo Both applications started successfully!
echo You can now close this window.
pause

