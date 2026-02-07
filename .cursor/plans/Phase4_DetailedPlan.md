# Phase 4: UI Restructure to Table Layout - Detailed Plan

## Goal
Redesign the Shift Management UI in both **ManagementApp** and **DisplayApp** to a 3-row x N-column table layout.
- **Rows**: 3 Shifts (Morning, Afternoon, Night)
- **Columns**: N Groups (Dynamic)
- **Cell**: Contains Employee assignment OR Status Card.

## 1. ManagementApp Implementation

### 1.1. Create `ShiftGroupControl` (New UserControl)
Instead of a monolithic MainWindow grid, we will create a reusable `ShiftGroupControl` to represent one column (one group).

**File:** `ManagementApp/Controls/ShiftGroupControl.xaml`
**Layout:**
- **Header:** Group Name (Top)
- **Grid:** 3 Rows (Morning, Afternoon, Night) - Uniform height.
- **Drop Areas:**
  - `MorningGap`: Border with Drop event.
  - `AfternoonGap`: Border with Drop event.
  - `NightGap`: Border with Drop event.
- **Content:** Inside each Drop Area, display either:
  - `EmployeeCard` (Photo + Name + Label)
  - `StatusCard` (Color + Name)
  - `EmptyState` (Placeholder text)

**Code Behind (`ShiftGroupControl.xaml.cs`):**
- Properties: `ShiftGroup` (Model)
- Events: `EmployeeDropped`, `StatusCardDropped`, `ItemRemoved`
- Logic: Handle DragOver/Drop events locally, then raise events for the MainController to handle the data update.

### 1.2. Update `MainWindow.xaml`
**Target:** "Shift Management" Tab (`TabItem Header="مدیریت شیفت"`)

- **Remove:** The 5-column grid layout (`Employee List | Splitter | Shift Assignment | Splitter | Absence`).
- **New Layout:**
  - **Left Panel (Fixed):** Employee List & Label Panel (Source of Drags).
  - **Main Area (Scrollable):** 
    - `ItemsControl` bound to `AllShiftGroups`.
    - `ItemsPanel`: `VirtualizingStackPanel` (Orientation="Horizontal").
    - `ItemTemplate`: Uses `ShiftGroupControl`.
  - **Status Cards Panel:** Add a sidebar or expandable section to view and drag Status Cards.

### 1.3. Logic Updates (`MainWindow.xaml.cs`)
- **Data Loading:** `LoadShifts` should populate `ObservableCollection<ShiftGroup> ShiftGroups`.
- **Drag & Drop:**
  - Update `Employee_DragOver` to carry Employee ID.
  - Update `ShiftGroupControl` drop handlers to extract Employee ID or Status ID.
  - Call `_controller.AssignEmployeeToShift(groupId, shiftType, employeeId)`
  - Call `_controller.AssignStatusCardToShift(groupId, shiftType, statusCardId)`

## 2. DisplayApp Implementation

### 2.1. Update `CreateGroupPanel` logic
**File:** `DisplayApp/MainWindow.xaml.cs`
**Method:** `CreateGroupPanel`

- **Current:** Inner Grid has 3 Columns (Shifts).
- **New:** Inner Grid has 3 Rows (Morning, Afternoon, Night).
  - Row 0: Morning
  - Row 1: Afternoon
  - Row 2: Night
- **Styling:** Ensure standard height for rows to align visually across groups.

### 2.2. Update `CreateShiftPanel`
- Ensure it renders `StatusCards` if present.
- If `StatusCardId` is present in the slot model, render a colored card instead of Employee details.

## 3. Migration & Compatibility
- No database schema changes (handled in Phase 1).
- Ensure `ShiftGroup` model correctly exposes all 3 shifts (Morning, Afternoon, Night) to the UI.

## 4. Execution Steps

1.  **Prepare ManagementApp Components:**
    - Create `ShiftGroupControl.xaml` & `.cs`.
    - Implement Drop logic.
2.  **Refactor ManagementApp MainWindow:**
    - Modify the XAML to use the horizontal list of groups.
    - Add "Status Cards" list to the sidebar (Drag Source).
3.  **Wire up Events:**
    - Connect UI events to Controller methods.
4.  **Update DisplayApp:**
    - Modify `CreateGroupPanel` to use vertical rows.
    - Test rendering.
5.  **Verify:**
    - Test Drag & Drop for all 3 shifts.
    - Test Status Card assignment.
    - Test persistence.
