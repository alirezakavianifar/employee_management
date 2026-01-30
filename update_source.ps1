# Update EmployeeManagementSystem_Source folder with latest source code
# This script copies source files from main project directories to the source folder

Write-Host "Updating EmployeeManagementSystem_Source folder..." -ForegroundColor Cyan

$sourcePath = "EmployeeManagementSystem_Source"
$managementAppSource = "ManagementApp"
$displayAppSource = "DisplayApp"
$sharedSource = "Shared"

# Function to copy directory excluding build artifacts
function Copy-SourceDirectory {
    param(
        [string]$SourceDir,
        [string]$DestDir,
        [string]$ProjectName
    )
    
    Write-Host "Updating $ProjectName..." -ForegroundColor Yellow
    
    # Create destination directory if it doesn't exist
    if (-not (Test-Path $DestDir)) {
        New-Item -ItemType Directory -Path $DestDir -Force | Out-Null
    }
    
    # Copy all files and folders, excluding build artifacts
    Get-ChildItem -Path $SourceDir -Recurse | Where-Object {
        $relativePath = $_.FullName.Substring($SourceDir.Length + 1)
        # Exclude bin, obj, and other build artifacts, and the source folder itself
        -not ($relativePath -like "bin\*" -or 
              $relativePath -like "obj\*" -or
              $relativePath -like ".vs\*" -or
              $relativePath -like ".git\*" -or
              $relativePath -like "EmployeeManagementSystem_Source\*" -or
              $relativePath -like "EmployeeManagementSystem_Deploy\*" -or
              $relativePath -like "*.user" -or
              $relativePath -like "*.suo" -or
              $relativePath -like "*.cache")
    } | ForEach-Object {
        $destPath = $_.FullName.Replace($SourceDir, $DestDir)
        $destParent = Split-Path -Path $destPath -Parent
        
        if (-not (Test-Path $destParent)) {
            New-Item -ItemType Directory -Path $destParent -Force | Out-Null
        }
        
        if (-not $_.PSIsContainer) {
            Copy-Item -Path $_.FullName -Destination $destPath -Force
        }
    }
    
    Write-Host "  $ProjectName updated successfully" -ForegroundColor Green
}

# Update ManagementApp
Copy-SourceDirectory -SourceDir $managementAppSource -DestDir "$sourcePath\ManagementApp" -ProjectName "ManagementApp"

# Update DisplayApp
Copy-SourceDirectory -SourceDir $displayAppSource -DestDir "$sourcePath\DisplayApp" -ProjectName "DisplayApp"

# Update Shared
Copy-SourceDirectory -SourceDir $sharedSource -DestDir "$sourcePath\Shared" -ProjectName "Shared"

# Copy solution file
if (Test-Path "ManagementApp.sln") {
    Copy-Item -Path "ManagementApp.sln" -Destination "$sourcePath\ManagementApp.sln" -Force
    Write-Host "  Solution file updated" -ForegroundColor Green
}

# Update SOURCE_CODE_SUMMARY.txt with current date
$summaryFile = "$sourcePath\SOURCE_CODE_SUMMARY.txt"
if (Test-Path $summaryFile) {
    $content = Get-Content $summaryFile -Raw
    $currentDate = Get-Date -Format "MMMM dd, yyyy"
    $content = $content -replace "Updated: .* \(Latest sync\)", "Updated: $currentDate (Latest sync)"
    Set-Content -Path $summaryFile -Value $content -NoNewline
    Write-Host "  Summary file updated with current date" -ForegroundColor Green
}

Write-Host "`nSource code update complete!" -ForegroundColor Green
Write-Host "All source files have been synchronized to EmployeeManagementSystem_Source folder." -ForegroundColor Cyan
