# Quick Reference: Changing Supervisor to Foreman

This is a quick reference guide for changing all "Supervisor" references to "Foreman" in the application.

## File to Edit

**Location**: `SharedData/resources.xml` (or `EmployeeManagementSystem_Deploy/SharedData/resources.xml` in deployment)

## Keys to Change

Open `resources.xml` and find these keys. Change the values as shown below:

### 1. Main Display Text (DisplayApp)
**Key**: `display_supervisor`  
**Line**: ~173  
**Change from**: `Supervisor: {0}`  
**Change to**: `Foreman: {0}`

```xml
<string key="display_supervisor">Foreman: {0}</string>
```

### 2. No Supervisor Message (DisplayApp)
**Key**: `display_no_supervisor`  
**Line**: ~174  
**Change from**: `No Supervisor`  
**Change to**: `No Foreman`

```xml
<string key="display_no_supervisor">No Foreman</string>
```

### 3. Shift Supervisors Label (ManagementApp)
**Key**: `label_shift_supervisors`  
**Line**: ~64  
**Change from**: `Shift Supervisors (required):`  
**Change to**: `Shift Foremen (required):`

```xml
<string key="label_shift_supervisors">Shift Foremen (required):</string>
```

### 4. Morning Shift Supervisor Label (ManagementApp)
**Key**: `label_morning_supervisor`  
**Line**: ~65  
**Change from**: `Morning Shift Supervisor (required):`  
**Change to**: `Morning Shift Foreman (required):`

```xml
<string key="label_morning_supervisor">Morning Shift Foreman (required):</string>
```

### 5. Afternoon Shift Supervisor Label (ManagementApp)
**Key**: `label_afternoon_supervisor`  
**Line**: ~66  
**Change from**: `Afternoon Shift Supervisor (required):`  
**Change to**: `Afternoon Shift Foreman (required):`

```xml
<string key="label_afternoon_supervisor">Afternoon Shift Foreman (required):</string>
```

### 6. Night Shift Supervisor Label (ManagementApp)
**Key**: `label_night_supervisor`  
**Line**: ~67  
**Change from**: `Night Shift Supervisor (required):`  
**Change to**: `Night Shift Foreman (required):`

```xml
<string key="label_night_supervisor">Night Shift Foreman (required):</string>
```

### 7. Drag & Drop Hint (ManagementApp)
**Key**: `hint_drag_drop_supervisor`  
**Line**: ~257  
**Change from**: `Drag and drop to assign supervisor`  
**Change to**: `Drag and drop to assign foreman`

```xml
<string key="hint_drag_drop_supervisor">Drag and drop to assign foreman</string>
```

### 8. Not Assigned Message (ManagementApp)
**Key**: `supervisor_not_assigned`  
**Line**: ~258  
**Change from**: `Supervisor: Not assigned`  
**Change to**: `Foreman: Not assigned`

```xml
<string key="supervisor_not_assigned">Foreman: Not assigned</string>
```

## Steps to Apply Changes

1. Open `resources.xml` in a text editor
2. Use Find/Replace or search for each key listed above
3. Change the text values as shown
4. **Important**: Keep the `{0}` placeholder if present - do not remove it
5. Save the file (as UTF-8 encoding)
6. Restart both ManagementApp and DisplayApp

## Complete Example

Here's what the relevant section should look like after all changes:

```xml
<!-- DisplayApp Specific -->
<string key="display_supervisor">Foreman: {0}</string>
<string key="display_no_supervisor">No Foreman</string>

<!-- Labels -->
<string key="label_shift_supervisors">Shift Foremen (required):</string>
<string key="label_morning_supervisor">Morning Shift Foreman (required):</string>
<string key="label_afternoon_supervisor">Afternoon Shift Foreman (required):</string>
<string key="label_night_supervisor">Night Shift Foreman (required):</string>

<!-- Shift Group -->
<string key="hint_drag_drop_supervisor">Drag and drop to assign foreman</string>
<string key="supervisor_not_assigned">Foreman: Not assigned</string>
```

## Notes

- **Keep placeholders**: If a value contains `{0}`, `{1}`, etc., keep them in your new text
- **Backup first**: Always backup `resources.xml` before making changes
- **Restart required**: Changes only take effect after restarting both applications
- **XML syntax**: Make sure all tags are properly closed

For more detailed instructions, see `TEXT_CUSTOMIZATION_GUIDE.md`.
