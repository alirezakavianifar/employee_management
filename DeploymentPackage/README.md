# Employee Shift Management System - Deployment Package

This package contains the complete Employee Shift Management System with both Management and Display applications.

## ⚠️ IMPORTANT: Path Issue Fix

**If employees are not showing up in the applications, this is due to hardcoded paths in the application code.**

### Quick Fix:
1. **Use the fixed launcher:** Run `Launch_With_Fixed_Paths.bat` instead of the individual .exe files
2. **Or use PowerShell:** Run `Fix_Paths_And_Launch.ps1` (requires PowerShell execution policy)

### Why This Happens:
The applications contain hardcoded paths to the development machine (`D:\projects\New folder (8)\SharedData`). When moved to another computer, these paths don't exist, so the applications can't find the employee data.

## System Requirements

- Windows 10 or later
- .NET 8.0 Runtime (if not installed, download from: https://dotnet.microsoft.com/download/dotnet/8.0)

## Package Contents

### ManagementApp/
Contains the main management application for:
- Employee management
- Shift scheduling
- Task assignment
- Report generation
- Data export/import

**Main executable:** `ManagementApp.exe`

### DisplayApp/
Contains the display application for:
- Real-time data visualization
- Charts and graphs
- Dashboard display
- Kiosk mode operation

**Main executable:** `DisplayApp.exe`

### SharedData/
Contains shared data files:
- Employee images
- Reports (JSON format)
- Logs
- Sample data files

## Installation Instructions

1. **Extract this package** to your desired location (e.g., `C:\EmployeeManagement\`)
2. **Install .NET 8.0 Runtime** if not already installed
3. **IMPORTANT:** Use the fixed launcher to avoid path issues:
   - Run `Launch_With_Fixed_Paths.bat` (recommended)
   - Or run `Fix_Paths_And_Launch.ps1` (PowerShell version)

## Launching Applications

### ✅ Recommended Method (Fixed Paths):
```batch
Launch_With_Fixed_Paths.bat
```
This will:
- Set correct environment variables
- Verify data directories exist
- Launch both applications with proper paths

### ❌ Direct Launch (May Not Work):
- `Start_ManagementApp.bat`
- `Start_DisplayApp.bat`
- Direct .exe execution

## Configuration

### ManagementApp Configuration
- Configuration file: `ManagementApp/Config/app_config.json`
- Data paths are set to use the SharedData folder
- Sync interval: 30 seconds

### DisplayApp Configuration
- Configuration file: `DisplayApp/Config/display_config.json`
- Data path points to the SharedData folder
- Supports Persian/Arabic language and RTL layout

## Usage

### ManagementApp
1. Launch using the fixed launcher
2. Use the interface to:
   - Add/edit employees
   - Create shift schedules
   - Assign tasks
   - Generate reports
   - Export data

### DisplayApp
1. Launch using the fixed launcher
2. The application will:
   - Display real-time data
   - Show charts and visualizations
   - Update automatically every 30 seconds
   - Support fullscreen mode

## Data Synchronization

Both applications share data through the `SharedData` folder:
- Reports are automatically synchronized
- Employee images are shared
- Logs are maintained separately for each application

## Troubleshooting

### 1. **Employees not showing up:**
- **Solution:** Use `Launch_With_Fixed_Paths.bat` instead of direct .exe execution
- **Cause:** Hardcoded paths in application code

### 2. **Application won't start:**
- Ensure .NET 8.0 Runtime is installed
- Check that all files are extracted properly
- Run as administrator if needed

### 3. **Data not syncing:**
- Check that SharedData folder is accessible
- Verify configuration files have correct paths
- Check logs in respective Logs folders

### 4. **Display issues:**
- Ensure display supports the required resolution
- Check display configuration in `display_config.json`

### 5. **PowerShell execution policy error:**
- Run PowerShell as administrator
- Execute: `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`
- Or use the batch file version instead

## File Structure
```
DeploymentPackage/
├── ManagementApp/           # Management application files
├── DisplayApp/             # Display application files
├── SharedData/             # Shared data (employees, reports, images)
├── Launch_With_Fixed_Paths.bat    # ✅ RECOMMENDED launcher
├── Fix_Paths_And_Launch.ps1       # PowerShell launcher
├── Start_ManagementApp.bat        # ❌ May not work on other PCs
├── Start_DisplayApp.bat           # ❌ May not work on other PCs
└── README.md               # This file
```

## Support

For technical support or questions, contact the development team.

## Version Information

- ManagementApp: Version 1.0.0.0
- DisplayApp: Version 1.0.0.0
- Target Framework: .NET 8.0
- Build Date: 2025-01-18
