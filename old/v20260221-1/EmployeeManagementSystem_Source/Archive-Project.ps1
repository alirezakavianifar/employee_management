<#
.SYNOPSIS
    Automates the archiving of the Employee Management System.
    
.DESCRIPTION
    This script performs the following:
    1. Creates a versioned folder in 'old/'.
    2. Copies source code (excluding build artifacts).
    3. Publishes both ManagementApp and DisplayApp.
    4. Copies SharedData.
    5. Copies and auto-corrects launcher .bat files for independent execution.
#>

param (
    [string]$Version = (Get-Date -Format "yyyyMMdd"),
    [switch]$DryRun
)

$BaseVersion = $Version
$ArchiveDir = Join-Path "old" "v$BaseVersion"
$Suffix = 1

while (Test-Path $ArchiveDir) {
    $ArchiveDir = Join-Path "old" "v$BaseVersion-$Suffix"
    $Suffix++
}

$SourceDir = Join-Path $ArchiveDir "EmployeeManagementSystem_Source"
$DeployDir = Join-Path $ArchiveDir "EmployeeManagementSystem_Deploy"

Write-Host "--- Archiving Project to $ArchiveDir ---" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "[DRY RUN] Would create directory: $ArchiveDir"
} else {
    New-Item -ItemType Directory -Path $ArchiveDir | Out-Null
}

# 1. Archive Source Code
Write-Host "Copying Source Code..." -ForegroundColor Yellow
$Excludes = @(".git", "old", "bin", "obj", ".vs", ".vscode")
if ($DryRun) {
    Write-Host "[DRY RUN] Would run: robocopy . $SourceDir /E /XD $($Excludes -join ' ') /XF .gitignore AGENTS.md"
}
else {
    $robocopyArgs = @(".", $SourceDir, "/E", "/XD") + $Excludes + @("/XF", ".gitignore", "AGENTS.md")
    & robocopy @robocopyArgs | Out-Null
}

# 2. Publish Applications (Deploy)
Write-Host "Publishing Applications..." -ForegroundColor Yellow
$Apps = @(
    @{Project = "ManagementApp/ManagementApp.csproj"; Output = "ManagementApp" },
    @{Project = "DisplayApp/DisplayApp.csproj"; Output = "DisplayApp" }
)

foreach ($App in $Apps) {
    if ($DryRun) {
        $OutPath = Join-Path $DeployDir $App.Output
        Write-Host "[DRY RUN] Would publish $($App.Project) to $OutPath"
    } else {
        $OutPath = Join-Path $DeployDir $App.Output
        Write-Host "Publishing $($App.Project)..." -ForegroundColor Gray
        dotnet publish $($App.Project) -c Release -o $OutPath --self-contained false
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish $($App.Project). Aborting."
            return
        }
    }
}

# 3. Copy Shared Data
Write-Host "Copying SharedData..." -ForegroundColor Yellow
if ($DryRun) {
    Write-Host "[DRY RUN] Would copy SharedData to $(Join-Path $DeployDir 'SharedData')"
}
else {
    Copy-Item -Path "SharedData" -Destination (Join-Path $DeployDir "SharedData") -Recurse
}

# 4. Copy and Auto-Correct Launchers
Write-Host "Configuring Launchers..." -ForegroundColor Yellow
$Launchers = @("Start_Both_Apps.bat", "Start_DisplayApp.bat", "Start_ManagementApp.bat", "Start_Both_Apps.ps1")

foreach ($Launcher in $Launchers) {
    if (Test-Path $Launcher) {
        $DestFile = Join-Path $DeployDir $Launcher
        if ($DryRun) {
            Write-Host "[DRY RUN] Would copy and patch $Launcher"
        }
        else {
            if ($Launcher.EndsWith(".bat")) {
                $Content = Get-Content $Launcher
                # Patch: replace "dotnet run" with executable execution
                # We assume the directory structure in Deploy matches the original source but contains the published outputs
                $PatchedContent = $Content -replace 'dotnet run', 'ManagementApp.exe' -replace 'ManagementApp.exe.*?DisplayApp', 'DisplayApp.exe'
                # Specific logic for simpler launchers
                if ($Launcher -match "ManagementApp") {
                    $PatchedContent = $Content -replace 'dotnet run', 'ManagementApp.exe'
                }
                elseif ($Launcher -match "DisplayApp") {
                    $PatchedContent = $Content -replace 'dotnet run', 'DisplayApp.exe'
                }
                else {
                    # Start_Both_Apps.bat needs careful replacement
                    $PatchedContent = $Content `
                        -replace 'cd /d "%PROJECT_ROOT%ManagementApp" && dotnet run', 'cd /d "%PROJECT_ROOT%ManagementApp" && ManagementApp.exe' `
                        -replace 'cd /d "%PROJECT_ROOT%DisplayApp" && dotnet run', 'cd /d "%PROJECT_ROOT%DisplayApp" && DisplayApp.exe'
                }
                $PatchedContent | Set-Content $DestFile
            }
            else {
                Copy-Item -Path $Launcher -Destination $DestFile
            }
        }
    }
}

Write-Host "--- Archiving Complete! ---" -ForegroundColor Green
Write-Host "Location: $ArchiveDir"
