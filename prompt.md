You are an expert C# developer. Your task is to create the **Display App (Dashboard)** for an Employee Shift Management System. The **Management App** has already been implemented and is located at `D:\projects\New folder (8)\ManagementApp`. 

The system uses a **serverless** architecture with a **shared folder (e.g., OneDrive)** to store all data in **JSON files**.

-----

## **1. Core Architecture & Technology Stack**

  * **Language:** C#
  * **UI Framework:** WPF (Windows Presentation Foundation)
  * **Data Storage:** JSON files only. No databases.
  * **Sync Mechanism:** Display App monitors the shared folder for changes. Data should be refreshed every 30 seconds.
  * **Data Source:** The Management App creates JSON report files that the Display App reads.
  * **Dependencies:** Use the following NuGet packages:
      * `LiveCharts.Wpf` or `OxyPlot.Wpf` for charting.
      * `System.IO.FileSystemWatcher` for monitoring the shared folder.
      * `.NET 8.0` for packaging and deployment.
      * **Shared Project**: Reference the existing Shared class library for data models and services.

-----

## **2. Management App (Already Implemented)**

The Management App has already been implemented at `D:\projects\New folder (8)\ManagementApp` and includes:

  * **Employee Management:** Full CRUD operations for employees with photo uploads
  * **Shift Management:** Visual drag-and-drop interface for Morning and Evening shifts
  * **Absence Management:** Three categories: **"مرخصی"**, **"بیمار"**, **"غایب"**
  * **Settings:** Shared folder configuration, shift capacity, and manager definitions
  * **Data Export:** PNG, PDF, and CSV export functionality
  * **JSON Reports:** Creates daily report files in `report_YYYY-MM-DD.json` format

The Management App creates JSON report files that the Display App will read and display.

-----

## **3. Display App (Dashboard) - TO BE IMPLEMENTED**

This is a **full-screen, read-only** dashboard application that you need to create. It must work in **offline mode**, loading the last valid JSON file if the shared folder is inaccessible.

### **Dashboard Layout:**

  * **Managers Panel (Top-Left):** Display the 3 configured managers with their photos in equally-sized cards.
  * **Shifts Panel:** Show Morning and Evening shifts in two distinct rows. Display only the **first name and photo** of each employee.
  * **Absence Panel:** Display the counts for the three absence categories with their exact Persian labels: **مرخصی**, **بیمار**, **غایب**.
  * **Performance Chart:**
      * Display a line chart using `LiveCharts.Wpf` or `OxyPlot.Wpf`.
      * The chart must have the following exact Persian titles and labels:
          * Title: "**افزایش عملکرد — هفتهٔ N**" (where N is the week number)
          * X-axis: "**هفته‌ها**"
          * Y-axis: "**واحدها**"
  * **AI Assistant Panel:**
      * Display a short, rule-based recommendation in Persian. Implement these two rules:
        1.  If next week’s orders are less than or equal to capacity, show: "**تا ۳ نفر می‌توانند به مرخصی بروند**".
        2.  If two weeks of orders are 15% or more over capacity, show: "**اضافه کاری باید برنامه ریزی شود**".

-----

## **4. Data Model & File Handling**

  * The Management App creates daily data files named `report_YYYY-MM-DD.json` in the shared folder.
  * Employee photos are stored in an `images/staff/` subfolder by the Management App.
  * The Display App must handle errors gracefully (e.g., missing JSON, missing images) by showing a **Persian error dialog** using WPF MessageBox without crashing.
  * Log all operations and errors to a `logs/` folder using .NET logging frameworks like Serilog or NLog.

-----

## **5. Project Structure & Deliverables**

The existing project structure is:

```
D:\projects\New folder (8)\
├── ManagementApp/                    # Already implemented
│   ├── Views/
│   ├── Controllers/
│   ├── Controls/
│   ├── Extensions/
│   ├── App.xaml
│   ├── App.xaml.cs
│   └── ManagementApp.csproj
├── DisplayApp/                       # TO BE IMPLEMENTED
│   ├── Views/
│   ├── Widgets/
│   ├── Services/
│   ├── App.xaml
│   ├── App.xaml.cs
│   └── DisplayApp.csproj
├── Shared/                           # Shared class library
│   ├── Models/                       # Employee, Shift, Absence, Task
│   ├── Services/                     # JsonHandler, SyncManager, LoggingService
│   ├── Utils/                        # ShamsiDateHelper
│   └── Shared.csproj
├── Data/                             # Shared data folder
│   ├── Reports/
│   ├── Exports/
│   └── Images/Staff/
└── ManagementApp.sln
```

**Your Task:** Implement the DisplayApp project to create a full-screen dashboard that reads data from the Management App's JSON reports and displays them in real-time.

**Important:** The DisplayApp must reference the existing Shared class library project to use the same data models (Employee, Shift, Absence, Task) and services (JsonHandler, SyncManager, LoggingService, ShamsiDateHelper). This ensures data consistency between both applications.

The final deliverable is a packaged Windows executable (`.exe`) for the Display App that runs on a clean Windows machine **without requiring .NET installation** (self-contained deployment).