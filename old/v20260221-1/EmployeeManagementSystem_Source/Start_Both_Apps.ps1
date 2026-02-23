# Employee Management System Launcher
Write-Host "Starting Employee Management System..." -ForegroundColor Green
Write-Host ""

# Get the current directory
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# Start Management App
Write-Host "Starting Management App..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$ProjectRoot\ManagementApp'; dotnet run"

# Wait a moment for the Management App to start
Start-Sleep -Seconds 3

# Start Display App
Write-Host "Starting Display App..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$ProjectRoot\DisplayApp'; dotnet run"

Write-Host ""
Write-Host "Both applications are starting..." -ForegroundColor Green
Write-Host "- Management App: For managing employees and groups" -ForegroundColor Cyan
Write-Host "- Display App: For displaying all groups side by side" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press any key to exit this launcher..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
