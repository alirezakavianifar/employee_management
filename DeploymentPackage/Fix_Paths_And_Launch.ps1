# PowerShell script to fix path issues and launch applications
param(
    [string]$App = "both"  # "management", "display", or "both"
)

# Get the script directory (where the deployment package is extracted)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Deployment package directory: $ScriptDir"

# Set environment variables for relative paths
$env:DATA_DIRECTORY = Join-Path $ScriptDir "SharedData"
$env:REPORTS_DIRECTORY = Join-Path $ScriptDir "SharedData\Reports"
$env:IMAGES_DIRECTORY = Join-Path $ScriptDir "SharedData\Images"
$env:LOGS_DIRECTORY = Join-Path $ScriptDir "SharedData\Logs"

Write-Host "Setting data directory to: $env:DATA_DIRECTORY"

# Verify SharedData directory exists
if (-not (Test-Path $env:DATA_DIRECTORY)) {
    Write-Error "SharedData directory not found at: $env:DATA_DIRECTORY"
    Write-Host "Please ensure the deployment package is extracted correctly."
    Read-Host "Press Enter to exit"
    exit 1
}

# Verify sample data exists
$SampleDataPath = Join-Path $env:DATA_DIRECTORY "sample_employees.csv"
if (-not (Test-Path $SampleDataPath)) {
    Write-Warning "Sample employees CSV not found at: $SampleDataPath"
    Write-Host "The application may not load sample data correctly."
}

# Function to launch Management App
function Launch-ManagementApp {
    Write-Host "Starting Management Application..."
    $ManagementExe = Join-Path $ScriptDir "ManagementApp\ManagementApp.exe"
    
    if (Test-Path $ManagementExe) {
        Set-Location (Join-Path $ScriptDir "ManagementApp")
        Start-Process -FilePath $ManagementExe -WorkingDirectory (Join-Path $ScriptDir "ManagementApp")
        Write-Host "Management App launched successfully!"
    } else {
        Write-Error "ManagementApp.exe not found at: $ManagementExe"
    }
}

# Function to launch Display App
function Launch-DisplayApp {
    Write-Host "Starting Display Application..."
    $DisplayExe = Join-Path $ScriptDir "DisplayApp\DisplayApp.exe"
    
    if (Test-Path $DisplayExe) {
        Set-Location (Join-Path $ScriptDir "DisplayApp")
        Start-Process -FilePath $DisplayExe -WorkingDirectory (Join-Path $ScriptDir "DisplayApp")
        Write-Host "Display App launched successfully!"
    } else {
        Write-Error "DisplayApp.exe not found at: $DisplayExe"
    }
}

# Launch applications based on parameter
switch ($App.ToLower()) {
    "management" {
        Launch-ManagementApp
    }
    "display" {
        Launch-DisplayApp
    }
    "both" {
        Launch-ManagementApp
        Start-Sleep -Seconds 2
        Launch-DisplayApp
        Write-Host "Both applications launched!"
    }
    default {
        Write-Host "Usage: .\Fix_Paths_And_Launch.ps1 [-App management|display|both]"
        Write-Host "Default: both applications"
        Launch-ManagementApp
        Start-Sleep -Seconds 2
        Launch-DisplayApp
    }
}

Write-Host "`nApplications launched. You can close this window."
Read-Host "Press Enter to exit"
