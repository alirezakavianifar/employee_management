# Management App - Employee Shift Management System

A PyQt5-based desktop application for managing employee shifts with Persian/Arabic language support.

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

#### 1. Entry Point (`main.py`)
- Initializes PyQt5 application with Persian language support
- Sets up RTL layout for Arabic/Persian text
- Configures Tahoma font for better Persian text rendering
- Handles dynamic imports for PyInstaller compatibility
- Sets up comprehensive logging system

#### 2. Main Controller (`controllers/main_controller.py`)
- Central business logic hub managing all data operations
- Handles employee, shift, absence, and task management
- Manages data persistence through JSON files
- Implements real-time synchronization between multiple app instances
- Emits signals for UI updates when data changes

#### 3. Data Models (`models/`)
- **Employee** (`employee.py`): Stores employee information and photo handling
- **Shift** (`shift.py`): Manages shift assignments with slot-based capacity
- **Absence** (`absence.py`): Tracks employee absences by category
- **Task** (`task.py`): Handles task management with priorities and assignments

#### 4. User Interface (`views/`, `ui/`, `widgets/`)
- **Main Window** (`views/main_window.py`): Primary application interface
- **Export Dialog** (`ui/export_dialog.py`): Report export functionality
- **Task Dialog** (`ui/task_dialog.py`): Task creation and editing
- **Draggable Employee** (`widgets/draggable_employee.py`): Drag-and-drop functionality

#### 5. Shared Components (`shared/`)
- **JSON Handler** (`json_handler.py`): Data persistence and backup management
- **Sync Manager** (`sync.py`): Real-time synchronization between instances
- **AI Rules** (`ai_rules.py`): Business logic rules and constraints

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
    "shared_folder_path": "data"
  }
}
```

## Installation and Setup

### Prerequisites
- Python 3.7+
- PyQt5
- Required Python packages (see requirements.txt)

### Running from Source
1. Clone or extract the source code
2. Install dependencies: `pip install -r requirements.txt`
3. Run the application: `python management_app/main.py`

### Building Executable
1. Install PyInstaller: `pip install pyinstaller`
2. Run build script: `python build_fixed.py`
3. Executable will be created in the `dist` folder

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
- **Reports**: Daily JSON files in `data/reports/`
- **Images**: Employee photos in `data/images/staff/`
- **Logs**: Application logs in `data/logs/`
- **Backups**: Automatic backups before each save

## Synchronization

The app supports real-time synchronization between multiple instances:
- File system watching detects external changes
- Automatic data reload when changes detected
- Conflict prevention and data integrity checks
- Backup creation before each save operation

## Error Handling

- Comprehensive logging system
- Graceful error recovery
- User-friendly error dialogs in Persian
- Automatic backup and restore mechanisms
- Data validation and integrity checks

## Technical Details

### Dependencies
- **PyQt5**: GUI framework
- **JSON**: Data serialization
- **CSV**: Employee import/export
- **File System Watching**: Real-time synchronization
- **Image Handling**: Employee photo management

### Performance Features
- Efficient data loading and saving
- Optimized UI updates through signals
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
management_app/
├── main.py                 # Application entry point
├── controllers/
│   └── main_controller.py  # Business logic controller
├── models/
│   ├── employee.py         # Employee data model
│   ├── shift.py           # Shift management model
│   ├── absence.py         # Absence tracking model
│   └── task.py            # Task management model
├── views/
│   └── main_window.py     # Main application window
├── ui/
│   ├── export_dialog.py   # Export functionality
│   └── task_dialog.py     # Task management UI
├── widgets/
│   └── draggable_employee.py # Drag-and-drop functionality
├── services/
│   └── export_service.py  # Export services
└── utils/
    └── (utility modules)

shared/
├── json_handler.py        # Data persistence
├── sync.py               # Synchronization
├── ai_rules.py           # Business rules
└── improved_sync.py      # Enhanced sync features
```

## Troubleshooting

### Common Issues
1. **Photo Not Loading**: Check file paths and permissions
2. **Sync Issues**: Verify file permissions and network access
3. **Data Loss**: Check backup files in reports directory
4. **UI Issues**: Ensure PyQt5 is properly installed

### Log Files
- `management_app.log`: Main application logs
- `json_handler.log`: Data persistence logs
- `sync_manager.log`: Synchronization logs

## License

This project is part of a workshop management system. Please refer to the project documentation for licensing information.

## Support

For technical support or questions about the management app, please refer to the project documentation or contact the development team.
