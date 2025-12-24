---
name: Menu and Settings Optimization
overview: Reorganize menus and settings with categorical organization, side navigation menu, and drag-and-drop support for both settings sections and individual items to improve UX.
todos:
  - id: create_settings_models
    content: Create SettingsCategory and SettingsItem model classes to represent categorized settings structure
    status: completed
  - id: create_navigation_menu
    content: Create NavigationMenu control with categorized menu structure (Management, Reports, Settings)
    status: completed
  - id: refactor_settings_ui
    content: Reorganize Settings tab in MainWindow.xaml into categorized sections (Data & Storage, Shift Configuration, Synchronization, Appearance, System Info)
    status: completed
    dependencies:
      - create_settings_models
  - id: update_settings_display_logic
    content: Update UpdateSettingsDisplay() and related methods in MainWindow.xaml.cs to work with categorized settings structure
    status: completed
    dependencies:
      - refactor_settings_ui
  - id: implement_section_dragdrop
    content: Implement drag-and-drop functionality for reordering settings sections/categories
    status: completed
    dependencies:
      - refactor_settings_ui
  - id: implement_item_dragdrop
    content: Implement drag-and-drop functionality for reordering individual settings items within categories
    status: completed
    dependencies:
      - implement_section_dragdrop
  - id: persist_settings_order
    content: Add persistence for settings category and item order in MainController.Settings dictionary
    status: completed
    dependencies:
      - implement_section_dragdrop
      - implement_item_dragdrop
  - id: integrate_navigation_menu
    content: Integrate side navigation menu with MainWindow, update layout to accommodate side menu, connect navigation to tab visibility
    status: completed
    dependencies:
      - create_navigation_menu
  - id: add_missing_settings_ui
    content: Add UI controls for currently missing settings (badge_template_path, auto_rotate settings) in appropriate categories
    status: completed
    dependencies:
      - refactor_settings_ui
  - id: enhance_ui_visual_feedback
    content: "Add visual enhancements: icons, drag handles, hover effects, collapsible sections, tooltips"
    status: completed
    dependencies:
      - implement_section_dragdrop
      - implement_item_dragdrop
---

# Menu and Settings Optimization Plan

## Current State Analysis

The current application uses a TabControl-based navigation with the following structure:

- **Main Tabs**: Employee Management, Shift Management, Task Management, Reports, Settings
- **Settings Tab**: Contains all settings in a vertical scrollable layout with GroupBoxes:
- General Settings (data directory, sync settings)
- Shift Settings (default capacity)
- System Information (sync status, logs, reports)

**Issues identified:**

1. Settings are in a flat, vertically-scrolled layout without clear categorization
2. Related settings are scattered (e.g., shift-related settings split between tabs and settings)
3. No drag-and-drop support for customization
4. Missing settings in UI (badge_template_path, auto_rotate settings only partially visible)
5. No visual organization by category or relationship

## Proposed Solution

### 1. Side Navigation Menu Structure

Create a left-side navigation panel with categorized menu items:

```javascript
📁 Management
  ├─ 👥 Employees
  ├─ 🕐 Shifts
  └─ ✅ Tasks

📊 Reports
  └─ 📈 Report Generator

⚙️ Settings
  ├─ 📂 Data & Storage
  ├─ 🕐 Shift Configuration
  ├─ 🔄 Synchronization
  ├─ 🎨 Appearance & Display
  └─ ℹ️ System Information
```

**Implementation:**

- Create a new `NavigationMenu` control or section in [MainWindow.xaml](ManagementApp/Views/MainWindow.xaml)
- Use TreeView or ListView with grouping for categories
- Maintain existing TabControl content but control visibility based on navigation selection
- Store navigation preferences (expanded categories, selected items) in settings

### 2. Settings Reorganization by Category

Reorganize settings into logical categories with better grouping:**Data & Storage:**

- Data directory path
- Copy existing data option
- Badge template path

**Shift Configuration:**

- Default shift capacity
- Morning capacity
- Evening capacity
- Auto-rotate shifts (currently hidden)
- Auto-rotate day

**Synchronization:**

- Sync interval
- Sync enabled/disabled
- Sync status display

**Appearance & Display:**

- Selected display group
- UI preferences (future extensibility)

**System Information:**

- Sync status
- Last update time
- Report files list
- System logs

**Implementation:**

- Refactor [MainWindow.xaml](ManagementApp/Views/MainWindow.xaml) Settings tab to use categorized sections
- Update [MainWindow.xaml.cs](ManagementApp/Views/MainWindow.xaml.cs) `UpdateSettingsDisplay()` to organize by category
- Create a `SettingsCategory` model/class to manage settings metadata

### 3. Drag & Drop Support

Implement drag-and-drop for reordering:**Settings Sections:**

- Allow users to reorder categories (Data & Storage, Shift Configuration, etc.)
- Persist order in user preferences/settings
- Visual feedback during drag (highlighting drop zones)

**Individual Settings Items:**

- Allow reordering within each category
- Store custom order per category
- Provide "Reset to default" option

**Implementation:**

- Leverage existing drag-and-drop patterns from employee assignment (see `Employee_DragOver`, `EmployeeItem_MouseMove` in [MainWindow.xaml.cs](ManagementApp/Views/MainWindow.xaml.cs))
- Create reusable drag-and-drop behaviors for settings items
- Use `DragDrop.DoDragDrop` with custom `DataObject` types for settings
- Store order preferences in `MainController.Settings` dictionary

### 4. Settings Data Model Enhancement

Enhance the settings storage to support:

- Category metadata
- Display order
- Visibility preferences
- Grouping relationships

**Implementation:**

- Create `SettingsCategory` class in new `ManagementApp/Models/SettingsCategory.cs`
- Create `SettingsItem` class in new `ManagementApp/Models/SettingsItem.cs`
- Update [MainController.cs](ManagementApp/Controllers/MainController.cs) to manage categorized settings
- Add methods to save/load settings order and category organization

### 5. UI Improvements

**Visual Enhancements:**

- Use icons for categories (📂, 🕐, 🔄, etc.)
- Collapsible category sections (Expander controls)
- Clear visual separation between categories
- Hover effects and drag indicators
- "Drag handle" icons on draggable items

**UX Improvements:**

- Tooltips explaining each setting
- Quick search/filter for settings
- "Reset category to default" buttons
- Visual feedback when settings are changed but not saved

## Implementation Files

### New Files to Create:

1. `ManagementApp/Models/SettingsCategory.cs` - Settings category model
2. `ManagementApp/Models/SettingsItem.cs` - Individual setting item model
3. `ManagementApp/Controls/SettingsPanel.xaml` - Reusable settings panel control
4. `ManagementApp/Controls/NavigationMenu.xaml` - Side navigation menu control
5. `ManagementApp/Behaviors/DragDropBehavior.cs` - Reusable drag-and-drop behavior (optional)

### Files to Modify:

1. [ManagementApp/Views/MainWindow.xaml](ManagementApp/Views/MainWindow.xaml) - Add side navigation, reorganize settings UI
2. [ManagementApp/Views/MainWindow.xaml.cs](ManagementApp/Views/MainWindow.xaml.cs) - Update settings display logic, add drag-and-drop handlers
3. [ManagementApp/Controllers/MainController.cs](ManagementApp/Controllers/MainController.cs) - Add settings category management, order persistence

## Technical Considerations

- **Persistence**: Store navigation order and settings order in `Settings` dictionary with keys like `"settings_category_order"` and `"settings_item_order_{category}"`
- **Backward Compatibility**: Ensure existing settings continue to work during migration
- **Performance**: Lazy-load settings sections if needed for large settings lists
- **Accessibility**: Ensure keyboard navigation works for drag-and-drop alternative
- **RTL Support**: Ensure the side menu works correctly with RTL layout (currently using `FlowDirection="LeftToRight"`)

## Migration Strategy

1. **Phase 1**: Add side navigation menu, keeping existing tab structure visible
2. **Phase 2**: Reorganize settings into categories within existing Settings tab
3. **Phase 3**: Add drag-and-drop support for sections
4. **Phase 4**: Add drag-and-drop support for individual items
5. **Phase 5**: Hide/show tabs based on navigation selection (optional enhancement)

## Testing Considerations

- Test drag-and-drop with mouse and touch input
- Verify settings persistence after reordering
- Test with RTL layout