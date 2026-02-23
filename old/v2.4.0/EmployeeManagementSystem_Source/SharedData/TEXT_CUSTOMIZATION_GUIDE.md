# Text Customization Guide for End Users

## Overview

This guide explains how to customize any text displayed in the Employee Management System applications. All user-facing texts can be changed by editing a single configuration file.

## Quick Start: Changing "Supervisor" to "Foreman"

1. **Locate the file**: Open `resources.xml` in the `SharedData` folder
2. **Find the key**: Search for `display_supervisor` (around line 173)
3. **Edit the value**: Change `Supervisor: {0}` to `Foreman: {0}`
4. **Save the file**
5. **Restart the applications** (both ManagementApp and DisplayApp)

## File Location

The resources file is located at:
- **Development**: `SharedData/resources.xml`
- **Deployment**: `EmployeeManagementSystem_Deploy/SharedData/resources.xml`

Both applications load texts from this file when they start.

## How to Change Any Text

### Step 1: Open resources.xml

Open `resources.xml` in any text editor:
- Notepad (Windows)
- Notepad++ (recommended)
- Visual Studio Code
- Any XML/text editor

### Step 2: Find the Text You Want to Change

Each text in the application has a unique "key". The file is organized into sections with comments. For example:

```xml
<!-- DisplayApp Specific -->
<string key="display_supervisor">Supervisor: {0}</string>
```

### Step 3: Edit the Value

Change only the text between the `<string>` tags. Keep the `key` attribute unchanged.

**Important**: If you see placeholders like `{0}`, `{1}`, etc., keep them in your text. They will be replaced with actual values by the application.

**Example:**
```xml
<!-- Before -->
<string key="display_supervisor">Supervisor: {0}</string>

<!-- After -->
<string key="display_supervisor">Foreman: {0}</string>
```

### Step 4: Save the File

Save the file with UTF-8 encoding to preserve special characters.

### Step 5: Restart Applications

Close and restart both ManagementApp and DisplayApp for changes to take effect.

## Common Customizations

### Changing Shift Names

Find these keys (around lines 7-9):
- `shift_morning` - Change "Morning" to your preferred name
- `shift_afternoon` - Change "Afternoon" to your preferred name
- `shift_night` - Change "Night" to your preferred name

### Changing Button Labels

All button texts start with `btn_`. Examples:
- `btn_add_employee` - "Add Employee" button
- `btn_save_changes` - "Save Changes" button
- `btn_delete_employee` - "Delete Employee" button

### Changing Messages

All messages start with `msg_`. Examples:
- `msg_save_success` - Success message after saving
- `msg_error` - Error message title
- `msg_confirm_delete` - Confirmation dialog text

### Changing Labels

All form labels start with `label_`. Examples:
- `label_name` - "Name:" label
- `label_shift_group` - "Shift Group:" label
- `label_description` - "Description:" label

## Complete Example: Supervisor to Foreman

To change all supervisor references to foreman, update these keys in `resources.xml`:

```xml
<!-- Main display text (line 173) -->
<string key="display_supervisor">Foreman: {0}</string>

<!-- No supervisor message (line 174) -->
<string key="display_no_supervisor">No Foreman</string>

<!-- Shift supervisor labels (lines 64-67) -->
<string key="label_shift_supervisors">Shift Foremen (required):</string>
<string key="label_morning_supervisor">Morning Shift Foreman (required):</string>
<string key="label_afternoon_supervisor">Afternoon Shift Foreman (required):</string>
<string key="label_night_supervisor">Night Shift Foreman (required):</string>

<!-- Supervisor assignment hint (line 257) -->
<string key="hint_drag_drop_supervisor">Drag and drop to assign foreman</string>

<!-- Not assigned message (line 258) -->
<string key="supervisor_not_assigned">Foreman: Not assigned</string>
```

## Finding Which Key Controls a Text

If you see text in the application and want to change it:

1. **Check the resources.xml file** - Browse through the sections and comments
2. **Search by keyword** - Use your editor's search function to find similar text
3. **Look at the key names** - Keys are descriptive (e.g., `btn_add_employee` for "Add Employee" button)

## Important Notes

### XML Format

Always maintain proper XML syntax:
- Each entry must be on a single line or properly formatted
- Keep the `key` attribute exactly as it is
- Only change the text between `<string>` and `</string>`

**Correct:**
```xml
<string key="display_supervisor">Foreman: {0}</string>
```

**Incorrect:**
```xml
<string key="display_supervisor">Foreman: {0}<string>  <!-- Missing closing tag -->
<string key="display_supervisor">Foreman: {0}</string>  <!-- Wrong key name -->
```

### Placeholders

Some strings contain placeholders that will be replaced with actual values:
- `{0}` - First value (e.g., employee name)
- `{1}` - Second value (e.g., employee last name)
- `{2}` - Third value, etc.

**Keep placeholders when editing:**
```xml
<!-- This is correct - keeps {0} placeholder -->
<string key="display_supervisor">Foreman: {0}</string>

<!-- This is wrong - removes placeholder -->
<string key="display_supervisor">Foreman</string>
```

### File Encoding

Always save the file as **UTF-8** encoding to preserve:
- Special characters (accents, umlauts, etc.)
- Non-English characters
- Emojis (if used)

### Backup Before Changes

**Always create a backup** of `resources.xml` before making changes:
1. Copy `resources.xml` to `resources.xml.backup`
2. Make your changes
3. Test the application
4. If something goes wrong, restore from backup

### Application Restart Required

Changes only take effect after:
1. Saving `resources.xml`
2. Closing both ManagementApp and DisplayApp completely
3. Restarting both applications

The ResourceManager loads the file only when the application starts.

## Troubleshooting

### Changes Not Appearing

1. **Did you save the file?** - Make sure you saved `resources.xml`
2. **Did you restart the applications?** - Close and reopen both apps
3. **Check file location** - Make sure you edited the correct `resources.xml` file
4. **Check XML syntax** - Make sure the file is valid XML (no syntax errors)

### Application Won't Start

If the application won't start after editing `resources.xml`:

1. **Check XML syntax** - The file must be valid XML
2. **Restore from backup** - Use your backup file
3. **Check for typos** - Make sure all tags are properly closed
4. **Check encoding** - Make sure file is saved as UTF-8

### Finding Syntax Errors

Use an XML validator or editor with XML validation:
- Notepad++ with XML Tools plugin
- Visual Studio Code with XML extension
- Online XML validators

## File Structure

The `resources.xml` file is organized into sections:

- **Application Title** - Main application title
- **Shift Names** - Morning, Afternoon, Night shift names
- **Tab Headers** - Names of tabs in the interface
- **Group Headers** - Section headers
- **Labels** - Form field labels
- **Buttons** - Button texts
- **Messages** - Dialog and status messages
- **DisplayApp Specific** - Texts specific to the display application

Each section has comments to help you find what you're looking for.

## Need Help?

If you need to find a specific text:
1. Look at the application interface
2. Note the section where it appears (tab, dialog, button, etc.)
3. Search `resources.xml` for similar keywords
4. Check the comments in the file for guidance

Remember: Always backup before making changes!
