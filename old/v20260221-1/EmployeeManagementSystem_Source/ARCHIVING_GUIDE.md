# Project Archiving Guide

This guide explains how to create a new versioned archive of the Employee Management System using the automated script or manual process.

## 🚀 Recommended Method: Automated Script

The project now includes a PowerShell script `Archive-Project.ps1` that automates the entire process, including publishing binaries and auto-correcting launcher paths.

### Basic Usage
1. Open PowerShell in the project root.
2. Run the script:
   ```powershell
   .\Archive-Project.ps1
   ```
   *This creates a folder in `old/vYYYYMMDD` with current date.*

### Custom Version
To specify a custom version name or number:
```powershell
.\Archive-Project.ps1 -Version "3.0.0"
```

### Dry Run
To see what would happen without creating any files:
```powershell
.\Archive-Project.ps1 -DryRun
```

---

## 🛠️ Manual Method (Reference)

If the script cannot be used, follow these manual steps to ensure the archive is complete and functional.

### 1. Create the version folder
Create a new directory in `old/` for the new version:
```powershell
New-Item -ItemType Directory -Path old\v20260221
```

### 2. Archiving Source Code
Create the `EmployeeManagementSystem_Source` folder and copy the project files, excluding build artifacts and version control history.

**PowerShell Command:**
```powershell
$Version = "v20260221" # Update this
New-Item -ItemType Directory -Path old\$Version\EmployeeManagementSystem_Source
robocopy . old\$Version\EmployeeManagementSystem_Source /E /XD .git old bin obj .vs /XF .gitignore AGENTS.md
```

### 3. Archiving Deployed Version
Create the `EmployeeManagementSystem_Deploy` folder and populate it with published binaries and launchers.

#### A. Publish Applications
```powershell
dotnet publish ManagementApp/ManagementApp.csproj -c Release -o old\$Version\EmployeeManagementSystem_Deploy\ManagementApp
dotnet publish DisplayApp/DisplayApp.csproj -c Release -o old\$Version\EmployeeManagementSystem_Deploy\DisplayApp
```

#### B. Copy Shared Data
```powershell
Copy-Item -Path SharedData -Destination old\$Version\EmployeeManagementSystem_Deploy\SharedData -Recurse
```

#### C. Copy and Configure Launchers
Copy the launcher scripts:
```powershell
Copy-Item -Path Start_Both_Apps.bat, Start_Both_Apps.ps1, Start_DisplayApp.bat, Start_ManagementApp.bat -Destination old\$Version\EmployeeManagementSystem_Deploy\
```

> [!IMPORTANT]
> **Edit the `.bat` files** in the `Deploy` folder to execute the `.exe` files directly instead of using `dotnet run`.
> - **Incorrect:** `dotnet run`
> - **Correct:** `ManagementApp.exe` (or `DisplayApp.exe`)

---

## Maintenance Tips
- **Clean Source**: Ensure the `Source` archive is free of `bin` and `obj` folders.
- **Pre-Archive Health Check**: Run `dotnet build` before archiving.
- **Verify Deployed App**: Test the `Start_Both_Apps.bat` inside the `Deploy` folder.
