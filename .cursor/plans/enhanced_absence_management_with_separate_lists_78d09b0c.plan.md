---
name: Enhanced Absence Management with Separate Lists
overview: Enhance absence management by displaying separate, categorized lists for absent, sick, and on-leave employees in both Shift Management and Employee Management sections. Add click-to-return functionality with confirmation and drag & drop support to assign employees directly to shifts.
todos: []
---

# Enhanced Absence Management with Separate Lists

## Overview

Currently, there's a single "کارمندان غایب" (Absent Employees) list that shows all employees with any absence. This plan adds separate, categorized lists for each absence type (Absent/غایب, Sick/بیمار, Leave/مرخصی) displayed as accordion sections in both Shift Management and Employee Management sections. Employees can be returned to the main list with a single click (with confirmation) or dragged directly into shifts.

## Current Implementation

- **Absence Categories**: "مرخصی" (Leave), "بیمار" (Sick), "غایب" (Absent) - defined in `Shared/Models/Absence.cs`
- **Current UI**: Single `AbsentEmployeeListBox` in Shift Management section (line 453 in `MainWindow.xaml`)
- **AbsenceManager**: Has `GetAbsencesByCategory(string category)` method (line 156 in `Absence.cs`)
- **Controller Methods**: 
  - `RemoveAbsence(Employee employee)` - removes all absences for an employee (line 1947 in `MainController.cs`)
  - `GetAllAbsences()` - gets all absences (line 1971 in `MainController.cs`)

## Implementation Plan

### 1. Replace Single Absence List with Categorized Accordion (Shift Management)

**File:** [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)

- Replace the current "کارمندان غایب" GroupBox (lines 451-456) with:
  - A new GroupBox titled "مدیریت غیبت‌ها" (Absence Management)
  - Three `Expander` controls (accordion style) for:
    - "کارمندان غایب" (Absent Employees) - category "غایب"
    - "کارمندان بیمار" (Sick Employees) - category "بیمار"
    - "کارمندان مرخصی" (Employees on Leave) - category "مرخصی"
  - Each Expander contains:
    - A `ListBox` with employee cards (photo + name)
    - Enable drag & drop (`AllowDrop="True"` on shift slots already supports this)
    - Click handler for returning to main list

### 2. Add Categorized Absence Lists (Employee Management)

**File:** [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)

- In the Employee Management section, add a new column or enhance existing absence management:
  - Add three Expander controls similar to Shift Management
  - Place them in the existing "مدیریت غیبت" GroupBox (around line 154)
  - Or create a new section below the absence input controls

### 3. Create Absence List Loading Methods

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Add method `LoadAbsenceLists()`:
  - Gets absences for today using `_controller.AbsenceManager.GetAbsencesByCategory(category)`
  - Filters to today's date only
  - Groups employees by category
  - Updates each category's ListBox
  - Uses employee cards similar to shift employee list (photo + name)

- Add method `GetEmployeesByAbsenceCategory(string category, string date)`:
  - Gets absences for category and date
  - Extracts unique employees
  - Returns list of employees

### 4. Add Click-to-Return Functionality

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Add event handler `AbsenceEmployeeListBox_MouseDoubleClick` or `AbsenceEmployeeListBox_SelectionChanged`:
  - On click/selection, show confirmation dialog: "آیا می‌خواهید این کارمند را به لیست اصلی بازگردانید؟" (Do you want to return this employee to the main list?)
  - If confirmed, call `_controller.RemoveAbsence(employee)` for today's absence
  - Refresh absence lists and employee lists
  - Show status message

- Alternative: Use `MouseLeftButtonUp` or `PreviewMouseLeftButtonDown` for single click

### 5. Enable Drag & Drop from Absence Lists

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Add drag handlers to absence list items:
  - `AbsenceEmployeeListBox_PreviewMouseLeftButtonDown`: Capture drag start
  - `AbsenceEmployeeListBox_MouseMove`: Initiate drag operation
  - Use same drag pattern as `ShiftEmployeeListBox`

- Ensure shift slots accept drops from absence lists (already supported via `Employee_DragOver`)

### 6. Create Employee Card Template for Absence Lists

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Create method `CreateAbsenceEmployeeCard(Employee employee, string category)`:
  - Creates a Border with employee photo and name
  - Similar to shift employee cards
  - Enables drag & drop
  - Adds click handler for return-to-list functionality
  - Shows category badge/indicator

- Or use DataTemplate in XAML for consistency

### 7. Update Absence Lists on Changes

**File:** [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Subscribe to `AbsencesUpdated` event in initialization
- Call `LoadAbsenceLists()` when absences are updated
- Ensure lists refresh when:
  - Employee is marked absent
  - Absence is removed
  - Date changes (if filtering by date)

### 8. Visual Design

- Each category Expander should have:
  - Distinct color coding (optional):
    - Absent (غایب): Light red/orange background
    - Sick (بیمار): Light yellow background
    - Leave (مرخصی): Light blue background
  - Clear header with category name and count
  - Employee cards with photo and name
  - Visual indication that items are draggable
  - Hover effect to indicate clickability

## Technical Details

### Absence List Structure

```xml
<Expander Header="کارمندان غایب (0)" x:Name="AbsentEmployeesExpander" IsExpanded="True">
    <ListBox x:Name="AbsentEmployeesListBox" 
             AllowDrop="False"
             MouseDoubleClick="AbsenceEmployeeListBox_MouseDoubleClick"
             PreviewMouseLeftButtonDown="AbsenceEmployeeListBox_PreviewMouseLeftButtonDown"
             MouseMove="AbsenceEmployeeListBox_MouseMove">
        <ListBox.ItemTemplate>
            <DataTemplate>
                <!-- Employee card with photo and name -->
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>
</Expander>
```

### Return to List Logic

1. User clicks on employee in absence list
2. Show confirmation: "آیا می‌خواهید {EmployeeName} را به لیست اصلی بازگردانید؟"
3. If confirmed:

   - Get today's date
   - Find absence for employee and today
   - Call `_controller.RemoveAbsence(employee)` or remove specific absence
   - Refresh absence lists
   - Refresh employee lists
   - Show success message

### Drag & Drop Integration

- Absence list employees use same drag mechanism as regular employee list
- Shift slots already accept Employee drag data
- When dropped on shift slot, absence should be automatically removed (or kept, depending on business logic)
- Consider: Should dropping on shift automatically remove absence, or just assign to shift?

## Files to Modify

1. [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml) - Replace/enhance absence lists in both sections
2. [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs) - Add absence list management, click handlers, drag handlers

## Testing Considerations

- Test loading absence lists with employees in each category
- Test click-to-return with confirmation dialog
- Test drag & drop from absence list to shift slot
- Test with employees having multiple absences
- Test with no absences (empty lists)
- Test list updates when absences are added/removed
- Test in both Shift Management and Employee Management sections
- Verify employee appears in main list after return
- Verify absence is removed from appropriate category list