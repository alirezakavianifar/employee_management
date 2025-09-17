using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Shared.Models
{
    public class Shift
    {
        public string ShiftType { get; set; } = string.Empty; // "morning" or "evening"
        public int Capacity { get; set; } = 15;
        public List<Employee?> AssignedEmployees { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Shift()
        {
            Capacity = 15;
            AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public Shift(string shiftType, int capacity = 15)
        {
            ShiftType = shiftType;
            Capacity = capacity;
            AssignedEmployees = new List<Employee?>(new Employee?[capacity]);
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        [JsonIgnore]
        public bool IsFull => AssignedEmployees.All(emp => emp != null);

        [JsonIgnore]
        public int AvailableSlots => AssignedEmployees.Count(emp => emp == null);

        [JsonIgnore]
        public string DisplayName => ShiftType switch
        {
            "morning" => "صبح",
            "evening" => "عصر",
            _ => ShiftType
        };

        public bool AssignEmployeeToSlot(Employee employee, int slotIndex)
        {
            // Ensure AssignedEmployees is properly initialized
            if (AssignedEmployees == null || AssignedEmployees.Count == 0)
            {
                AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
            }
            
            if (slotIndex < 0 || slotIndex >= Capacity)
                return false;

            if (AssignedEmployees[slotIndex] != null)
                return false;

            // Remove employee from other slots first
            RemoveEmployee(employee);

            // Assign to the specific slot
            AssignedEmployees[slotIndex] = employee;
            UpdatedAt = DateTime.Now;
            return true;
        }

        public Employee? GetEmployeeAtSlot(int slotIndex)
        {
            // Ensure AssignedEmployees is properly initialized
            if (AssignedEmployees == null || AssignedEmployees.Count == 0)
            {
                AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
            }
            
            if (slotIndex >= 0 && slotIndex < AssignedEmployees.Count)
                return AssignedEmployees[slotIndex];
            return null;
        }

        public bool ClearSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < AssignedEmployees.Count)
            {
                if (AssignedEmployees[slotIndex] != null)
                {
                    AssignedEmployees[slotIndex] = null;
                    UpdatedAt = DateTime.Now;
                    return true;
                }
            }
            return false;
        }

        public bool AddEmployee(Employee employee)
        {
            if (IsFull)
                return false;

            // Find first available slot
            for (int i = 0; i < AssignedEmployees.Count; i++)
            {
                if (AssignedEmployees[i] == null)
                {
                    return AssignEmployeeToSlot(employee, i);
                }
            }

            return false;
        }

        public bool RemoveEmployee(Employee employee)
        {
            for (int i = 0; i < AssignedEmployees.Count; i++)
            {
                if (AssignedEmployees[i] == employee)
                {
                    AssignedEmployees[i] = null;
                    UpdatedAt = DateTime.Now;
                    return true;
                }
            }
            return false;
        }

        public void ClearShift()
        {
            AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
            UpdatedAt = DateTime.Now;
        }

        public Employee? GetEmployeeById(string employeeId)
        {
            return AssignedEmployees.FirstOrDefault(emp => emp?.EmployeeId == employeeId);
        }

        public bool IsEmployeeAssigned(Employee employee)
        {
            return AssignedEmployees.Contains(employee);
        }

        public void SetCapacity(int newCapacity)
        {
            if (newCapacity < 0)
                newCapacity = 0;

            if (newCapacity > Capacity)
            {
                // Expand: add null slots
                var newList = AssignedEmployees.ToList();
                for (int i = Capacity; i < newCapacity; i++)
                {
                    newList.Add(null);
                }
                AssignedEmployees = newList;
            }
            else
            {
                // Shrink: remove excess employees and slots
                AssignedEmployees = AssignedEmployees.Take(newCapacity).ToList();
            }

            Capacity = newCapacity;
            UpdatedAt = DateTime.Now;
        }

        public override string ToString()
        {
            var assignedCount = AssignedEmployees.Count(emp => emp != null);
            return $"Shift({DisplayName}: {assignedCount}/{Capacity})";
        }

        public static Shift FromJson(string json, Dictionary<string, Employee> employeesDict)
        {
            var shiftData = JsonConvert.DeserializeObject<ShiftData>(json);
            if (shiftData == null)
                return new Shift();

            var shift = new Shift(shiftData.ShiftType, shiftData.Capacity);
            
            // Restore employee assignments
            for (int i = 0; i < shiftData.AssignedEmployeeIds.Count && i < shift.Capacity; i++)
            {
                var empId = shiftData.AssignedEmployeeIds[i];
                if (!string.IsNullOrEmpty(empId) && employeesDict.ContainsKey(empId))
                {
                    shift.AssignedEmployees[i] = employeesDict[empId];
                }
            }

            shift.CreatedAt = shiftData.CreatedAt;
            shift.UpdatedAt = shiftData.UpdatedAt;
            return shift;
        }

        public string ToJson()
        {
            var shiftData = new ShiftData
            {
                ShiftType = ShiftType,
                Capacity = Capacity,
                AssignedEmployeeIds = AssignedEmployees.Select(emp => emp?.EmployeeId).ToList(),
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
            return JsonConvert.SerializeObject(shiftData, Formatting.Indented);
        }

        private class ShiftData
        {
            public string ShiftType { get; set; } = string.Empty;
            public int Capacity { get; set; }
            public List<string?> AssignedEmployeeIds { get; set; } = new();
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }

    public class ShiftManager
    {
        public Shift MorningShift { get; set; }
        public Shift EveningShift { get; set; }
        public int Capacity { get; set; } = 15; // Changed default from 5 to 15

        public ShiftManager()
        {
            Capacity = 15; // Changed default from 5 to 15
            MorningShift = new Shift("morning", Capacity);
            EveningShift = new Shift("evening", Capacity);
        }

        public ShiftManager(int capacity = 15)
        {
            Capacity = capacity;
            MorningShift = new Shift("morning", capacity);
            EveningShift = new Shift("evening", capacity);
        }

        public void SetCapacity(int newCapacity)
        {
            var oldCapacity = Capacity;
            Capacity = newCapacity;
            MorningShift.SetCapacity(newCapacity);
            EveningShift.SetCapacity(newCapacity);
            
            // Log capacity changes for debugging
            System.Diagnostics.Debug.WriteLine($"ShiftManager.SetCapacity: Changed from {oldCapacity} to {newCapacity}");
        }

        public Shift? GetShift(string shiftType)
        {
            return shiftType switch
            {
                "morning" => MorningShift,
                "evening" => EveningShift,
                _ => null
            };
        }

        public bool AssignEmployee(Employee employee, string shiftType)
        {
            // Check if employee is already assigned to another shift
            var currentShifts = GetEmployeeShifts(employee);
            if (currentShifts.Any() && !currentShifts.Contains(shiftType))
                return false;

            var shift = GetShift(shiftType);
            return shift?.AddEmployee(employee) ?? false;
        }

        public bool AssignEmployeeToSlot(Employee employee, string shiftType, int slotIndex)
        {
            // Check if employee is already assigned to another shift
            var currentShifts = GetEmployeeShifts(employee);
            if (currentShifts.Any() && !currentShifts.Contains(shiftType))
                return false;

            var shift = GetShift(shiftType);
            return shift?.AssignEmployeeToSlot(employee, slotIndex) ?? false;
        }

        public bool RemoveEmployeeFromShift(Employee employee, string shiftType)
        {
            var shift = GetShift(shiftType);
            return shift?.RemoveEmployee(employee) ?? false;
        }

        public List<string> GetEmployeeShifts(Employee employee)
        {
            var shifts = new List<string>();
            if (MorningShift.IsEmployeeAssigned(employee))
                shifts.Add("morning");
            if (EveningShift.IsEmployeeAssigned(employee))
                shifts.Add("evening");
            return shifts;
        }

        public void ClearShift(string shiftType)
        {
            var shift = GetShift(shiftType);
            shift?.ClearShift();
        }

        public override string ToString()
        {
            return $"ShiftManager(Morning: {MorningShift}, Evening: {EveningShift})";
        }

        public static ShiftManager FromJson(string json, Dictionary<string, Employee> employeesDict)
        {
            var managerData = JsonConvert.DeserializeObject<ShiftManagerData>(json);
            if (managerData == null)
                return new ShiftManager();

            var manager = new ShiftManager(managerData.Capacity);
            
            if (managerData.MorningShift != null)
                manager.MorningShift = Shift.FromJson(managerData.MorningShift, employeesDict);
            
            if (managerData.EveningShift != null)
                manager.EveningShift = Shift.FromJson(managerData.EveningShift, employeesDict);

            return manager;
        }

        public string ToJson()
        {
            var managerData = new ShiftManagerData
            {
                MorningShift = MorningShift.ToJson(),
                EveningShift = EveningShift.ToJson(),
                Capacity = Capacity
            };
            return JsonConvert.SerializeObject(managerData, Formatting.Indented);
        }

        private class ShiftManagerData
        {
            public string MorningShift { get; set; } = string.Empty;
            public string EveningShift { get; set; } = string.Empty;
            public int Capacity { get; set; }
        }
    }
}
