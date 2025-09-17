Perfect 👍 I’ll add a **recommended folder structure** section so your developers have a clean starting point for organizing the project.

Here’s the updated and final extended specification:

---

# Project Specification: Employee Shift Management & Display System

## 1. Overview

This project consists of two lightweight applications written in **Python with PyQt5 for the UI**:

1. **Management App** – for managing employees, shifts, absences, and settings.
2. **Display App** – for presenting shifts, managers, absences, performance charts, and short AI-generated insights on a full-screen dashboard.

The system is designed to be simple, portable, and serverless: all data is stored in **JSON files** within a shared folder (e.g., Network Drive, Google Drive, or OneDrive).

---python bui   

## 2. System Architecture

* **Language:** Python
* **UI Framework:** PyQt5 (cross-platform, packaged for Windows)
* **Storage:** JSON files only (no SQL/Database)
* **Shared Folder:** Common directory for read/write operations by both apps
* **Sync:** Automatic synchronization every 30 seconds
* **Conflict Resolution:** Last edit wins; previous version stored with timestamp
* **Offline Mode:** Display app uses last valid file if disconnected

---

## 3. Features

### 3.1 Display App (Dashboard)

* **Managers Panel:** 3 managers shown in top-left (not part of shifts)
* **Shift Display:** Morning & Evening rows, with first names only
* **Capacity:** Configurable (default 10, demo 5)
* **Absence Panel:** Categories → Leave, Sick, Absent
* **Performance Chart:** *“Performance Increase — Week N”* (Persian labels, rendered with matplotlib/pyqtgraph)
* **AI Panel:** Short insights (e.g., “122 orders → up to 3 leaves”, “+20% → overtime”)

### 3.2 Management App

* **Employee Management:** Search, Add/Edit/Delete, CSV Import
* **Shift Management:** Drag & Drop employees into a 5×2 grid, capacity enforcement, free movement, auto-save
* **Absence Management:** Quick entry, automatic removal from shift, daily report
* **Settings:** UI customization, managers’ info, sync path, roles, export (PNG/PDF/CSV)

---

## 4. Data Model

* **Daily File:** `report_YYYY-MM-DD.json` (English date, Persian keys)
* **Photos:** 1:1 ratio, min 400×400 px, JPG/PNG, path → `images/staff/`

---

## 5. AI Assistant (Rule-Based Engine)

* Next week’s orders ≤ capacity → *“Up to 3 leaves possible”*
* Two weeks’ orders ≥ 1.15 × capacity → *“Overtime should be scheduled”*
* Output: short Persian text only

---

## 6. Synchronization Logic

* Folder check: every 30 seconds
* Conflict: latest edit wins, previous saved with timestamp
* Offline display → last valid file

---

## 7. Acceptance Checklist

* [ ] 3 managers top-left, equal size
* [ ] Shift capacities applied from settings
* [ ] Only 3 absence categories
* [ ] Chart with Persian labels
* [ ] Drag & Drop with Persian messages & auto-save
* [ ] JSON-only, no DB/server
* [ ] Must be written in **Python with PyQt5**

---

## 8. Delivery & Deployment Requirements

* **Two source codes** must be delivered (Management App + Display App)
* **Python/PyQt5 applications**, packaged to run on **Windows** without installation
* All data stored in **OneDrive folder**, so results can be accessed live from home if OneDrive is available on the system

---

## 9. Recommended Python Dependencies

| Purpose              | Package(s)                           | Notes                                         |
| -------------------- | ------------------------------------ | --------------------------------------------- |
| **UI Development**   | `PyQt5`                              | Main GUI framework                            |
| **Charts & Graphs**  | `matplotlib`, `pyqtgraph`            | For performance charts, visualizations        |
| **Data Handling**    | `pandas`                             | JSON/CSV read/write, data processing          |
| **File Export**      | `reportlab` (PDF), `Pillow` (images) | For PDF/PNG export                            |
| **Sync & Utilities** | `watchdog`                           | To monitor shared folder changes              |
| **Packaging**        | `PyInstaller`                        | Bundle into Windows EXE (no install required) |

---

## 10. Suggested Folder Structure

```
project_root/
│
├── management_app/                 # Management app source
│   ├── ui/                         # PyQt5 UI files (.ui or .py)
│   ├── models/                     # Data models (Employee, Shift, Absence)
│   ├── controllers/                # Business logic
│   ├── views/                      # PyQt5 windows/forms
│   ├── utils/                      # Helpers (JSON I/O, sync, etc.)
│   └── main.py                     # Entry point
│
├── display_app/                    # Display app source
│   ├── ui/                         # PyQt5 UI files
│   ├── widgets/                    # Custom widgets (charts, panels)
│   ├── services/                   # Data reading, AI rules
│   ├── utils/                      # Shared helpers
│   └── main.py                     # Entry point
│
├── shared/                         # Common code (if needed)
│   ├── json_handler.py             # Read/write JSON logic
│   ├── sync.py                     # Folder sync functions
│   └── ai_rules.py                 # Rule-based AI assistant
│
├── data/                           # Data folder (OneDrive shared path)
│   ├── reports/                    # JSON daily reports
│   ├── exports/                    # PNG, PDF, CSV outputs
│   └── images/staff/               # Staff images
│
├── requirements.txt                # Python dependencies
├── README.md                       # Documentation
└── build/                          # PyInstaller build outputs
```

---

✅ With this structure, the development team can:

* Separate UI, logic, and data cleanly (MVC-inspired).
* Reuse shared code (JSON handling, AI rules, sync).
* Package each app independently.

---


