# Employee Management System - Testing Scripts

This directory contains scripts to help you test the updated Display App that now shows all groups side by side.

## Available Scripts

### 1. `Start_Both_Apps.bat` (Recommended)
- **Purpose**: Starts both Management App and Display App simultaneously
- **Usage**: Double-click the file or run from command prompt
- **What it does**:
  - Opens Management App in one window
  - Waits 3 seconds
  - Opens Display App in another window
  - Both apps run independently

### 2. `Start_Both_Apps.ps1` (PowerShell Version)
- **Purpose**: Same as above but using PowerShell
- **Usage**: Right-click → "Run with PowerShell" or run from PowerShell terminal
- **Benefits**: Better error handling and colored output

### 3. `Start_DisplayApp.bat`
- **Purpose**: Starts only the Display App for testing
- **Usage**: Double-click to test the display changes
- **When to use**: When you only want to test the display functionality

### 4. `Start_ManagementApp.bat`
- **Purpose**: Starts only the Management App
- **Usage**: Double-click to manage employees and groups
- **When to use**: When you only want to manage data

## Testing the New Multi-Group Display

### Step 1: Start Both Apps
1. Double-click `Start_Both_Apps.bat`
2. Wait for both applications to open

### Step 2: Create Multiple Groups (in Management App)
1. In the Management App, go to the Groups section
2. Create a new group (e.g., "Group 2")
3. Assign some employees to different shifts in different groups
4. Save the changes

### Step 3: Verify Display (in Display App)
1. Check the Display App - it should now show:
   - Multiple groups side by side
   - Each group in its own card
   - Group names at the top of each card
   - Morning and evening shifts for each group
   - Employee cards with photos and names

### Expected Behavior
- **Before**: Only one group was displayed
- **After**: All groups are displayed side by side in a horizontal scrollable layout
- **Header**: Shows "گروه‌های فعال: X گروه: [Group Names]"
- **Layout**: Groups scroll horizontally if there are many groups

## Troubleshooting

### If Display App doesn't start:
1. Make sure you're in the project root directory
2. Run `dotnet build` in the DisplayApp folder first
3. Check that all dependencies are installed

### If no groups are shown:
1. Make sure you have created groups in the Management App
2. Check that employees are assigned to shifts in those groups
3. Verify that the Management App has saved the data

### If groups don't appear side by side:
1. Check the Display App window size - make it wider
2. Look for horizontal scrollbars in the groups section
3. Verify the latest report data is being loaded

## File Structure
```
employee_management_csharp/
├── Start_Both_Apps.bat          # Main launcher (recommended)
├── Start_Both_Apps.ps1          # PowerShell version
├── Start_DisplayApp.bat         # Display App only
├── Start_ManagementApp.bat      # Management App only
├── ManagementApp/               # Management application
├── DisplayApp/                  # Display application (updated)
└── SharedData/                  # Shared data between apps
```

## Notes
- The Display App automatically refreshes every 30 seconds
- Changes made in Management App should appear in Display App within 30 seconds
- Both apps can run simultaneously without conflicts
- The Display App now supports unlimited groups (limited by screen width)
