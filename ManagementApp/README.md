# Management App - Employee Shift Management System (C# WPF)

A WPF-based desktop application for managing employee shifts with Persian/Arabic language support, equivalent to the Python PyQt5 version.

## Overview

The Management App is designed for workplaces that need to manage employee shifts, track absences, and handle task assignments. It features a user-friendly Persian/Arabic interface with RTL (Right-to-Left) layout support.

## Features

### Employee Management
- **Add/Edit/Delete Employees**: Manage employee information including name, role, and photo
- **CSV Import**: Import employee data from CSV files
- **Photo Management**: Automatic photo path resolution and placeholder generation
- **Role-based Categorization**: Separate employees and managers

### Shift Management
- **Two Shift Types**: Morning (صبح) and Evening (عصر) shifts
- **Configurable Capacity**: Set capacity per shift (default: 5 employees)
- **Slot-based Assignment**: Drag-and-drop employee assignment to specific slots
- **Conflict Prevention**: Prevents double-booking (employees can only be in one shift)
- **Automatic Removal**: Removes employees from shifts when marked absent

### Absence Tracking
- **Three Categories**: 
  - مرخصی (Vacation)
  - بیمار (Sick Leave)
  - غایب (Absent)
- **Shift Prevention**: Prevents shift assignment for absent employees
- **Automatic Cleanup**: Removes employees from shifts when marking absence

### Task Management
- **Create and Assign Tasks**: Full task lifecycle management
- **Priority Levels**: Set task priorities (High, Medium, Low)
- **Time Estimation**: Track estimated vs actual hours
- **Status Tracking**: Pending, In-Progress, Completed
- **Employee Assignment**: Assign tasks to specific employees

## Architecture

### Core Components

#### 1. Entry Point (`App.xaml.cs`)
- Initializes WPF application with Persian language support
- Sets up RTL layout for Arabic/Persian text
- Configures Tahoma font for better Persian text rendering
- Sets up comprehensive logging system

#### 2. Main Controller (`Controllers/MainController.cs`)
- Central business logic hub managing all data operations
- Handles employee, shift, absence, and task management
- Manages data persistence through JSON files
- Implements real-time synchronization between multiple app instances
- Emits events for UI updates when data changes

#### 3. Data Models (`Models/`)
- **Employee** (`Employee.cs`): Stores employee information and photo handling
- **Shift** (`Shift.cs`): Manages shift assignments with slot-based capacity
- **Absence** (`Absence.cs`): Tracks employee absences by category
- **Task** (`Task.cs`): Handles task management with priorities and assignments

#### 4. User Interface (`Views/`)
- **Main Window** (`MainWindow.xaml`): Primary application interface
- **Employee Dialog** (`EmployeeDialog.xaml`): Employee creation and editing
- **Task Dialog** (`TaskDialog.xaml`): Task creation and editing

#### 5. Services (`Services/`)
- **JSON Handler** (`JsonHandler.cs`): Data persistence and backup management
- **Sync Manager** (`SyncManager.cs`): Real-time synchronization between instances
- **Logging Service** (`LoggingService.cs`): Centralized logging configuration

## Data Structure

### JSON Report Format
```json
{
  "date": "2025-01-09",
  "employees": [...],
  "managers": [...],
  "shifts": {
    "morning": {
      "shift_type": "morning",
      "capacity": 5,
      "assigned_employees": [...]
    },
    "evening": {
      "shift_type": "evening", 
      "capacity": 5,
      "assigned_employees": [...]
    }
  },
  "absences": {
    "مرخصی": [...],
    "بیمار": [...],
    "غایب": [...]
  },
  "tasks": {
    "tasks": {...},
    "next_task_id": 1
  },
  "settings": {
    "shift_capacity": 5,
    "morning_capacity": 5,
    "evening_capacity": 5,
    "shared_folder_path": "Data"
  }
}
```

## Installation and Setup

### Prerequisites
- .NET 8.0 Runtime
- Windows 10/11
- Visual Studio 2022 (for development)

### Running the Application
1. Download the application files
2. Ensure .NET 8.0 Runtime is installed
3. Run `ManagementApp.exe`

### Building from Source
1. Install Visual Studio 2022 with .NET 8.0 SDK
2. Open `ManagementApp.sln` in Visual Studio
3. Restore NuGet packages
4. Build and run the solution

## Usage

### Starting the Application
1. Launch the application
2. The app will automatically load today's data or create a new report
3. If no previous data exists, it will load sample employees from CSV

### Managing Employees
1. **Add Employee**: Use the employee management section to add new employees
2. **Import from CSV**: Use the import function to load multiple employees
3. **Edit Employee**: Click on employee to edit their information
4. **Delete Employee**: Remove employees (will also remove from shifts)

### Assigning Shifts
1. **Drag and Drop**: Drag employees from the employee list to shift slots
2. **Capacity Management**: Adjust shift capacity in settings
3. **Conflict Resolution**: App prevents double-booking automatically

### Tracking Absences
1. **Mark Absent**: Select employee and choose absence category
2. **Automatic Removal**: Employee is automatically removed from shifts
3. **Remove Absence**: Clear absence to allow shift assignment again

### Task Management
1. **Create Tasks**: Add new tasks with priority and time estimates
2. **Assign Employees**: Assign tasks to specific employees
3. **Track Progress**: Update task status and completion

## Configuration

### Settings
- **Shift Capacity**: Set maximum employees per shift
- **Shared Folder**: Configure data storage location
- **Manager Selection**: Choose which employees are managers

### Data Storage
- **Reports**: Daily JSON files in `Data/Reports/`
- **Images**: Employee photos in `Data/Images/Staff/`
- **Logs**: Application logs in `Data/Logs/`
- **Backups**: Automatic backups before each save

## Synchronization

The app supports real-time synchronization between multiple instances:
- File system watching detects external changes
- Automatic data reload when changes detected
- Conflict prevention and data integrity checks
- Backup creation before each save operation

## Error Handling

- Comprehensive logging system using Microsoft.Extensions.Logging
- Graceful error recovery
- User-friendly error dialogs in Persian
- Automatic backup and restore mechanisms
- Data validation and integrity checks

## Technical Details

### Dependencies
- **WPF**: Windows Presentation Foundation for GUI
- **Newtonsoft.Json**: JSON serialization
- **Microsoft.Extensions.Logging**: Logging framework
- **System.IO.FileSystem.Watcher**: File system monitoring
- **Image Handling**: Employee photo management

### Performance Features
- Efficient data loading and saving
- Optimized UI updates through events
- Memory-conscious image handling
- Background synchronization
- Lazy loading for large datasets

### Internationalization
- Full Persian/Arabic language support
- RTL layout for proper text display
- Persian calendar integration
- Unicode text handling

## File Structure

```
ManagementApp/
├── App.xaml                 # Application entry point
├── App.xaml.cs             # Application initialization
├── Controllers/
│   └── MainController.cs   # Business logic controller
├── Models/
│   ├── Employee.cs         # Employee data model
│   ├── Shift.cs           # Shift management model
│   ├── Absence.cs         # Absence tracking model
│   └── Task.cs            # Task management model
├── Views/
│   ├── MainWindow.xaml    # Main application window
│   ├── MainWindow.xaml.cs # Main window logic
│   ├── EmployeeDialog.xaml # Employee management dialog
│   ├── EmployeeDialog.xaml.cs
│   ├── TaskDialog.xaml    # Task management dialog
│   └── TaskDialog.xaml.cs
├── Services/
│   ├── JsonHandler.cs     # Data persistence
│   ├── SyncManager.cs     # Synchronization
│   └── LoggingService.cs  # Logging configuration
├── Resources/
│   ├── Colors.xaml        # Color definitions
│   ├── Fonts.xaml         # Font configurations
│   └── Styles.xaml        # UI styles
├── sample_employees.csv   # Sample employee data
└── ManagementApp.csproj   # Project file
```

## Troubleshooting

### Common Issues
1. **Photo Not Loading**: Check file paths and permissions
2. **Sync Issues**: Verify file permissions and network access
3. **Data Loss**: Check backup files in reports directory
4. **UI Issues**: Ensure .NET 8.0 Runtime is properly installed

### Log Files
- `Data/Logs/management_app.log`: Main application logs
- `Data/Logs/json_handler.log`: Data persistence logs
- `Data/Logs/sync_manager.log`: Synchronization logs

## License

This project is part of a workshop management system. Please refer to the project documentation for licensing information.

## Support

For technical support or questions about the management app, please refer to the project documentation or contact the development team.

## Migration from Python Version

This C# WPF version maintains full compatibility with the Python PyQt5 version:
- Same JSON data format
- Same file structure
- Same synchronization mechanism
- Same feature set
- Improved performance and stability

Data files created by either version can be used interchangeably.
