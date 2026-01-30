
---

## Shift Display & Management Requirements

### 1. Three Full Shifts (Morning / Afternoon / Night)

The system must fully support **three shifts**:

* Morning
* Afternoon
* Night

---

### 2. One Employee per Group per Shift

* Each group can have **only one employee per shift**.
* Within each group, shifts must be displayed **vertically**:

  * Morning at the top
  * Afternoon in the middle
  * Night at the bottom

---

### 3. Table-Based Layout (3 Rows × N Columns)

The UI must be displayed in a **structured table format**:

* **Rows (3):** Shifts (Morning / Afternoon / Night)
* **Columns:** Groups

  * Example: If there are 10 groups, the table has 10 columns.

At the top of each column:

* The **group name** must be displayed.

---

### 4. Cell Content (Shift × Group)

Each cell (intersection of a shift and a group) must display:

* Employee photo
* Employee name and personnel ID below the photo
* If no employee is assigned, the cell must clearly indicate an **empty state**

---

### 5. Status Cards per Group and Shift (Out of Order / Empty / Available)

Instead of assigning an employee, it must be possible to assign a **status card** to a group/room for each shift, such as:

* “Out of Order”
* “Empty”
* “Available / Usable”

Important notes:

* Status cards must be **user-defined** (created by the user).
* Each status card must be usable for **any shift** (Morning / Afternoon / Night).

---

### 6. Text Labels for Employees

The system must allow adding **text labels** to employees.

Label specifications:

* A small rectangular element
* Exactly the same width as the employee photo
* Displayed **below the employee photo** after assignment
* Clearly associated with the corresponding employee

Label use cases may include:

* Current phone number
* Vehicle name / license plate / model
* Name of a specific person
* Any other short text

---

### 7. Label Creation and Assignment via Drag & Drop

* Provide a section for **quick label creation** by typing text.
* Labels must be assignable to employee cards using **Drag & Drop**.
* When dropped:

  * The label is automatically placed below the employee photo
  * The assignment is saved in the database / persistent storage

---

### 8. Label Archive (Persistent Storage)

A separate **Label Archive** must exist where:

* All previously created labels are stored
* The list is scrollable
* Any label can be reused and assigned to another employee

---

### 9. Removing Labels from Employees (Fast & Simple)

Users must be able to quickly remove a label from an employee by:

* Right-clicking the label → Remove (×)
  **or**
* Drag & Drop the label outside the employee card / into a trash area / back to the archive

After removal:

* The label is removed from the employee card
* The label remains in the archive for reuse
  (unless the user explicitly chooses **“Delete permanently from archive”**)

---

## Language & Source Code Requirements

### 20. English-Only Software and Source Code

* All UI text must be in **English**
* All variable names, class names, file names, and source code must be in **English**

---

### 21. Language Switching via a Single Resource File

A simple localization system must be implemented:

* Use a centralized resource file (e.g. `strings.json` or `resources.xml`)
* All UI text must be loaded from this file
* Changing the language must be possible **only by editing this file**
* After compilation, the application language must reflect the contents of the resource file

> Text strings must not be hardcoded.
> A single centralized location for all UI text is required.
