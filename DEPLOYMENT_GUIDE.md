# Employee Management System - Deployment Guide

## Portable Deployment

This application is designed to be portable and work on different machines without requiring installation or specific directory structures.

### Directory Structure

When deploying the application, maintain this directory structure:

```
YourDeploymentFolder/
├── ManagementApp/
│   ├── ManagementApp.exe
│   ├── Config/
│   │   └── app_config.json
│   └── [other DLL files]
├── DisplayApp/
│   ├── DisplayApp.exe
│   ├── Config/
│   │   └── app_config.json
│   └── [other DLL files]
├── SharedData/
│   ├── Reports/
│   ├── Images/
│   │   └── Staff/
│   └── Logs/
└── sample_employees.csv (optional)
```

### Configuration Files

Both applications use relative paths in their configuration files:

**ManagementApp/Config/app_config.json:**
```json
{
  "DataDirectory": "../SharedData",
  "ReportsDirectory": "../SharedData/Reports",
  "ImagesDirectory": "../SharedData/Images",
  "LogsDirectory": "../SharedData/Logs",
  "SyncEnabled": true,
  "SyncIntervalSeconds": 30
}
```

**DisplayApp/Config/app_config.json:**
```json
{
  "DataDirectory": "../SharedData",
  "ReportsDirectory": "../SharedData/Reports",
  "ImagesDirectory": "../SharedData/Images",
  "LogsDirectory": "../SharedData/Logs",
  "SyncEnabled": true,
  "SyncIntervalSeconds": 30
}
```

### Automatic Data Directory Detection

The application automatically detects the SharedData directory in the following order:

1. **Relative to current directory**: `../SharedData`
2. **Relative to executable**: `../SharedData` (from executable location)
3. **Same directory as executable**: `SharedData` (for portable deployment)
4. **Development structure**: `../../../SharedData` (for development)
5. **Fallback**: Creates `SharedData` in current directory

### Deployment Steps

1. **Copy the entire folder** to the target machine
2. **Ensure the directory structure** is maintained
3. **Run ManagementApp.exe** first to create initial data
4. **Run DisplayApp.exe** to view the data

### Data Persistence

- All data is stored in the `SharedData` folder
- Reports are saved with Persian calendar dates (e.g., `report_1404-06-26.json`)
- Employee photos are stored in `SharedData/Images/Staff/`
- Logs are stored in `SharedData/Logs/`

### Troubleshooting

**If the Display App can't find data:**

1. Check that `SharedData` folder exists in the same directory as the executables
2. Verify that `ManagementApp` has been run at least once to create data
3. Check the log files in `SharedData/Logs/` for error messages
4. Ensure both applications have write permissions to the `SharedData` folder

**If paths are incorrect:**

1. The application will automatically create the `SharedData` directory if it doesn't exist
2. Check that the configuration files are in the correct locations
3. The application logs the data directory path on startup - check the console output

### Network Deployment

For network deployment:

1. Place the application on a shared network drive
2. Ensure all users have read/write access to the `SharedData` folder
3. Run `ManagementApp` from one machine to initialize the data
4. Other users can run `DisplayApp` from their machines

### Backup

To backup the application:

1. Copy the entire deployment folder
2. The `SharedData` folder contains all user data
3. Configuration files in `Config/` folders contain settings

### Updates

To update the application:

1. Stop all running instances
2. Replace the executable files and DLLs
3. Keep the `SharedData` and `Config` folders unchanged
4. Restart the applications
