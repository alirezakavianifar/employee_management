---
name: Display Managers Like Regular Employees
overview: Modify the manager display in the Display App to show managers like regular employees (name + personnel number) instead of showing "Manager" title. Managers will still be placed in the managers bar at the top, but their display format will match regular employees.
todos:
  - id: modify_manager_card
    content: Modify CreateManagerCard method to extract personnel_id and display name + personnel_id instead of name + role
    status: pending
  - id: remove_role_text
    content: Remove the roleText TextBlock creation and addition to stackPanel in CreateManagerCard
    status: pending
    dependencies:
      - modify_manager_card
  - id: verify_display_format
    content: Verify that managers display correctly with name + personnel_id format matching regular employees
    status: pending
    dependencies:
      - remove_role_text
---

# Display Managers Like Regular Employees

## Overview

Currently, managers in the Display App show their photo, name, and role title ("مدیر"). This plan changes the display to match regular employees by showing name and personnel number instead of the role title. Managers will still be placed in the managers bar at the top of the page.

## Current Implementation

- **File**: [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs)
  - `CreateManagerCard` method (lines 304-377) currently displays:
    - Manager photo (40x40)
    - Manager name (first_name + last_name)
    - Manager role text ("مدیر" or role name from data)
  - Uses a simple StackPanel layout with dark background

- **File**: [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs)
  - `CreateEmployeeCard` method (lines 707-833) displays:
    - Employee photo
    - Name + Personnel ID (format: "{fullName} {personnelId}")
    - Uses badge-style layout with blue border

- **Data Structure**: Managers have the same data structure as employees, including `personnel_id` field (from `Employee.ToDictionary()` method in `Shared/Models/Employee.cs` line 168)

## Implementation Plan

### 1. Modify CreateManagerCard Method

**File**: [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs)

- Update `CreateManagerCard` method (lines 304-377):
  - Get `personnel_id` from `managerData` using `GetValueOrDefault("personnel_id", "")`
  - Combine name and personnel ID similar to `CreateEmployeeCard`:
    - Format: `"{fullName} {personnelId}"` if personnel_id exists
    - Format: `"{fullName}"` if personnel_id is empty
  - Remove the role text (`roleText` TextBlock)
  - Update the name text to include personnel ID:
    - Change from showing just name to showing "name + personnel_id"
    - Keep the same styling (white text, bold, font size 10)
  - Maintain the same card structure (photo + name text only)

### 2. Ensure Personnel ID is Available

**File**: [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs)

- Verify that `managerData` dictionary includes `personnel_id` field
- The data comes from `Employee.ToDictionary()` which already includes `personnel_id` (line 168 in `Shared/Models/Employee.cs`)
- No changes needed to data loading - managers already have personnel_id in their data

## Technical Details

### Display Format Change

**Before:**

```
[Photo]
Name
Role (e.g., "مدیر")
```

**After:**

```
[Photo]
Name PersonnelID
```

### Code Changes

1. In `CreateManagerCard` method:

   - Extract `personnel_id` from `managerData`
   - Combine `first_name`, `last_name`, and `personnel_id` into display text
   - Remove `roleText` TextBlock creation and addition to stackPanel
   - Update `nameText` to show combined name + personnel ID

### Example Implementation

```csharp
// Get personnel ID
var personnelId = managerData.GetValueOrDefault("personnel_id", "").ToString() ?? "";
var firstName = managerData.GetValueOrDefault("first_name", "").ToString();
var lastName = managerData.GetValueOrDefault("last_name", "").ToString();
var fullName = $"{firstName} {lastName}".Trim();

// Display name with personnel ID if available
var displayName = string.IsNullOrEmpty(personnelId) ? fullName : $"{fullName} {personnelId}";

// Update nameText to use displayName instead of just name
var nameText = new TextBlock
{
    Text = displayName,
    // ... existing styling
};

// Remove roleText creation and addition
```

## Files to Modify

1. [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs)

   - Modify `CreateManagerCard` method to display name + personnel ID instead of name + role

## Testing Considerations

- Test with managers that have personnel_id → should display "Name PersonnelID"
- Test with managers without personnel_id → should display just "Name"
- Verify managers still appear in the managers bar at the top
- Verify styling matches (white text, bold, appropriate font size)
- Verify photo display remains unchanged
- Test with multiple managers to ensure layout is correct
- Compare with regular employee display to ensure consistency