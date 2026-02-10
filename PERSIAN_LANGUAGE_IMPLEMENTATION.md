# Persian Language Support - Implementation Summary

## Date: February 7, 2026

## Overview
Added Persian (Farsi) language support with runtime switching capability. Users can now choose between English and Persian in either app, and the change applies instantly to both ManagementApp and DisplayApp (when both are running).

## New Files Created

### 1. Resource Files
- **SharedData/resources.fa.xml** - Complete Persian translation with all 234+ string keys matching resources.xml

### 2. Shared Infrastructure
- **Shared/Utils/LanguageConfigHelper.cs** - Manages language.json config file in SharedData/Config/
- **Shared/Utils/ResourceBridge.cs** - Singleton with INotifyPropertyChanged for binding-driven UI updates

### 3. Converters
- **ManagementApp/Converters/ResKeyConverter.cs** - Converts resource keys to localized strings
- **DisplayApp/Converters/ResKeyConverter.cs** - Same for DisplayApp

## Modified Files

### Shared Project
- **Shared/Utils/ResourceManager.cs**
  - Added `LoadResourcesForLanguage(sharedDataDir, "en"|"fa")` method

### ManagementApp
- **App.xaml.cs**
  - Language loading at startup with SharedData path resolution
  - FileSystemWatcher on language.json for cross-app sync
  - ApplyFlowDirection() for RTL/LTR support
- **Views/MainWindow.xaml**
  - Added Language ComboBox in Settings (System Settings section)
- **Views/MainWindow.xaml.cs**
  - LanguageComboBox_SelectionChanged handler
  - PropertyChanged subscription for cross-app language updates
  - UpdateSettingsDisplay() syncs language combo
- **Converters/ResourceExtension.cs**
  - Now returns Binding instead of static string for live updates

### DisplayApp
- **App.xaml.cs**
  - Language loading at startup
  - FileSystemWatcher for cross-app sync
  - ApplyFlowDirection() for RTL/LTR
- **MainWindow.xaml**
  - Added Language ComboBox in header
- **MainWindow.xaml.cs**
  - LanguageComboBox_SelectionChanged handler
  - RefreshCodeBehindText() for LastUpdateText/CountdownText
  - PropertyChanged subscription
- **Converters/ResExtension.cs**
  - Now returns Binding for live updates

### Tests
- **SharedTests/ResourceManagerTests.cs**
  - Added `LoadResources_FromResourcesFa_LoadsPersianStrings()` test

## Resource Files Updated
- **SharedData/resources.xml** - Added language selector keys (label_language, language_english, language_persian)
- **SharedData/resources.fa.xml** - Complete Persian translations

## How It Works

### Language Storage
- Current language stored in: `SharedData/Config/language.json`
- Format: `{ "language": "en" }` or `{ "language": "fa" }`
- Default: "en" (English)

### Runtime Switching Flow
1. User selects language in ComboBox (either app)
2. Save to `language.json`
3. Reload resources from appropriate file
4. Set `ResourceBridge.Instance.CurrentLanguage`
5. Call `ResourceBridge.Instance.NotifyLanguageChanged()` to increment Version
6. Apply RTL (Persian) or LTR (English) flow direction
7. All XAML bindings refresh automatically via ResExtension → Binding → Version change
8. FileSystemWatcher in other app detects change and applies same steps

### RTL Support
- Persian (fa): FlowDirection.RightToLeft
- English (en): FlowDirection.LeftToRight
- Applied to MainWindow, propagates to all child controls

## Testing
- All 38 tests pass including new Persian resource loading test
- Test verifies: Persian "shift_morning" = "صبح"

## Deployment Status
- ✓ EmployeeManagementSystem_Source updated with all changes
- ✓ EmployeeManagementSystem_Deploy updated with compiled binaries
- ✓ resources.fa.xml present in both Deploy and Source SharedData folders
- ✓ SharedData/Config/ folder created for language.json

## Usage Instructions
1. Open either ManagementApp or DisplayApp
2. In ManagementApp: Go to Settings tab → Language dropdown
3. In DisplayApp: Header area → Language dropdown
4. Select "English" or "فارسی" (Persian)
5. UI updates instantly in current app
6. Other app (if running) updates within 1-2 seconds via file watcher

## Known Behavior
- First launch creates `SharedData/Config/language.json` with "en" if missing
- Code-behind text (status messages, timestamps) updates on language change
- Message boxes shown before switching show old language (new ones show new language)
- All XAML-bound text updates instantly via binding system
