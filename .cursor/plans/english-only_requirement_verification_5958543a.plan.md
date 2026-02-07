---
name: English-Only Requirement Verification
overview: "Verification shows that requirement 20 (English-Only Software and Source Code) from the implementation plan / new requirements is not met: UI text and many string literals are in Persian; resources.xml holds Persian values; one file has a non-English name. Variable and class names are in English."
todos: []
isProject: false
---

# Verification: English-Only Software and Source Code (Requirement 20)

## Requirement (from [new_requirements.md](.cursor/plans/new_requirements.md))

- All UI text must be in **English**
- All variable names, class names, file names, and source code must be in **English**

## Summary: **Not implemented**

The codebase currently uses **Persian (Farsi)** for most UI text and many string literals. Variable and class names are in English.

---

## 1. UI text – not in English

### ManagementApp


| Location                                                                                             | Examples (Persian)                                                                                                                                                                                                                                                                                                           |
| ---------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [ManagementApp/Views/MainWindow.xaml](ManagementApp/Views/MainWindow.xaml)                           | Title "سیستم مدیریت شیفت کارمندان", Tab "مدیریت کارمندان", "لیست کارمندان", "جستجو...", "افزودن کارمند", "جزئیات کارمند", "مدیریت نقش‌ها", "مدیریت گروه‌های شیفت", "مدیریت کارت‌های وضعیت", "وارد کردن از CSV", etc.                                                                                                         |
| [ManagementApp/Controls/ShiftSlotControl.xaml](ManagementApp/Controls/ShiftSlotControl.xaml)         | Empty slot label: "خالی" (Empty)                                                                                                                                                                                                                                                                                             |
| [ManagementApp/Controls/ShiftGroupControl.xaml](ManagementApp/Controls/ShiftGroupControl.xaml)       | "سرپرست: {0}", "سرپرست: تعیین نشده", "کشیدن و رها کردن برای تعیین سرپرست", Shift titles "شیفت صبح", "شیفت عصر", "شیفت شب"                                                                                                                                                                                                    |
| [ManagementApp/Views/MainWindow.xaml.cs](ManagementApp/Views/MainWindow.xaml.cs)                     | Dozens of hardcoded Persian strings: "جستجو...", "سرپرست شیفت", "مدیریت نقش‌ها تکمیل شد", "خطا", "تأیید", "شیفت صبح پاک شد", "کارمندان غایب", "بازگرداندن به لیست اصلی", report headers "آمار کارمندان", "شیفت صبح (میانگین)", "گزارش تولید شده توسط سیستم مدیریت کارمندان", regex patterns for Persian report parsing, etc. |
| [ManagementApp/Controls/ShiftGroupControl.xaml.cs](ManagementApp/Controls/ShiftGroupControl.xaml.cs) | "خطا در تخصیص شیفت", "کارمند در شیفت ... مشغول است. آیا مایل به انتقال هستید؟"                                                                                                                                                                                                                                               |
| [ManagementApp/Controllers/MainController.cs](ManagementApp/Controllers/MainController.cs)           | "خطا در افزودن گروه شیفت", "خطا در بروزرسانی گروه شیفت", "گروه شیفت مورد نظر یافت نشد", "شیفت مورد نظر یافت نشد", "نمی‌توان کارمند را به این شیفت اضافه کرد", etc.                                                                                                                                                           |


### DisplayApp


| Location                                                 | Examples (Persian)                                                                                                                                                                                                                |
| -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [DisplayApp/MainWindow.xaml](DisplayApp/MainWindow.xaml) | "سیستم مدیریت کارکنان", "خروج از تمام صفحه", "بستن", "آخرین بروزرسانی", "بروزرسانی بعدی", "مدیران", "گروه‌های کاری", "نمودار عملکرد", "توصیه هوش مصنوعی", "در حال بارگذاری...", "مرخصی", "بیمار", "غایب", formula text in Persian |


### Shared model (display text)


| Location                                         | Issue                                                                         |
| ------------------------------------------------ | ----------------------------------------------------------------------------- |
| [Shared/Models/Shift.cs](Shared/Models/Shift.cs) | `DisplayName` returns Persian: "صبح", "عصر", "شب" for morning/afternoon/night |


---

## 2. Localization file – values are Persian

[SharedData/resources.xml](SharedData/resources.xml) exists and is structured for localization, but **all string values are in Persian** (e.g. `app_title` = "سیستم مدیریت شیفت کارمندان", `shift_morning` = "صبح", `label_search` = "جستجو..."). Requirement 20 requires UI text to be in English, so these values should be English.

`ResourceManager.GetString()` is used in only a few places (e.g. [ManagementApp/Views/MainWindow.xaml.cs](ManagementApp/Views/MainWindow.xaml.cs) for "خطا", "هشدار", "لطفاً یک کارمند انتخاب کنید"); most UI strings are still hardcoded in XAML and C#, and the fallbacks/keys used are Persian.

---

## 3. File names – one non-English

- [display_app_chart_usage.md](display_app_chart_usage.md) (formerly Persian filename in repo root; renamed for English-only compliance)

All other project file names (e.g. `MainWindow.xaml`, `ShiftSlotControl.xaml`, `MainController.cs`) are in English.

---

## 4. Variable names, class names, source code identifiers – in English

- No Persian variable, class, or method names were found. Identifiers are English (e.g. `EmployeeListBox`, `MainWindow`, `ShiftGroupControl`, `GetString`).

---

## 5. Data files (JSON reports, etc.)

- [SharedData/Reports/](SharedData/Reports/) and runtime JSON contain Persian in **user/content data** (employee names, role names, group names, absence categories). Requirement 20 focuses on “software and source code”; changing stored user data may be a separate decision (e.g. migration or display-only translation).

---

## What would be needed for compliance

1. **UI text in English**

- Replace all Persian UI strings in ManagementApp and DisplayApp (XAML and C#) with English text.
- Either: use `ResourceManager.GetString()` everywhere with keys, and set **English** values in `resources.xml`, or use English literals if not using resources for that string.

1. **resources.xml in English**

- Set all values in [SharedData/resources.xml](SharedData/resources.xml) to English (e.g. "Employee Shift Management System", "Morning", "Afternoon", "Night", "Search...", etc.).

1. **Shift display name**

- In [Shared/Models/Shift.cs](Shared/Models/Shift.cs), make `DisplayName` return English (e.g. "Morning", "Afternoon", "Night") or load from resources.

1. **Report generation and parsing**

- In [ManagementApp/Views/MainWindow.xaml.cs](ManagementApp/Views/MainWindow.xaml.cs), switch report headers and body text to English and update the report-parsing logic (e.g. lines ~5317–5402 that match Persian phrases like "کل کارمندان", "شیفت صبح") to match the new English strings.

1. **File name**

- ~~Rename `نحوه_کار_چارت_در_اپلیکیشن_نمایش.md` to an English name (e.g. `display_app_chart_usage.md`)~~ — **Done:** file renamed to `display_app_chart_usage.md`.

1. **Comments**

- Implementation plan Phase 5 also asks to “Update all comments to English”; a separate pass would be needed to find and translate any remaining non-English comments.

---

## Conclusion

**Requirement 20 (English-Only Software and Source Code) is not satisfied.** UI text and many string literals are in Persian, `resources.xml` holds Persian values, and one file has a non-English name. Identifiers (variables, classes, file names except one) are already in English. The changes above would bring the project into alignment with requirement 20.