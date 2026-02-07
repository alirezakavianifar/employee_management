# Implementation Plan: 3-Shift Layout with Status Cards and Labels

This plan is organized into 5 testable phases. After completing each phase, you can run and test the application before proceeding to the next phase.

## User Decisions

### Confirmed Design Decisions
- [x] Shift naming: "Evening" → "Afternoon" (3 shifts: Morning/Afternoon/Night)
- [x] Status cards are exclusive: A cell contains EITHER employee OR status card, not both
- [x] Label storage: Per-employee (in Employee model)
- [x] Localization: Using `resources.xml` file

### IMPORTANT: Breaking Changes
1. Data format will change to accommodate 3 shifts instead of 2.
2. Existing data files will be automatically migrated.
3. UI layout will change to table-based grid (3 rows × N columns).

---

## Phase 1: Extend Models for 3 Shifts

### Goal
Update data models to support 3 shifts (Morning, Afternoon, Night) with backward compatibility.

### Changes

#### `Shift.cs`
- Rename "evening" to "afternoon" in `ShiftType` and `DisplayName`.
- Add "night" shift support in `DisplayName` property.
- Update all switch statements to handle "afternoon" and "night".

#### `ShiftGroup.cs`
- Rename `EveningCapacity` → `AfternoonCapacity`.
- Rename `EveningShift` → `AfternoonShift`.
- Add `NightCapacity` and `NightShift` properties.
- Update constructors to initialize all 3 shifts.
- Update `GetShift()`, `GetEmployeeShifts()`, `GetTotalCapacity()`.
- Update serialization (`ToJson`, `FromJson`, `ToDictionary`) for 3 shifts.

#### `ShiftGroupService.cs`
- Update save/load to handle 3 shifts.
- Add migration logic: rename `EveningShift` → `AfternoonShift`, add empty `NightShift`.
- Ensure backward compatibility with existing data files.

### Testing Phase 1

#### Run & Test
- [ ] Build and run ManagementApp.
- [ ] Load existing data - verify no errors, data migrated correctly.
- [ ] Check that employees assigned to "evening" shift now appear in "afternoon".
- [ ] Manually inspect JSON data file - verify it now has `afternoon_shift` and `night_shift`.
- [ ] Try assigning employees to the new night shift programmatically (UI not updated yet).
- [ ] Save and reload - verify night shift assignments persist.

#### Pass Criteria
Application loads without errors, existing data migrates successfully, all 3 shifts are accessible in code.

---

## Phase 2: Add Status Cards

### Goal
Implement status cards that can be assigned to shift cells instead of employees.

### Changes

#### [NEW] `StatusCard.cs`
```csharp
public class StatusCard
{
    public string StatusCardId { get; set; }
    public string Name { get; set; }
    public string Color { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Serialization methods
}
```

#### [NEW] `StatusCardService.cs`
- `List<StatusCard> LoadStatusCards()`
- `void SaveStatusCards(List<StatusCard>)`
- `StatusCard CreateStatusCard(string name, string color)`
- `void DeleteStatusCard(string statusCardId)`

#### `Shift.cs`
- Add `public string StatusCardId { get; set; }` to each slot or overall shift.
- Update assignment logic: if `StatusCardId` is set, employee cannot be assigned (exclusive).
- Update serialization to include status card IDs.

#### [NEW] `StatusCardDialog.xaml`
- Simple dialog with Name input, Color picker, Save/Cancel buttons.

#### `MainWindow.xaml.cs`
- Add status card creation UI trigger (temporary button for testing).
- Add method to assign status card to a shift cell.
- Update rendering to show status card instead of employee when present.

### Testing Phase 2

#### Run & Test
- [ ] Build and run ManagementApp.
- [ ] Create a status card via the dialog (e.g., "Out of Order" with red color).
- [ ] Assign status card to a shift cell - verify employee assignment is blocked.
- [ ] Verify UI shows status card (name + color background).
- [ ] Remove status card - verify employee can now be assigned.
- [ ] Save and reload - verify status cards persist.
- [ ] Run DisplayApp - verify status cards render correctly.

#### Pass Criteria
Status cards can be created, assigned exclusively to cells, saved/loaded, and displayed in both apps.

---

## Phase 3: Add Employee Labels

### Goal
Implement label system where employees can have text labels attached.

### Changes

#### [NEW] `EmployeeLabel.cs`
```csharp
public class EmployeeLabel
{
    public string LabelId { get; set; }
    public string Text { get; set; }
    public DateTime CreatedAt { get; set; }
    // Serialization methods
}
```

#### `Employee.cs`
- Add `public List<EmployeeLabel> Labels { get; set; } = new();`
- Update serialization to include labels.
- Update `Update()` method to handle label modifications.

#### [NEW] `LabelService.cs`
- `List<EmployeeLabel> LoadLabelArchive()`
- `void SaveLabelArchive(List<EmployeeLabel>)`
- `EmployeeLabel CreateLabel(string text)`
- `void DeleteLabelFromArchive(string labelId)`

#### [NEW] `LabelCreationPanel.xaml`
- Text input + "Create Label" button.
- Scrollable list of archived labels.
- Drag source for labels.

#### `MainWindow.xaml.cs`
- Add label creation panel to UI.
- Implement drag & drop from label archive to employee cards.
- Implement right-click context menu on labels for removal.
- Update employee card rendering to show labels below photo.

### Testing Phase 3

#### Run & Test
- [ ] Build and run ManagementApp.
- [ ] Create labels (e.g., "555-1234", "Vehicle: ABC-123").
- [ ] Drag label from archive to employee card - verify it appears below photo.
- [ ] Right-click label on employee - select Remove - verify removed from employee.
- [ ] Verify label still exists in archive for reuse.
- [ ] Drag same label to different employee - verify creates new copy.
- [ ] Save and reload - verify employee labels persist.
- [ ] Run DisplayApp - verify labels render below employee photos.

#### Pass Criteria
Labels can be created, assigned to employees, removed, reused from archive, and displayed correctly.

---

## Phase 4: UI Restructure to Table Layout

### Goal
Redesign UI from current layout to 3-row × N-column table format.

### Changes

#### `MainWindow.xaml` (ManagementApp)
- Replace current shift grid with table: 3 rows (Morning/Afternoon/Night) × N columns (Groups).
- Display group name at top of each column.
- Each cell shows: EITHER employee (photo, name, ID, labels) OR status card.
- Empty cells show drag & drop placeholder.

#### `MainWindow.xaml.cs` (ManagementApp)
- Update rendering logic for table layout.
- Update drag & drop to work with table cells.
- Ensure all 3 shifts visible and functional.

#### `MainWindow.xaml` (DisplayApp)
- Update display layout to match table structure.
- Render all 3 shifts vertically per group.

#### `MainWindow.xaml.cs` (DisplayApp)
- Update data loading and rendering for table layout.

### Testing Phase 4

#### Run & Test
- [ ] Build and run both apps.
- [ ] Verify ManagementApp shows table with 3 rows × N columns.
- [ ] Assign employees to all 3 shifts across multiple groups.
- [ ] Verify only 1 employee per group per shift.
- [ ] Assign status cards to some cells - verify exclusive behavior.
- [ ] Add labels to employees - verify they display below photos.
- [ ] Run DisplayApp - verify table layout matches ManagementApp.
- [ ] Test all drag & drop operations in new layout.

#### Pass Criteria
Table layout works correctly, all features functional in new UI, DisplayApp matches ManagementApp.

---

## Phase 5: Implement Localization

### Goal
Centralize all UI strings in `resources.xml` for easy language switching.

### Changes

#### [NEW] `resources.xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<resources>
  <string key="shift_morning">Morning</string>
  <string key="shift_afternoon">Afternoon</string>
  <string key="shift_night">Night</string>
  <string key="group">Group</string>
  <string key="status_out_of_order">Out of Order</string>
  <string key="status_empty">Empty</string>
  <string key="status_available">Available</string>
  <string key="label_create">Create Label</string>
  <string key="label_archive">Label Archive</string>
  <!-- Extract ALL UI strings here -->
</resources>
```

#### [NEW] `ResourceManager.cs`
```csharp
public static class ResourceManager
{
    private static Dictionary<string, string> _strings;

    public static void LoadResources(string filePath)
    {
        // Parse resources.xml and populate _strings dictionary
    }

    public static string GetString(string key, string fallback = "")
    {
        return _strings.ContainsKey(key) ? _strings[key] : fallback;
    }
}
```

#### [MODIFY] All XAML and C# files in both apps
- Replace all hardcoded strings with `ResourceManager.GetString("key")`.
- Refactor Persian/non-English variable/class names to English.
- Update all comments to English.

### Testing Phase 5

#### Run & Test
- [ ] Build and run both apps.
- [ ] Verify all UI text loads from `resources.xml`.
- [ ] Edit `resources.xml` - change "Morning" to "AM Shift".
- [ ] Restart app - verify UI shows "AM Shift".
- [ ] Verify no hardcoded strings remain (visual inspection).
- [ ] Test all features still work with localized strings.

#### Pass Criteria
All UI strings load from `resources.xml`, changing file updates UI, no hardcoded strings.

---

## Final Verification

After completing all 5 phases:

### Full Integration Test
- [ ] Create groups, assign employees to all 3 shifts.
- [ ] Add status cards and labels.
- [ ] Save, close, reload - verify everything persists.
- [ ] Test ManagementApp and DisplayApp together.

### Data Migration Test
- [ ] Use backup of old 2-shift data.
- [ ] Load in new system - verify clean migration.
- [ ] Verify no data loss.

### Edge Cases
- [ ] Maximum capacity in all shifts.
- [ ] Empty shifts with only status cards.
- [ ] Employees with many labels.
- [ ] Special characters in labels.

**Final Pass Criteria:** All features work together seamlessly, data persists correctly, migration is clean, no regressions.

---

## Migration Notes

Each phase handles its own migration:

1. **Phase 1:** Auto-migrates `EveningShift` → `AfternoonShift`, adds empty `NightShift`.
2. **Phase 2:** Adds empty status cards list to existing data.
3. **Phase 3:** Adds empty labels list to employees.
4. **Phase 4:** No migration (UI only).
5. **Phase 5:** No migration (code refactoring only).

All migrations maintain backward compatibility and preserve existing data.
