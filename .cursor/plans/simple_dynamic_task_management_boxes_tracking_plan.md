# Simple and Dynamic Task Management - Box Tracking System

## Overview

This plan implements a simple daily task tracking system where managers can enter the number of completed "boxes" (tasks) for each shift group and shift. The system tracks daily and weekly progress, with targets of 100 boxes per day and 1,000 boxes per week for each shift. The system analyzes progress and indicates whether teams are ahead of schedule or behind.

## Current Implementation

- **Task Management**: Existing task system tracks individual tasks with details (title, description, priority, hours, etc.)
- **Shift Groups**: Each group has morning and evening shifts
- **No Daily Progress Tracking**: Currently no system to track daily completed task counts per shift

## Requirements

- **Daily Target**: 100 boxes (tasks) per day for each shift (morning and evening separately)
- **Weekly Target**: 1,000 boxes (tasks) per week for each shift (morning and evening separately)
- **Tracking Level**: Per shift group AND per shift (morning/evening tracked separately)
- **Input Method**: Manager enters completed boxes count each day
- **Analysis**: System calculates and displays if team is ahead or behind schedule

## Implementation Plan

### 1. Create Daily Task Progress Model

**File**: [`Shared/Models/DailyTaskProgress.cs`](Shared/Models/DailyTaskProgress.cs) (new file)

- Create new class `DailyTaskProgress`:
  ```csharp
  public class DailyTaskProgress
  {
      public string ProgressId { get; set; } = string.Empty; // Format: "{groupId}_{shiftType}_{date}"
      public string GroupId { get; set; } = string.Empty;
      public string ShiftType { get; set; } = string.Empty; // "morning" or "evening"
      public string Date { get; set; } = string.Empty; // Shamsi date in yyyy/MM/dd format
      public int CompletedBoxes { get; set; } = 0;
      public int DailyTarget { get; set; } = 100;
      public DateTime CreatedAt { get; set; }
      public DateTime UpdatedAt { get; set; }
  }
  ```

- Create `DailyTaskProgressManager` class:
  - Dictionary to store progress by ProgressId
  - Methods: `AddProgress`, `UpdateProgress`, `GetProgress`, `GetWeeklyProgress`, `GetProgressForGroupAndShift`
  - Calculate ahead/behind status
  - Serialization support (ToJson, FromJson)

### 2. Add Progress Tracking to MainController

**File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

- Add `DailyTaskProgressManager` property
- Add methods:
  - `RecordDailyProgress(string groupId, string shiftType, string date, int completedBoxes)`
  - `GetDailyProgress(string groupId, string shiftType, string date)`
  - `GetWeeklyProgress(string groupId, string shiftType, string weekStartDate)` - returns progress for 7 days
  - `CalculateProgressStatus(string groupId, string shiftType, string date)` - returns ahead/behind analysis
- Load/save progress data in `LoadData()` and `SaveData()`

### 3. Add Progress Status Calculation Logic

**File**: [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)

- Add method `CalculateProgressStatus()`:
  - Get daily progress for the date
  - Compare completed vs daily target (100)
  - Calculate percentage: `(completed / target) * 100`
  - Calculate absolute difference: `completed - target`
  - Determine status:
    - Ahead: completed > target (or percentage > 100%)
    - Behind: completed < target (or percentage < 100%)
    - On Track: completed == target (or percentage == 100%)
  - Return status object with percentage, absolute difference, and status text

- Add method `GetWeeklyProgressStatus()`:
  - Get progress for all 7 days of the week
  - Calculate cumulative completed boxes
  - Compare to weekly target (1000)
  - Calculate percentage and status

### 4. Add UI for Daily Progress Entry

**File**: [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)

- In Task Management tab, add new GroupBox "ثبت پیشرفت روزانه" (Daily Progress Entry):
  - ComboBox for shift group selection
  - RadioButtons or ComboBox for shift type (morning/evening)
  - DatePicker for date selection (default: today)
  - TextBox for completed boxes input
  - Button "ثبت" (Record)
  - Display area showing:
    - Daily target: 100
    - Completed: [entered value]
    - Status: Ahead/Behind/On Track
    - Percentage: X%
    - Difference: +X or -X boxes

### 5. Add Weekly Progress View

**File**: [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)

- Add GroupBox "پیشرفت هفتگی" (Weekly Progress):
  - ComboBox for shift group selection
  - ComboBox for shift type (morning/evening)
  - Display weekly summary:
    - Week start date
    - Total completed this week
    - Weekly target: 1,000
    - Status: Ahead/Behind/On Track
    - Percentage: X%
    - Daily breakdown (table showing each day's progress)

### 6. Implement Progress Entry Handler

**File**: [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Add `RecordDailyProgress_Click` handler:
  - Get selected group, shift type, date, and completed boxes
  - Validate input (must be non-negative integer)
  - Call `_controller.RecordDailyProgress()`
  - Refresh progress display
  - Show status message

- Add `LoadDailyProgress()` method:
  - Get current progress for selected group/shift/date
  - Update UI with progress data
  - Calculate and display status

- Add `LoadWeeklyProgress()` method:
  - Get weekly progress for selected group/shift
  - Calculate week start date (Saturday in Persian calendar)
  - Display daily breakdown and weekly summary

### 7. Add Progress Status Display

**File**: [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)

- Add method `DisplayProgressStatus()`:
  - Get status from controller
  - Format status text in Persian:
    - Ahead: "در حال پیشرفت ({Percentage}%) - {Difference} جعبه جلوتر"
    - Behind: "عقب افتاده ({Percentage}%) - {Difference} جعبه عقب‌تر"
    - On Track: "در مسیر ({Percentage}%)"
  - Set color coding:
    - Green for ahead/on track
    - Red for behind
  - Update status TextBlock

## Technical Details

### Progress ID Format

```
ProgressId = "{groupId}_{shiftType}_{date}"
Example: "default_morning_1404/06/30"
```

### Daily Status Calculation

```csharp
public class ProgressStatus
{
    public int Completed { get; set; }
    public int Target { get; set; }
    public double Percentage { get; set; }
    public int Difference { get; set; }
    public string StatusText { get; set; } // "در حال پیشرفت", "عقب افتاده", "در مسیر"
    public bool IsAhead { get; set; }
    public bool IsBehind { get; set; }
    public bool IsOnTrack { get; set; }
}
```

### Weekly Progress Calculation

- Week starts on Saturday (شنبه) in Persian calendar
- Calculate cumulative completed boxes for 7 days
- Compare to weekly target (1000)
- Show daily breakdown in table format

### Data Persistence

- Store progress data in JSON format
- Include in report data for synchronization
- Save to daily report file

## Files to Create

1. [`Shared/Models/DailyTaskProgress.cs`](Shared/Models/DailyTaskProgress.cs) - New model file

## Files to Modify

1. [`ManagementApp/Controllers/MainController.cs`](ManagementApp/Controllers/MainController.cs)
   - Add DailyTaskProgressManager
   - Add progress tracking methods
   - Add status calculation methods

2. [`ManagementApp/Views/MainWindow.xaml`](ManagementApp/Views/MainWindow.xaml)
   - Add daily progress entry UI in Task Management tab
   - Add weekly progress view

3. [`ManagementApp/Views/MainWindow.xaml.cs`](ManagementApp/Views/MainWindow.xaml.cs)
   - Add progress entry handlers
   - Add progress loading and display methods

## Testing Considerations

- Test entering daily progress for morning shift → should save and display status
- Test entering daily progress for evening shift → should save and display status
- Test with different groups → should track separately per group
- Test daily status calculation:
  - 120 boxes → should show "در حال پیشرفت (120%) - 20 جعبه جلوتر"
  - 80 boxes → should show "عقب افتاده (80%) - 20 جعبه عقب‌تر"
  - 100 boxes → should show "در مسیر (100%)"
- Test weekly progress calculation → should sum 7 days and compare to 1000
- Test data persistence → should save and load correctly
- Test with multiple days → should track historical data
- Test week boundary → should correctly identify week start (Saturday)
