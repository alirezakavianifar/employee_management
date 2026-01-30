---
name: Confirmation Dialog for Group Assignment Conflicts
overview: Replace error dialogs with confirmation dialogs when assigning employees to groups. When conflicts occur (employee is sick/on leave/absent or already assigned to another group), show a confirmation dialog asking if the manager wants to remove the employee from the previous group and move them to the new group. If confirmed, automatically remove the conflicts and complete the assignment.
todos:
  - id: create_result_classes
    content: Create AssignmentResult, AssignmentConflict classes and ConflictType enum in MainController.cs
    status: pending
  - id: modify_assign_method
    content: Modify AssignEmployeeToShift to return AssignmentResult instead of bool, remove error dialogs for conflicts
    status: pending
    dependencies:
      - create_result_classes
  - id: add_remove_method
    content: Add RemoveEmployeeFromPreviousAssignment method to MainController to handle conflict removal
    status: pending
    dependencies:
      - create_result_classes
  - id: update_ui_calls
    content: Update all AssignEmployeeToShift calls in MainWindow.xaml.cs to handle AssignmentResult
    status: pending
    dependencies:
      - modify_assign_method
  - id: add_confirmation_dialog
    content: Add ShowAssignmentConflictDialog helper method in MainWindow.xaml.cs to show confirmation dialogs
    status: pending
    dependencies:
      - update_ui_calls
  - id: implement_conflict_resolution
    content: "Implement conflict resolution logic: show dialog, remove conflicts if confirmed, retry assignment"
    status: pending
    dependencies:
      - add_confirmation_dialog
      - add_remove_method
---

# Confirmation Dialog for Group Assignment Conflicts

## Overview

Currently, when trying to assign an employee to a new group, if the employee is sick, on leave, absent, or already assigned to another group, an error dialog is shown. This plan replaces those error dialogs with a confirmation dialog that allows the manager to automatically remove the employee from the previous group and move them to the new group.

## Current Implementation

- **File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)
  - `AssignEmployeeToShift` method (lines 1711-1795) shows error dialogs for three conflict types:

    1. Employee is absent/sick/on leave (lines 1736-1743)
    2. Employee is already assigned to another shift in the same group (lines 1745-1753)
    3. Employee is assigned to a different group (lines 1755-1763)

  - Uses `ShowErrorDialog` which displays a MessageBox

- **File**: [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)
  - Calls `_controller.AssignEmployeeToShift` from multiple places (lines 2241, 2818, 2877, 2917, 3254)
  - Currently doesn't handle conflict scenarios - just checks if assignment succeeded

## Implementation Plan

### 1. Create Assignment Result Class

**File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

- Create a new class `AssignmentResult` to represent the result of an assignment attempt:
  ```csharp
  public class AssignmentResult
  {
      public bool Success { get; set; }
      public AssignmentConflict? Conflict { get; set; }
      public string? ErrorMessage { get; set; }
  }
  
  public class AssignmentConflict
  {
      public ConflictType Type { get; set; }
      public string? CurrentGroupId { get; set; }
      public string? CurrentGroupName { get; set; }
      public string? CurrentShiftType { get; set; }
      public string? AbsenceType { get; set; } // "غایب", "بیمار", "مرخصی"
  }
  
  public enum ConflictType
  {
      None,
      Absent,              // Employee is absent/sick/on leave
      DifferentShift,      // Employee assigned to different shift in same group
      DifferentGroup       // Employee assigned to different group
  }
  ```

- Place this class before the `MainController` class definition

### 2. Modify AssignEmployeeToShift to Return Result Object

**File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

- Change method signature from `public bool AssignEmployeeToShift(...)` to `public AssignmentResult AssignEmployeeToShift(...)`
- Remove all `ShowErrorDialog` calls for conflicts (lines 1740-1742, 1750-1752, 1760-1762)
- Instead, return `AssignmentResult` with conflict information:
  - For absence conflict (line 1736-1743): Return result with `ConflictType.Absent` and `AbsenceType`
  - For different shift conflict (line 1745-1753): Return result with `ConflictType.DifferentShift` and `CurrentShiftType`
  - For different group conflict (line 1755-1763): Return result with `ConflictType.DifferentGroup`, `CurrentGroupId`, and `CurrentGroupName`
- Keep `ShowErrorDialog` for actual errors (group not found, shift not found, etc.) but return error result
- On successful assignment, return result with `Success = true`

### 3. Add Method to Remove Employee from Previous Assignment

**File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

- Add new method `RemoveEmployeeFromPreviousAssignment(Employee employee, AssignmentConflict conflict)`:
  - If conflict type is `Absent`: Remove today's absence using `AbsenceManager.RemoveAbsence`
  - If conflict type is `DifferentShift`: Remove employee from the conflicting shift using `RemoveEmployeeFromShift`
  - If conflict type is `DifferentGroup`: Remove employee from all shifts in the previous group (both morning and evening)
  - Invoke appropriate events (`AbsencesUpdated`, `ShiftsUpdated`, `ShiftGroupsUpdated`)
  - Save data
  - Return success status

### 4. Update UI Layer to Handle Conflicts

**File**: [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Update all calls to `AssignEmployeeToShift` (lines 2241, 2818, 2877, 2917, 3254):
  - Change from `bool success = _controller.AssignEmployeeToShift(...)` to `var result = _controller.AssignEmployeeToShift(...)`
  - Check `result.Success`:
    - If `true`: Proceed with existing success logic (refresh UI, show status message)
    - If `false` and `result.Conflict != null`: Show confirmation dialog
  - Create helper method `ShowAssignmentConflictDialog(AssignmentConflict conflict, Employee employee, string targetGroupName, string targetShiftType)`:
    - Build confirmation message based on conflict type:
      - Absent: "کارمند {Name} به عنوان {AbsenceType} ثبت شده است. آیا می‌خواهید غیبت را حذف کرده و کارمند را به گروه {GroupName} تخصیص دهید؟"
      - DifferentShift: "کارمند {Name} قبلاً به شیفت {CurrentShift} در این گروه تخصیص داده شده است. آیا می‌خواهید از شیفت قبلی حذف شده و به شیفت {TargetShift} تخصیص داده شود؟"
      - DifferentGroup: "کارمند {Name} قبلاً به گروه {CurrentGroupName} تخصیص داده شده است. آیا می‌خواهید از گروه قبلی حذف شده و به گروه {TargetGroupName} تخصیص داده شود؟"
    - Show `MessageBox` with Yes/No buttons
    - Return `MessageBoxResult.Yes` or `MessageBoxResult.No`
  - If user confirms:
    - Call `RemoveEmployeeFromPreviousAssignment` (to be added to controller)
    - Retry `AssignEmployeeToShift` with same parameters
    - If retry succeeds, proceed with success logic
    - If retry fails, show error message

### 5. Add Controller Method for Removing Previous Assignment

**File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

- Add public method `RemoveEmployeeFromPreviousAssignment(Employee employee, AssignmentConflict conflict)`:
  - Handle each conflict type:
    - `Absent`: Get today's absence and remove it using `AbsenceManager.RemoveAbsence(absence)`
    - `DifferentShift`: Call `RemoveEmployeeFromShift(employee, conflict.CurrentShiftType, groupId)` for current group
    - `DifferentGroup`: 
      - Get all shifts employee is assigned to in the previous group using `GetEmployeeShifts`
      - Remove from each shift using `RemoveEmployeeFromShift`
  - Invoke `AbsencesUpdated` if absence was removed
  - Invoke `ShiftsUpdated` and `ShiftGroupsUpdated` if shift assignments were removed
  - Call `SaveData()`
  - Return `true` on success

## Technical Details

### Conflict Resolution Flow

```
User tries to assign employee to group
    ↓
AssignEmployeeToShift returns conflict
    ↓
UI shows confirmation dialog
    ↓
User clicks "Yes"
    ↓
RemoveEmployeeFromPreviousAssignment removes conflicts
    ↓
Retry AssignEmployeeToShift
    ↓
Assignment succeeds → Refresh UI
```

### Message Format

The confirmation messages should be in Persian and clearly explain:

- What the current situation is
- What action will be taken if confirmed
- The target group/shift they will be moved to

## Files to Modify

1. [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

   - Add `AssignmentResult`, `AssignmentConflict`, and `ConflictType` classes/enum
   - Modify `AssignEmployeeToShift` to return `AssignmentResult`
   - Add `RemoveEmployeeFromPreviousAssignment` method

2. [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

   - Update all `AssignEmployeeToShift` calls to handle `AssignmentResult`
   - Add `ShowAssignmentConflictDialog` helper method
   - Implement conflict resolution logic with confirmation dialogs

## Testing Considerations

- Test assigning employee who is absent → should show confirmation, remove absence, assign
- Test assigning employee who is sick → should show confirmation, remove absence, assign
- Test assigning employee who is on leave → should show confirmation, remove absence, assign
- Test assigning employee to different shift in same group → should show confirmation, remove from previous shift, assign
- Test assigning employee to different group → should show confirmation, remove from all shifts in previous group, assign
- Test clicking "No" in confirmation → should cancel assignment, no changes made
- Test multiple conflicts (e.g., absent AND in different group) → should handle appropriately
- Verify UI refreshes correctly after conflict resolution
- Verify data is saved after conflict resolution