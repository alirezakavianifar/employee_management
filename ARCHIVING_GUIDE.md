# Project Archiving Guide

This guide explains how to create a new versioned archive of the Employee Management System, similar to the structure in `old/v20260221`.

## Archive Structure
Each version archive should be placed in the `old/` directory with a folder name like `vYYYYMMDD` or a version number (e.g., `v3.0.0`).

Inside each version folder:
- **EmployeeManagementSystem_Source/**: Clean copy of the source code.
- **EmployeeManagementSystem_Deploy/**: Published binaries ready for execution.

---

## 1. Create the version folder
Create a new directory in `old/` for the new version:
```powershell
New-Item -ItemType Directory -Path old\v20260221
```

## 2. Archiving Source Code
Create the `EmployeeManagementSystem_Source` folder and copy the project files, excluding build artifacts and version control history.

**Files to include:**
- `ManagementApp/`, `DisplayApp/`, `Shared/`, `SharedData/`, `SharedTests/`
- `ManagementApp.sln`, `.gitignore`, `AGENTS.md`
- All `.bat` and `.ps1` launcher scripts in the root.

**Directories to exclude:**
- `.git/`, `old/`, `bin/`, `obj/`

**PowerShell Command (Recommended):**
```powershell
New-Item -ItemType Directory -Path old\v20260221\EmployeeManagementSystem_Source
robocopy . old\v20260221\EmployeeManagementSystem_Source /E /XD .git old bin obj /XF .gitignore AGENTS.md
```

## 3. Archiving Deployed Version
Create the `EmployeeManagementSystem_Deploy` folder and populate it with published binaries and launchers.

### A. Publish Applications
Run the following commands to generate optimized binaries for the `v20260221` archive:
```powershell
dotnet publish ManagementApp/ManagementApp.csproj -c Release -o old\v20260221\EmployeeManagementSystem_Deploy\ManagementApp
dotnet publish DisplayApp/DisplayApp.csproj -c Release -o old\v20260221\EmployeeManagementSystem_Deploy\DisplayApp
```

### B. Copy Shared Data
The applications require the `SharedData` directory to be present in the root of the deployment folder:
```powershell
Copy-Item -Path SharedData -Destination old\v20260221\EmployeeManagementSystem_Deploy\SharedData -Recurse
```

### C. Copy and Configure Launchers
Copy the launcher scripts from the project root to the deployment root:
```powershell
Copy-Item -Path Start_Both_Apps.bat, Start_Both_Apps.ps1, Start_DisplayApp.bat, Start_ManagementApp.bat -Destination old\v20260221\EmployeeManagementSystem_Deploy\
```

**CRITICAL:** You MUST edit the `.bat` files in the `EmployeeManagementSystem_Deploy` folder to execute the `.exe` files directly instead of using `dotnet run`.
- **Incorrect:** `start "App" cmd /k "cd /d ... && dotnet run"`
- **Correct:** `start "App" cmd /k "cd /d ... && ManagementApp.exe"`

---

## Maintenance Tips
- **Keep it Clean**: Always ensure the `Source` archive is free of `bin` and `obj` folders to save space.
- **Verify before Archiving**: Run `dotnet build` on the root solution to ensure the current state is healthy before creating the archive.
- **Test the Archive**: After creation, try running `Start_Both_Apps.bat` from within the `Deploy` folder to ensure the binaries work as expected.
