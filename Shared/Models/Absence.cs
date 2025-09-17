using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shared.Utils;

namespace Shared.Models
{
    public class Absence
    {
        public static readonly Dictionary<string, string> Categories = new()
        {
            { "مرخصی", "Leave" },
            { "بیمار", "Sick" },
            { "غایب", "Absent" }
        };

        public Employee Employee { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // Shamsi date in yyyy/MM/dd format
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Absence()
        {
            Employee = new Employee();
            Date = ShamsiDateHelper.GetCurrentShamsiDate();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public Absence(Employee employee, string category, string? date = null, string notes = "")
        {
            Employee = employee;
            Category = category;
            Date = date ?? ShamsiDateHelper.GetCurrentShamsiDate();
            Notes = notes;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        [JsonIgnore]
        public string CategoryDisplay => Category;

        [JsonIgnore]
        public string EmployeeName => Employee.FullName;

        [JsonIgnore]
        public string EmployeeId => Employee.EmployeeId;

        [JsonIgnore]
        public string DateDisplay => ShamsiDateHelper.FormatForDisplay(Date);

        [JsonIgnore]
        public string GregorianDate => ShamsiDateHelper.ShamsiToGregorian(Date);

        public bool IsValidCategory()
        {
            return Categories.ContainsKey(Category);
        }

        public override string ToString()
        {
            return $"Absence({EmployeeName}: {CategoryDisplay})";
        }

        public static Absence FromJson(string json, Dictionary<string, Employee> employeesDict)
        {
            var absenceData = JsonConvert.DeserializeObject<AbsenceData>(json);
            if (absenceData == null)
                return new Absence();

            var employee = employeesDict.ContainsKey(absenceData.EmployeeId) 
                ? employeesDict[absenceData.EmployeeId]
                : new Employee(absenceData.EmployeeId, absenceData.FirstName, absenceData.LastName, "Employee", absenceData.PhotoPath);

            var absence = new Absence(employee, absenceData.Category, absenceData.Date, absenceData.Notes)
            {
                CreatedAt = absenceData.CreatedAt,
                UpdatedAt = absenceData.UpdatedAt
            };

            return absence;
        }

        public string ToJson()
        {
            var absenceData = new AbsenceData
            {
                EmployeeId = Employee.EmployeeId,
                FirstName = Employee.FirstName,
                LastName = Employee.LastName,
                EmployeeName = Employee.FullName,
                PhotoPath = Employee.PhotoPath,
                Category = Category,
                Date = Date,
                Notes = Notes,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
            return JsonConvert.SerializeObject(absenceData, Formatting.Indented);
        }

        private class AbsenceData
        {
            public string EmployeeId { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string EmployeeName { get; set; } = string.Empty;
            public string PhotoPath { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }

    public class AbsenceManager
    {
        public Dictionary<string, List<Absence>> Absences { get; set; } = new();

        public AbsenceManager()
        {
            Absences = new Dictionary<string, List<Absence>>
            {
                { "مرخصی", new List<Absence>() },
                { "بیمار", new List<Absence>() },
                { "غایب", new List<Absence>() }
            };
        }

        public bool AddAbsence(Absence absence)
        {
            if (!absence.IsValidCategory())
                return false;

            // Check if employee already has an absence for this date
            if (HasAbsenceForEmployee(absence.Employee, absence.Date))
                return false;

            Absences[absence.Category].Add(absence);
            return true;
        }

        public bool RemoveAbsence(Absence absence)
        {
            if (Absences.ContainsKey(absence.Category))
            {
                return Absences[absence.Category].Remove(absence);
            }
            return false;
        }

        public List<Absence> GetAbsencesByCategory(string category)
        {
            return Absences.GetValueOrDefault(category, new List<Absence>());
        }

        public List<Absence> GetAbsencesByEmployee(Employee employee)
        {
            var allAbsences = new List<Absence>();
            foreach (var categoryAbsences in Absences.Values)
            {
                foreach (var absence in categoryAbsences)
                {
                    if (absence.Employee == employee)
                    {
                        allAbsences.Add(absence);
                    }
                }
            }
            return allAbsences;
        }

        public bool HasAbsenceForEmployee(Employee employee, string date)
        {
            foreach (var categoryAbsences in Absences.Values)
            {
                foreach (var absence in categoryAbsences)
                {
                    if (absence.Employee == employee && absence.Date == date)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public Absence? GetAbsenceForEmployee(Employee employee, string date)
        {
            foreach (var categoryAbsences in Absences.Values)
            {
                foreach (var absence in categoryAbsences)
                {
                    if (absence.Employee == employee && absence.Date == date)
                    {
                        return absence;
                    }
                }
            }
            return null;
        }

        public void RemoveEmployeeAbsences(Employee employee)
        {
            foreach (var category in Absences.Keys.ToList())
            {
                Absences[category] = Absences[category]
                    .Where(absence => absence.Employee != employee)
                    .ToList();
            }
        }

        public int GetCategoryCount(string category)
        {
            return Absences.GetValueOrDefault(category, new List<Absence>()).Count;
        }

        public int GetTotalAbsences()
        {
            return Absences.Values.Sum(absences => absences.Count);
        }

        public void ClearCategory(string category)
        {
            if (Absences.ContainsKey(category))
            {
                Absences[category].Clear();
            }
        }

        public void ClearAll()
        {
            foreach (var category in Absences.Keys)
            {
                Absences[category].Clear();
            }
        }

        public override string ToString()
        {
            var counts = Absences.Select(kvp => $"{kvp.Key}: {kvp.Value.Count}");
            return $"AbsenceManager({string.Join(", ", counts)})";
        }

        public static AbsenceManager FromJson(string json, Dictionary<string, Employee> employeesDict)
        {
            var managerData = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
            if (managerData == null)
                return new AbsenceManager();

            var manager = new AbsenceManager();

            foreach (var kvp in managerData)
            {
                var category = kvp.Key;
                var absenceJsonList = kvp.Value;

                if (manager.Absences.ContainsKey(category))
                {
                    foreach (var absenceJson in absenceJsonList)
                    {
                        var absence = Absence.FromJson(absenceJson, employeesDict);
                        if (absence != null)
                        {
                            manager.Absences[category].Add(absence);
                        }
                    }
                }
            }

            return manager;
        }

        public string ToJson()
        {
            var result = new Dictionary<string, List<string>>();
            foreach (var kvp in Absences)
            {
                result[kvp.Key] = kvp.Value.Select(absence => absence.ToJson()).ToList();
            }
            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }
    }
}
