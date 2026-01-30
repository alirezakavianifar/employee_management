---
name: Simplify Task Management with Shift Group Selection
overview: Simplify the task management section by replacing individual employee selection with shift group selection. When a shift group is selected, all employees in that group (from both morning and evening shifts) are automatically assigned to the task. Also change the default working time from 1.0 hours to 8.0 hours for EstimatedHours.
todos:
  - id: add_group_methods
    content: Add GetEmployeesFromShiftGroup() and AssignTaskToShiftGroup() methods to MainController
    status: completed
  - id: replace_employee_dialog
    content: Replace ShowEmployeeAssignmentDialog() to use shift group selection instead of individual employee selection
    status: completed
    dependencies:
      - add_group_methods
  - id: change_default_hours
    content: Change default EstimatedHours from 1.0 to 8.0 in TaskDialog.xaml, MainWindow.xaml, Task.cs, and MainController.cs
    status: completed
---

# Simplify Task Management with Shift Group Selection

## Overview

Currently, task management requires selecting employees one by one from a list. This plan simplifies the process by allowing selection based on shift groups. When a shift group is selected, all employees in that group (from both morning and evening shifts) are automatically assigned to the task. Additionally, the default working time is changed from 1.0 hours to 8.0 hours.

## Current Implementation

- **File**: [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)
  - `ShowEmployeeAssignmentDialog()` method (lines 3801-3915) shows a ListBox with individual employees
  - User selects one employee at a time
  - Each employee is assigned individually via `AssignTaskToEmployee()`

- **File**: [`ManagementApp/Views/TaskDialog.xaml`](ManagementApp/Views/TaskDialog.xaml)
  - `EstimatedHoursTextBox` has default value "1.0" (line 47)

- **File**: [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)
  - `TaskEstimatedHoursTextBox` has default value "1.0" (line 804)

- **File**: [`Shared/Models/Task.cs`](Shared/Models/Task.cs)
  - `EstimatedHours` default is 1.0 (line 44)
  - Constructor default is 1.0 (line 69)

- **File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)
  - `AddTask()` method default is 1.0 (line 2125)
  - `AssignTaskToEmployee()` assigns one employee at a time (line 2374)

## Implementation Plan

### 1. Add Method to Get All Employees from Shift Group

**File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

- Add new method `GetEmployeesFromShiftGroup(string groupId)`:
  - Get the shift group using `ShiftGroupManager.GetShiftGroup(groupId)`
  - Get all employees from `MorningShift.AssignedEmployees` (filter nulls)
  - Get all employees from `EveningShift.AssignedEmployees` (filter nulls)
  - Combine and return unique employees (using `Distinct()` or `Union()`)
  - Return `List<Employee>`

- Add new method `AssignTaskToShiftGroup(string taskId, string groupId)`:
  - Get all employees from the shift group using `GetEmployeesFromShiftGroup(groupId)`
  - Loop through each employee and call `AssignTaskToEmployee(taskId, employee.EmployeeId)`
  - Return success status (true if all assignments succeed, false otherwise)
  - Log the number of employees assigned

### 2. Replace Employee Selection Dialog with Group Selection

**File**: [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Modify `ShowEmployeeAssignmentDialog()` method (lines 3801-3915):
  - Change dialog title to "تخصیص گروه شیفت به وظیفه"
  - Change label text to "گروه شیفت مورد نظر را انتخاب کنید:"
  - Replace employee ListBox with shift group ComboBox or ListBox:
    - Get all shift groups using `_controller.GetAllShiftGroups()` or `_controller.GetActiveShiftGroups()`
    - Display group names
    - Store group IDs in Tag or use group object directly
  - Update assign button logic:
    - Instead of `AssignTaskToEmployee()`, call `AssignTaskToShiftGroup(taskId, groupId)`
    - Show message indicating how many employees were assigned
    - Update status message to show group name instead of individual employee name

### 3. Change Default Working Time to 8 Hours

**File**: [`ManagementApp/Views/TaskDialog.xaml`](ManagementApp/Views/TaskDialog.xaml)

- Change `EstimatedHoursTextBox` default value from "1.0" to "8.0" (line 47)

**File**: [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)

- Change `TaskEstimatedHoursTextBox` default value from "1.0" to "8.0" (line 804)

**File**: [`Shared/Models/Task.cs`](Shared/Models/Task.cs)

- Change `EstimatedHours` default from 1.0 to 8.0 (line 44)
- Change constructor default from 1.0 to 8.0 (line 69)

**File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

- Change `AddTask()` method default from 1.0 to 8.0 (line 2125)

### 4. Update Task Assignment Display

**File**: [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- `LoadTaskAssignments()` method (lines 4032-4062) can remain the same - it already displays assigned employees
- Consider adding group information to the display (optional enhancement)

## Technical Details

### Shift Group Employee Retrieval

```csharp
public List<Employee> GetEmployeesFromShiftGroup(string groupId)
{
    var group = ShiftGroupManager.GetShiftGroup(groupId);
    if (group == null) return new List<Employee>();
    
    var employees = new List<Employee>();
    
    // Get employees from morning shift
    if (group.MorningShift != null)
    {
        employees.AddRange(group.MorningShift.AssignedEmployees
            .Where(emp => emp != null)
            .Cast<Employee>());
    }
    
    // Get employees from evening shift
    if (group.EveningShift != null)
    {
        employees.AddRange(group.EveningShift.AssignedEmployees
            .Where(emp => emp != null)
            .Cast<Employee>());
    }
    
    // Return unique employees (in case someone is in both shifts, though unlikely)
    return employees.Distinct().ToList();
}
```

### Group Assignment Logic

```csharp
public bool AssignTaskToShiftGroup(string taskId, string groupId)
{
    var employees = GetEmployeesFromShiftGroup(groupId);
    if (employees.Count == 0) return false;
    
    bool allSuccess = true;
    foreach (var employee in employees)
    {
        var success = AssignTaskToEmployee(taskId, employee.EmployeeId);
        if (!success) allSuccess = false;
    }
    
    if (allSuccess)
    {
        TasksUpdated?.Invoke();
        SaveData();
    }
    
    return allSuccess;
}
```

### UI Changes

- Replace employee ListBox with shift group ComboBox/ListBox
- Show group names (e.g., "گروه A", "گروه B")
- When group is selected and assigned, show message like "تمام کارمندان گروه {GroupName} به وظیفه تخصیص داده شدند ({Count} نفر)"

## Files to Modify

1. [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

   - Add `GetEmployeesFromShiftGroup()` method
   - Add `AssignTaskToShiftGroup()` method

2. [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

   - Modify `ShowEmployeeAssignmentDialog()` to use shift groups instead of individual employees

3. [`ManagementApp/Views/TaskDialog.xaml`](ManagementApp/Views/TaskDialog.xaml)

   - Change default EstimatedHours from "1.0" to "8.0"

4. [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)

   - Change default EstimatedHours from "1.0" to "8.0"

5. [`Shared/Models/Task.cs`](Shared/Models/Task.cs)

   - Change default EstimatedHours from 1.0 to 8.0

## Testing Considerations

- Test selecting a shift group with employees → should assign all employees from both shifts
- Test selecting a shift group with no employees → should show appropriate message
- Test with groups that have employees in both morning and evening shifts
- Test with groups that have employees in only one shift
- Verify default EstimatedHours is 8.0 in all places (TaskDialog, MainWindow, Task model)
- Test creating new tasks → should default to 8.0 hours
- Test updating existing tasks → should preserve their current hours
- Verify assigned employees list shows all employees from the selected group
- Test removing employees from task (should still work individually)