---
name: English-Only Compliance Plan
overview: Step-by-step plan to fully implement Requirement 20 (English-Only Software and Source Code) by replacing all remaining Persian UI text, literals, report content, and one filename with English across ManagementApp, DisplayApp, and Shared.
todos: []
isProject: false
---

# Step-by-Step Plan: English-Only Compliance (Requirement 20)

## Scope

- **In scope:** [ManagementApp](ManagementApp), [DisplayApp](DisplayApp), [Shared](Shared), [SharedData](SharedData). Rename one repo-root file.
- **Out of scope (optional):** [EmployeeManagementSystem_Source](EmployeeManagementSystem_Source) is a separate copy; can be updated in a follow-up pass or left as-is.
- **Already done:** [Shared/Models/Shift.cs](Shared/Models/Shift.cs) `DisplayName` returns English; [SharedData/resources.xml](SharedData/resources.xml) values are English; [ManagementApp/Views/MainWindow.xaml](ManagementApp/Views/MainWindow.xaml) main UI is English.

---

## Step 1: Rename the Persian filename ✓

- ~~Rename `نحوه_کار_چارت_در_اپلیکیشن_نمایش.md` (repo root) to `display_app_chart_usage.md`.~~ **Done.**
- ~~Update any references to this file (docs, links) if they exist.~~ **Done** (verification plan updated).

---

## Step 2: Replace Persian in Shared layer

Shared code is used by both apps; fixing it first avoids duplication and ensures consistent English.

- **[Shared/Models/Absence.cs](Shared/Models/Absence.cs):** Replace Persian keys in the absence-type mapping (e.g. "مرخصی", "بیمار", "غایب") with English keys or use English-only display strings and keep internal/storage keys for backward compatibility if needed.
- **[Shared/Models/ShiftGroup.cs](Shared/Models/ShiftGroup.cs):** Change default `Name`/`Description` from "گروه جدید", "بدون توضیحات", "گروه پیش‌فرض" to English ("New group", "No description", "Default group").
- **[Shared/Models/Task.cs](Shared/Models/Task.cs):** Replace Persian in `JsonProperty` names and in `AssignedEmployeesDisplay` ("تخصیص داده نشده"). Prefer adding English display strings and keeping existing JSON keys for backward compatibility so existing saved data still deserializes.
- **[Shared/Models/RoleManager.cs](Shared/Models/RoleManager.cs):** Replace default role display names ("مدیر", "سرپرست", etc.) with English ("Manager", "Supervisor", "Employee", "Intern", "Contractor").
- **[Shared/Models/DailyTaskProgress.cs](Shared/Models/DailyTaskProgress.cs):** Replace Persian in default/display strings and comments (e.g. "در حال پیشرفت") with English.
- **[Shared/Services/JsonHandler.cs](Shared/Services/JsonHandler.cs):** Replace Persian in role checks and defaults ("مدیر", "کارگر") with English or neutral keys.
- **[Shared/Utils/AppConfigHelper.cs](Shared/Utils/AppConfigHelper.cs):** Replace all Persian error messages (e.g. "مسیر پوشه نمی‌تواند خالی باشد") with English.
- **[Shared/Utils/ShamsiDateHelper.cs](Shared/Utils/ShamsiDateHelper.cs):** Persian month/day names are for Jalali calendar output. For strict English-only UI, either expose English labels for UI or document as localization exception; if requirement is “all UI in English”, use English month/day names for any user-facing output.

---

## Step 3: Replace Persian in ManagementApp XAML

Update each view/control so every user-visible string is in English.

- **[ManagementApp/Views/MainWindow.xaml](ManagementApp/Views/MainWindow.xaml):** Any remaining Persian (e.g. in tooltips, watermarks, or secondary labels).
- **[ManagementApp/Controls/ShiftSlotControl.xaml](ManagementApp/Controls/ShiftSlotControl.xaml):** Empty-slot label "خالی" → "Empty".
- **[ManagementApp/Controls/ShiftGroupControl.xaml](ManagementApp/Controls/ShiftGroupControl.xaml):** "سرپرست: {0}", "سرپرست: تعیین نشده", drag-drop hint, shift titles → English.
- **[ManagementApp/Views/ShiftGroupEditDialog.xaml](ManagementApp/Views/ShiftGroupEditDialog.xaml):** Labels (e.g. "سرپرست شیفت صبح (الزامی):") and button "تأیید" → English.
- **[ManagementApp/Views/ShiftGroupDialog.xaml](ManagementApp/Views/ShiftGroupDialog.xaml):** All Persian labels and buttons.
- **[ManagementApp/Views/StatusCardDialog.xaml](ManagementApp/Views/StatusCardDialog.xaml)** and **[StatusCardEditDialog.xaml](ManagementApp/Views/StatusCardEditDialog.xaml):** "جستجو در کارت‌ها...", "تأیید", etc. → English.
- **[ManagementApp/Views/EmployeeDialog.xaml](ManagementApp/Views/EmployeeDialog.xaml)**, **[RoleDialog.xaml](ManagementApp/Views/RoleDialog.xaml)**, **[RoleEditDialog.xaml](ManagementApp/Views/RoleEditDialog.xaml)**, **[TaskDialog.xaml](ManagementApp/Views/TaskDialog.xaml)**, **[ColorPalettePopup.xaml](ManagementApp/Views/ColorPalettePopup.xaml)**, **[NavigationMenu.xaml](ManagementApp/Controls/NavigationMenu.xaml):** Replace every Persian string with English.

---

## Step 4: Replace Persian in ManagementApp C#

Replace all hardcoded Persian strings in code-behind and services: messages, placeholders, status text, and report-related strings that are not handled in Step 5.

- **[ManagementApp/App.xaml.cs](ManagementApp/App.xaml.cs):** Error/window messages ("خطا در ایجاد پنجره اصلی", "خطا", etc.) → English.
- **[ManagementApp/Controllers/MainController.cs](ManagementApp/Controllers/MainController.cs):** All error and status messages → English.
- **[ManagementApp/Views/MainWindow.xaml.cs](ManagementApp/Views/MainWindow.xaml.cs):** Placeholders ("جستجو..."), status text, message boxes, and any report strings not covered in Step 5 → English. Leave report header/body and parsing to Step 5.
- **[ManagementApp/Views/ShiftGroupDialog.xaml.cs](ManagementApp/Views/ShiftGroupDialog.xaml.cs)** and **[ShiftGroupEditDialog.xaml.cs](ManagementApp/Views/ShiftGroupEditDialog.xaml.cs):** Messages ("جستجو در گروه‌ها...", "خطا در بارگذاری گروه‌های شیفت", etc.) → English.
- **[ManagementApp/Views/StatusCardDialog.xaml.cs](ManagementApp/Views/StatusCardDialog.xaml.cs)** and **[StatusCardEditDialog.xaml.cs](ManagementApp/Views/StatusCardEditDialog.xaml.cs):** All MessageBox and placeholder text → English.
- **[ManagementApp/Views/EmployeeDialog.xaml.cs](ManagementApp/Views/EmployeeDialog.xaml.cs)**, **[RoleDialog.xaml.cs](ManagementApp/Views/RoleDialog.xaml.cs)**, **[RoleEditDialog.xaml.cs](ManagementApp/Views/RoleEditDialog.xaml.cs)**, **[TaskDialog.xaml.cs](ManagementApp/Views/TaskDialog.xaml.cs):** All Persian strings → English.
- **[ManagementApp/Controls/ShiftGroupControl.xaml.cs](ManagementApp/Controls/ShiftGroupControl.xaml.cs)** and **[ShiftSlotControl.xaml.cs](ManagementApp/Controls/ShiftSlotControl.xaml.cs):** Error and UI strings → English.
- **[ManagementApp/Services/ShiftGroupService.cs](ManagementApp/Services/ShiftGroupService.cs):** Validation and error messages (e.g. "ظرفیت شیفت صبح باید حداقل 1 باشد", "خطا در محاسبه پیشنهاد") → English.

---

## Step 5: Report generation and parsing (English)

Reports must be generated in English and parsing must match the new text.

- **[ManagementApp/Services/PdfReportService.cs](ManagementApp/Services/PdfReportService.cs):**
  - Replace all Persian report content: titles ("سیستم مدیریت کارمندان"), section headers ("آمار کارمندان", "کل کارمندان"), shift labels ("شیفت صبح (میانگین)", "حداکثر شیفت صبح"), footer text, and any bullet text (e.g. "تمام داده‌ها از سیستم مدیریت کارمندان استخراج شده‌اند") with English equivalents.
  - Update parsing logic that matches Persian phrases (e.g. `cleanLine.Contains("کل کارمندان")`, "شیفت صبح", "حداکثر", "تعداد کل کارمندان:") to match the new English strings (e.g. "Total employees", "Morning shift", "Max", "Total employees:").
- **[ManagementApp/Views/MainWindow.xaml.cs](ManagementApp/Views/MainWindow.xaml.cs):**
  - Replace any remaining report header/body strings (e.g. "گزارش تولید شده توسط سیستم مدیریت کارمندان") with English.
  - Locate and update report-parsing blocks (e.g. around lines ~5317–5402 if still present) that use Persian phrases; switch to the same English phrases used in PdfReportService so parsing stays in sync.

---

## Step 6: Replace Persian in DisplayApp

- **[DisplayApp/MainWindow.xaml](DisplayApp/MainWindow.xaml):** Replace "آخرین بروزرسانی" and any other Persian (e.g. in tooltips) with English ("Last update", etc.).
- **[DisplayApp/MainWindow.xaml.cs](DisplayApp/MainWindow.xaml.cs):** All UI and error strings (e.g. "سرپرست", "شیفت صبح", "خطا در دریافت توصیه", "نمودار عملکرد - خطا در بارگذاری") → English.
- **[DisplayApp/App.xaml.cs](DisplayApp/App.xaml.cs):** Exception/error messages ("خطا در رابط کاربری", "خطا") → English.
- **[DisplayApp/Services/AIService.cs](DisplayApp/Services/AIService.cs):** All returned or displayed messages (e.g. "خطا در داده‌ها", "سیستم خالی است", "افزودن کارمند و تعریف کار توصیه می‌شود") → English.
- **[DisplayApp/Services/DataService.cs](DisplayApp/Services/DataService.cs)** and **[ChartService.cs](DisplayApp/Services/ChartService.cs):** Any user-facing or log Persian strings → English.
- **[DisplayApp/Models/GroupDisplayModel.cs](DisplayApp/Models/GroupDisplayModel.cs)** and **[DisplayApp/Converters/ImagePathConverter.cs](DisplayApp/Converters/ImagePathConverter.cs)** / **[ValidationHelper.cs](DisplayApp/Utils/ValidationHelper.cs):** Replace Persian with English where applicable.

---

## Step 7: Optional — Use resources for common strings

- Add any missing keys to [SharedData/resources.xml](SharedData/resources.xml) (e.g. "Error", "Warning", "Confirm", common button labels) and ensure values are English.
- Where it reduces duplication, replace repeated English literals in ManagementApp/DisplayApp with `ResourceManager.GetString(key)` so future localization stays in one place. This step is optional for “English-only” compliance but improves maintainability.

---

## Step 8: Optional — Comments in English

- Scan ManagementApp, DisplayApp, and Shared for non-English comments and translate to English. This aligns with the verification plan’s “Update all comments to English” note.

---

## Step 9: Verification

- Run a project-wide search for Persian character ranges (e.g. `[\u0600-\u06FF]`) in `.xaml`, `.cs`, and `resources.xml` under ManagementApp, DisplayApp, Shared, and SharedData.
- Confirm the only remaining matches are either intentional (e.g. stored user content in JSON, not UI) or documented exceptions (e.g. ShamsiDateHelper if kept as-is).
- Build ManagementApp and DisplayApp; do a quick UI and report flow test to ensure no broken or missing strings.

---

## Execution order summary


| Step | Focus                                     |
| ---- | ----------------------------------------- |
| 1    | Rename Persian filename                   |
| 2    | Shared (models, services, utils)          |
| 3    | ManagementApp XAML (views + controls)     |
| 4    | ManagementApp C# (excluding report logic) |
| 5    | Report generation + parsing               |
| 6    | DisplayApp (XAML + C# + services)         |
| 7    | Optional: resources consolidation         |
| 8    | Optional: comments                        |
| 9    | Verification                              |


Dependencies: Step 2 before 3/4/6 (Shared is referenced by both apps). Steps 3 and 4 can be interleaved per file. Step 5 must use the same English phrases in both PdfReportService and MainWindow report parsing.