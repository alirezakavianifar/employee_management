---
name: Shift Supervisor Drag and Drop Selection
overview: Add dedicated drop areas for Shift Supervisors in the Shift Management section. Employees can be dragged and dropped into these areas to automatically become the shift supervisor, with the previous supervisor being returned to the employee list. Each shift (morning/evening) will have its own supervisor area.
todos: []
---

# Shift Supervisor Drag & Drop Selection

## Overview

Add dedicated drop areas for Shift Supervisors in the Shift Management section. When an employee is dragged and dropped into a supervisor area, they automatically become the shift supervisor, and the previous supervisor (if any) is returned to the employee list. Each shift (morning/evening) will have its own supervisor area placed inside the shift GroupBox, above the shift slots.

## Current Implementation

- **Shift Model**: Has `TeamLeaderId` property (line 13 in `Shared/Models/Shift.cs`)
- **Controller Methods**: 
  - `SetTeamLeader(string shiftType, string employeeId, string? groupId = null)` (line 2498 in `MainController.cs`)
  - `GetTeamLeader(string shiftType, string? groupId = null)` (line 2527 in `MainController.cs`)
- **UI Structure**: 
  - Morning and Evening shifts are in separate GroupBoxes (lines 363-374 in `MainWindow.xaml`)
  - Each GroupBox contains a ScrollViewer with a StackPanel for shift slots
  - Shift slots are created dynamically in `CreateShiftSlot` method

## Implementation Plan

### 1. Add Supervisor Drop Areas to XAML

**File:** [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)

- Modify the Morning Shift GroupBox (around line 363):
  - Add a new RowDefinition or StackPanel above the ScrollViewer
  - Create a Border/GroupBox for "سرپرست شیفت صبح" (Morning Shift Supervisor)
  - Set `AllowDrop="True"`
  - Add event handlers: `DragOver`, `DragEnter`, `DragLeave`, `Drop`
  - Style it to be visually distinct (different background, border, padding)
  - Display current supervisor if one exists

- Modify the Evening Shift GroupBox (around line 370):
  - Add similar supervisor drop area for "سرپرست شیفت عصر" (Evening Shift Supervisor)
  - Same styling and event handlers

### 2. Create Supervisor Display Component

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Add method `CreateSupervisorDropArea(string shiftType)`:
  - Creates a Border or GroupBox with:
    - Label/Header: "سرپرست شیفت [صبح/عصر]"
    - Area to display current supervisor (photo + name)
    - Visual indication when empty ("هیچ سرپرستی انتخاب نشده")
    - Styling to indicate it's a drop target
  - Returns the UI element

- Add method `UpdateSupervisorDisplay(string shiftType)`:
  - Gets current supervisor using `_controller.GetTeamLeader(shiftType, groupId)`
  - Updates the supervisor display area
  - Shows employee photo and name if supervisor exists
  - Shows placeholder text if no supervisor

### 3. Add Drag & Drop Handlers

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Add `SupervisorArea_DragOver` handler:
  - Check if drag data contains Employee
  - Set `e.Effects = DragDropEffects.Move` if valid
  - Provide visual feedback

- Add `SupervisorArea_DragEnter` handler:
  - Highlight the supervisor area (change background/border)
  - Show move cursor

- Add `SupervisorArea_DragLeave` handler:
  - Reset visual feedback

- Add `SupervisorArea_Drop` handler:
  - Extract employee from drag data
  - Get current supervisor (if any)
  - If current supervisor exists:
    - Remove them from shift (if they're assigned)
    - Return them to employee list
  - Set new employee as supervisor using `_controller.SetTeamLeader(shiftType, employee.EmployeeId, groupId)`
  - If new supervisor is not assigned to shift, automatically add them using `_controller.AssignEmployeeToShift`
  - Refresh UI: `UpdateSupervisorDisplay`, `LoadShiftSlots`, `LoadEmployees`, `UpdateShiftStatistics`

### 4. Integrate Supervisor Areas into Shift Loading

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Modify `LoadShiftSlots` method (around line 1067):
  - After clearing panels, add supervisor areas to each shift GroupBox
  - Call `UpdateSupervisorDisplay` for both shifts
  - Ensure supervisor areas are added before shift slots

- Alternative approach: Add supervisor areas directly in XAML and update them in code-behind

### 5. Handle Supervisor Removal

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- When setting a new supervisor:
  - Get previous supervisor using `GetTeamLeader`
  - If previous supervisor exists and is assigned to the shift:
    - Remove them from shift using `RemoveEmployeeFromShift`
    - This will automatically return them to the employee list
  - Set new supervisor
  - If new supervisor is not in shift, add them automatically

### 6. Visual Design

- Supervisor area should be:
  - Visually distinct (different background color, e.g., light blue or gold)
  - Clearly labeled
  - Show current supervisor prominently (photo + name)
  - Indicate it's a drop target (dashed border when empty, solid when has supervisor)
  - Provide hover/drag feedback

## Technical Details

### Supervisor Area Structure

```xml
<Border x:Name="MorningSupervisorArea" 
        AllowDrop="True"
        DragOver="SupervisorArea_DragOver"
        DragEnter="SupervisorArea_DragEnter"
        DragLeave="SupervisorArea_DragLeave"
        Drop="SupervisorArea_Drop"
        Background="#E3F2FD"
        BorderBrush="#1976D2"
        BorderThickness="2"
        CornerRadius="5"
        Padding="10"
        Margin="5">
    <StackPanel>
        <Label Content="سرپرست شیفت صبح" FontWeight="Bold"/>
        <StackPanel x:Name="MorningSupervisorContent">
            <!-- Supervisor display or placeholder -->
        </StackPanel>
    </StackPanel>
</Border>
```

### Supervisor Assignment Logic

1. Extract employee from drag data
2. Get current supervisor: `var currentSupervisor = _controller.GetTeamLeader(shiftType, groupId)`
3. If current supervisor exists:

   - Check if they're assigned to shift
   - If assigned, remove them: `_controller.RemoveEmployeeFromShift(currentSupervisor, shiftType, groupId)`

4. Set new supervisor: `_controller.SetTeamLeader(shiftType, employee.EmployeeId, groupId)`
5. Check if new supervisor is assigned to shift
6. If not assigned, add them: `_controller.AssignEmployeeToShift(employee, shiftType, null, groupId)`
7. Refresh all UI components

## Files to Modify

1. [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml) - Add supervisor drop areas
2. [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs) - Add drag handlers and supervisor management logic

## Testing Considerations

- Test dragging employee from list to supervisor area
- Test dragging employee from shift slot to supervisor area
- Test replacing existing supervisor (verify previous supervisor returns to list)
- Test with employees not assigned to shift (verify auto-assignment)
- Test with different shift groups
- Verify supervisor display updates correctly
- Test visual feedback during drag operations
- Verify supervisor persists when switching shift groups