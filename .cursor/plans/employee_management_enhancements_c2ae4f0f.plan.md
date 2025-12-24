---
name: Employee Management Enhancements
overview: "Implement 8 major features across Management App and Display App: colored shields, stickers, medals/badges, folder structure, drag & drop, shift/team leader management, and settings optimization."
todos:
  - id: extend_employee_model
    content: Extend Employee model with ShieldColor, StickerPaths, MedalBadgePath, and PersonnelId properties
    status: completed
  - id: shield_color_management
    content: Add shield color selection to EmployeeDialog in Management App
    status: completed
    dependencies:
      - extend_employee_model
  - id: shield_color_display
    content: Render colored shield overlay in Display App employee cards
    status: completed
    dependencies:
      - extend_employee_model
  - id: stickers_management
    content: Add sticker management UI to EmployeeDialog (add/remove stickers, PNG file picker)
    status: completed
    dependencies:
      - extend_employee_model
  - id: stickers_display
    content: Display stickers vertically on right side of photo in Display App
    status: completed
    dependencies:
      - extend_employee_model
  - id: medals_management
    content: Add medal/badge file picker to EmployeeDialog in Management App
    status: completed
    dependencies:
      - extend_employee_model
  - id: medals_display
    content: Display medal/badge above photo on right side in Display App
    status: completed
    dependencies:
      - extend_employee_model
  - id: folder_structure
    content: Implement Workers/FirstName_LastName/ folder structure with auto-creation and name detection
    status: completed
    dependencies:
      - extend_employee_model
  - id: personnel_id_display
    content: Display personnel ID after employee name in both apps
    status: completed
    dependencies:
      - extend_employee_model
  - id: drag_drop_photos
    content: Implement drag & drop for photos from Windows Explorer to employee cards/folders
    status: completed
    dependencies:
      - folder_structure
  - id: drag_drop_employees
    content: Implement drag & drop for employee cards in shift management view
    status: completed
  - id: team_leader_model
    content: Add TeamLeaderId property to Shift class for morning/evening shifts
    status: completed
  - id: team_leader_ui
    content: Add team leader selection UI in shift management section
    status: completed
    dependencies:
      - team_leader_model
  - id: shift_swap
    content: Implement one-click shift swap functionality (morning ↔ evening)
    status: completed
    dependencies:
      - team_leader_model
  - id: auto_rotation
    content: Implement automatic shift rotation (e.g., every Saturday) with background scheduler
    status: completed
    dependencies:
      - shift_swap
  - id: settings_reorganization
    content: Reorganize settings menu into logical categories with improved navigation
    status: completed
---

# Employee Man

agement System Enhancements

## Overview

This plan implements 8 major features to enhance the employee management and display system. Features are split between the Management App (for configuration) and Display App (for visualization).

## Feature Distribution

### Management App Features

- **Feature 2**: Configurable Colored Shield (settings/edit)
- **Feature 3**: Stickers on Worker's Photo (settings/edit)
- **Feature 4**: PNG Files (Medals/Badges) (settings/edit)
- **Feature 5**: Dedicated Folder Structure (automatic)
- **Feature 6**: Drag & Drop for Workers/Photos
- **Feature 7**: Shift and Team Leader Management (automatic rotation)
- **Feature 8**: Settings Menu Optimization

### Display App Features

- **Feature 2**: Display Colored Shield (read-only)
- **Feature 3**: Display Stickers (read-only)
- **Feature 4**: Display Medals/Badges (read-only)
- **Feature 5**: Display Personnel ID after name (read-only)

---

## Implementation Details

### 1. Employee Model Extensions

**File**: [`Shared/Models/Employee.cs`](Shared/Models/Employee.cs)Add new properties to the `Employee` class:

- `ShieldColor` (string): "Red", "Blue", "Yellow", "Black" (default: "Blue")
- `StickerPaths` (List<string>): List of PNG file paths for stickers
- `MedalBadgePath` (string): Path to medal/badge PNG file
- `PersonnelId` (string): Personnel ID to display after name

Update `ToDictionary()` and `Update()` methods to include these fields.---

### 2. Configurable Colored Shield (Feature 2)

**Files**:

- [`ManagementApp/Views/EmployeeDialog.xaml`](ManagementApp/Views/EmployeeDialog.xaml)
- [`ManagementApp/Views/EmployeeDialog.xaml.cs`](ManagementApp/Views/EmployeeDialog.xaml.cs)

**Changes**:

- Add ComboBox in EmployeeDialog for shield color selection (Red, Blue, Yellow, Black)
- Store selected color in Employee model
- Update EmployeeDialog to load/save shield color

**Display App**:

- [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs) - Update `CreateEmployeeCard()` to render colored shield overlay based on employee's `ShieldColor`
- Create semi-transparent shield frame using WPF Border/Ellipse with appropriate color

---

### 3. Stickers on Worker's Photo (Feature 3)

**Files**:

- [`ManagementApp/Views/EmployeeDialog.xaml`](ManagementApp/Views/EmployeeDialog.xaml)
- [`ManagementApp/Views/EmployeeDialog.xaml.cs`](ManagementApp/Views/EmployeeDialog.xaml.cs)

**Changes**:

- Add sticker management section in EmployeeDialog
- Add "Add Sticker" button that opens file dialog (PNG only)
- Display list of current stickers with remove option
- Store sticker paths in `Employee.StickerPaths`
- Create sticker storage folder: `Data/Stickers/` (shared across employees)

**Display App**:

- [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs) - Update `CreateEmployeeCard()` to display stickers vertically on the right side of the photo using a StackPanel

---

### 4. PNG Files (Medals/Badges) (Feature 4)

**Files**:

- [`ManagementApp/Views/EmployeeDialog.xaml`](ManagementApp/Views/EmployeeDialog.xaml)
- [`ManagementApp/Views/EmployeeDialog.xaml.cs`](ManagementApp/Views/EmployeeDialog.xaml.cs)

**Changes**:

- Add "Medal/Badge" section in EmployeeDialog
- Add file picker for PNG medal/badge image
- Store path in `Employee.MedalBadgePath`
- Create storage folder: `Data/Medals/`

**Display App**:

- [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs) - Update `CreateEmployeeCard()` to display medal/badge above the photo, on the right side, with sufficient spacing

---

### 5. Dedicated Folder Structure (Feature 5)

**Files**:

- [`Shared/Services/JsonHandler.cs`](Shared/Services/JsonHandler.cs)
- [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)
- [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

**Changes**:

- Create folder structure: `Data/Workers/{FirstName}_{LastName}/`
- When adding/editing employee, automatically create folder if it doesn't exist
- When selecting employee photo, detect worker name from folder name if image is in worker's folder
- Auto-populate employee name from folder name during photo selection
- Display personnel ID after worker's name: `{FullName} {PersonnelId}`

**Display App**:

- [`DisplayApp/MainWindow.xaml.cs`](DisplayApp/MainWindow.xaml.cs) - Update name display to show `{FullName} {PersonnelId}` format

---

### 6. Drag & Drop for Workers/Photos (Feature 6)

**Files**:

- [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)
- [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

**Changes**:

- Enable drag & drop on employee cards in shift view
- Enable drag & drop for photo files from Windows Explorer to employee cards or worker folders
- Visual feedback during drag (highlight target folder/section)
- After drop: add worker to folder or update employee photo
- Immediately display updated card in main view

**Implementation**:

- Use WPF `DragDrop.DoDragDrop()` and `AllowDrop` properties
- Handle `PreviewDragOver` and `Drop` events
- Create visual highlight effect for drop targets

---

### 7. Shift and Team Leader Management (Feature 7)

**Files**:

- [`Shared/Models/Shift.cs`](Shared/Models/Shift.cs)
- [`Shared/Models/ShiftGroup.cs`](Shared/Models/ShiftGroup.cs)
- [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)
- [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)

**Changes**:

- Add `TeamLeaderId` property to `Shift` class (morning and evening can have different leaders)
- Add team leader selection UI in shift management section
- Add "Swap Shifts" button to switch morning/evening shifts with one click
- Add automatic shift rotation settings:
- Checkbox: "Automatically swap shifts every Saturday"
- Store rotation schedule in Settings
- Implement automatic rotation logic in MainController
- Create background task/service to check rotation schedule daily

**Display App**:

- Display team leader information for each shift (read-only)

---

### 8. Settings Menu Optimization (Feature 8)

**Files**:

- [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml) (Settings tab)
- [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

**Changes**:

- Reorganize settings into logical categories:
- **General Settings**: Data directory, sync interval
- **Shift Settings**: Capacity, rotation schedule
- **Display Settings**: UI preferences
- **System Information**: Status, logs, reports
- Group related settings together visually
- Add drag & drop for reordering settings categories (optional, if time permits)
- Simplify navigation with collapsible sections or tabs within settings
- Improve visual hierarchy and spacing

---

## Data Flow

```javascript
Management App:
  EmployeeDialog → Employee Model → MainController → JSON Reports → Data/Reports/

Display App:
  Data/Reports/ → DataService → MainWindow → Employee Cards (with shields/stickers/medals)
```



## Folder Structure

```javascript
Data/
  Workers/
    FirstName_LastName/
      FirstName_LastName_001.jpg
      FirstName_LastName_002.jpg
  Stickers/
    sticker1.png
    sticker2.png
  Medals/
    medal1.png
    badge1.png
  Reports/
    report_YYYY-MM-DD.json
```



## Testing Considerations

- Test shield color rendering in both apps
- Test sticker display (vertical list on right side)
- Test medal/badge positioning (above photo, right side)
- Test folder auto-creation and name detection
- Test drag & drop functionality
- Test shift rotation automation
- Test settings menu navigation

## Migration Notes

- Existing employees will get default shield color (Blue)