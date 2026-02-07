# Update EmployeeManagementSystem_Deploy folder with latest builds
# This script builds the solution and copies files to the deploy folder

Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build ManagementApp.sln --configuration Debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build successful. Updating deployment folder..." -ForegroundColor Green

$deployPath = "EmployeeManagementSystem_Deploy"
$managementAppSource = "ManagementApp\bin\Debug\net8.0-windows"
$displayAppSource = "DisplayApp\bin\Debug\net8.0-windows"

# Update ManagementApp
Write-Host "Updating ManagementApp..." -ForegroundColor Yellow
$managementAppDeploy = "$deployPath\ManagementApp"

# Create directory if it doesn't exist
if (-not (Test-Path $managementAppDeploy)) {
    New-Item -ItemType Directory -Path $managementAppDeploy -Force | Out-Null
}

# Copy all files except Config folder
Get-ChildItem -Path $managementAppSource -File | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination "$managementAppDeploy\$($_.Name)" -Force
    Write-Host "  Copied: $($_.Name)" -ForegroundColor Gray
}

# Ensure Config folder exists and is preserved
if (-not (Test-Path "$managementAppDeploy\Config")) {
    New-Item -ItemType Directory -Path "$managementAppDeploy\Config" -Force | Out-Null
}

# Copy DisplayApp.dll to ManagementApp folder (ManagementApp references DisplayApp)
Write-Host "Copying DisplayApp.dll to ManagementApp (required dependency)..." -ForegroundColor Yellow
if (Test-Path "$displayAppSource\DisplayApp.dll") {
    Copy-Item -Path "$displayAppSource\DisplayApp.dll" -Destination "$managementAppDeploy\DisplayApp.dll" -Force
    Write-Host "  Copied: DisplayApp.dll" -ForegroundColor Gray
}
if (Test-Path "$displayAppSource\DisplayApp.pdb") {
    Copy-Item -Path "$displayAppSource\DisplayApp.pdb" -Destination "$managementAppDeploy\DisplayApp.pdb" -Force
    Write-Host "  Copied: DisplayApp.pdb" -ForegroundColor Gray
}

# Remove DisplayApp.exe and DisplayApp.deps.json from ManagementApp folder (if they exist)
if (Test-Path "$managementAppDeploy\DisplayApp.exe") {
    Remove-Item -Path "$managementAppDeploy\DisplayApp.exe" -Force
    Write-Host "  Removed: DisplayApp.exe (should not be in ManagementApp folder)" -ForegroundColor Gray
}
if (Test-Path "$managementAppDeploy\DisplayApp.deps.json") {
    Remove-Item -Path "$managementAppDeploy\DisplayApp.deps.json" -Force
    Write-Host "  Removed: DisplayApp.deps.json (should not be in ManagementApp folder)" -ForegroundColor Gray
}
if (Test-Path "$managementAppDeploy\DisplayApp.runtimeconfig.json") {
    Remove-Item -Path "$managementAppDeploy\DisplayApp.runtimeconfig.json" -Force
    Write-Host "  Removed: DisplayApp.runtimeconfig.json (should not be in ManagementApp folder)" -ForegroundColor Gray
}

# Update DisplayApp
Write-Host "Updating DisplayApp..." -ForegroundColor Yellow
$displayAppDeploy = "$deployPath\DisplayApp"

# Create directory if it doesn't exist
if (-not (Test-Path $displayAppDeploy)) {
    New-Item -ItemType Directory -Path $displayAppDeploy -Force | Out-Null
}

# Copy all files except Config folder
Get-ChildItem -Path $displayAppSource -File | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination "$displayAppDeploy\$($_.Name)" -Force
    Write-Host "  Copied: $($_.Name)" -ForegroundColor Gray
}

# Ensure Config folder exists and is preserved
if (-not (Test-Path "$displayAppDeploy\Config")) {
    New-Item -ItemType Directory -Path "$displayAppDeploy\Config" -Force | Out-Null
}

# Sync SharedData from project root to deploy folder
Write-Host "Syncing SharedData..." -ForegroundColor Yellow
$sharedDataSource = "SharedData"
$sharedDataDeploy = "$deployPath\SharedData"
if (Test-Path $sharedDataSource) {
    if (-not (Test-Path $sharedDataDeploy)) {
        New-Item -ItemType Directory -Path $sharedDataDeploy -Force | Out-Null
    }
    Copy-Item -Path "$sharedDataSource\*" -Destination $sharedDataDeploy -Recurse -Force
    Write-Host "  SharedData synced from project" -ForegroundColor Gray
} else {
    Write-Host "  SharedData source not found, skipping" -ForegroundColor Yellow
}

Write-Host "`nDeployment update complete!" -ForegroundColor Green
Write-Host "Note: Config folders have been preserved. SharedData has been synced from project." -ForegroundColor Cyan
