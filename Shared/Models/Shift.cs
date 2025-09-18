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
            try
            {
                System.Diagnostics.Debug.WriteLine($"Shift.RemoveEmployee: employee={employee?.FullName}, AssignedEmployees={AssignedEmployees?.Count ?? -1}");
                
                // Ensure AssignedEmployees is properly initialized
                if (AssignedEmployees == null || AssignedEmployees.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Shift.RemoveEmployee: Initializing AssignedEmployees with capacity {Capacity}");
                    AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
                    return false;
                }

                for (int i = 0; i < AssignedEmployees.Count; i++)
                {
                    if (AssignedEmployees[i] == employee)
                    {
                        System.Diagnostics.Debug.WriteLine($"Shift.RemoveEmployee: Found employee at index {i}, removing");
                        AssignedEmployees[i] = null;
                        UpdatedAt = DateTime.Now;
                        return true;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Shift.RemoveEmployee: Employee not found in shift");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Shift.RemoveEmployee: Exception - {ex.Message}");
                throw;
            }
        }

        public void ClearShift()
        {
            AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
            UpdatedAt = DateTime.Now;
        }

        public Employee? GetEmployeeById(string employeeId)
        {
            // Ensure AssignedEmployees is properly initialized
            if (AssignedEmployees == null || AssignedEmployees.Count == 0)
            {
                AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
                return null;
            }

            return AssignedEmployees.FirstOrDefault(emp => emp?.EmployeeId == employeeId);
        }

        public bool IsEmployeeAssigned(Employee employee)
        {
            // Ensure AssignedEmployees is properly initialized
            if (AssignedEmployees == null || AssignedEmployees.Count == 0)
            {
                AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
                return false;
            }

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
            try
            {
                // Try to deserialize as the expected ShiftData structure first
                var shiftData = JsonConvert.DeserializeObject<ShiftData>(json);
                if (shiftData != null && !string.IsNullOrEmpty(shiftData.ShiftType))
                {
                    var shift = new Shift(shiftData.ShiftType, shiftData.Capacity);
                    
                    // Ensure AssignedEmployees is properly initialized
                    if (shift.AssignedEmployees == null || shift.AssignedEmployees.Count == 0)
                    {
                        shift.AssignedEmployees = new List<Employee?>(new Employee?[shift.Capacity]);
                    }
                    
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
            }
            catch
            {
                // If that fails, try to deserialize as the actual structure being saved
            }

            // Try to deserialize as the actual structure being saved (Dictionary<string, object>)
            var shiftDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (shiftDict != null)
            {
                var shiftType = shiftDict.GetValueOrDefault("shift_type", "").ToString() ?? "";
                var capacity = 15; // Default capacity
                
                if (shiftDict.ContainsKey("capacity") && int.TryParse(shiftDict["capacity"].ToString(), out int parsedCapacity))
                {
                    capacity = parsedCapacity;
                }

                var shift = new Shift(shiftType, capacity);
                
                // Ensure AssignedEmployees is properly initialized
                if (shift.AssignedEmployees == null || shift.AssignedEmployees.Count == 0)
                {
                    shift.AssignedEmployees = new List<Employee?>(new Employee?[shift.Capacity]);
                }
                
                // Load assigned employees
                if (shiftDict.ContainsKey("assigned_employees"))
                {
                    var assignedEmployeesData = shiftDict["assigned_employees"];
                    if (assignedEmployeesData is Newtonsoft.Json.Linq.JArray jArray)
                    {
                        var assignedEmployees = jArray.ToObject<List<object>>();
                        if (assignedEmployees != null)
                        {
                            for (int i = 0; i < assignedEmployees.Count && i < shift.Capacity; i++)
                            {
                                var empObj = assignedEmployees[i];
                                if (empObj != null)
                                {
                                    // Try to extract employee ID from the employee object
                                    string? empId = null;
                                    
                                    if (empObj is Dictionary<string, object> empDict)
                                    {
                                        empId = empDict.GetValueOrDefault("employee_id", "").ToString();
                                    }
                                    else if (empObj is Newtonsoft.Json.Linq.JObject jObj)
                                    {
                                        empId = jObj["employee_id"]?.ToString();
                                    }
                                    
                                    if (!string.IsNullOrEmpty(empId) && employeesDict.ContainsKey(empId))
                                    {
                                        shift.AssignedEmployees[i] = employeesDict[empId];
                                    }
                                }
                            }
                        }
                    }
                }

                return shift;
            }

            // If all else fails, return a new shift
            var fallbackShift = new Shift();
            // Ensure AssignedEmployees is properly initialized
            if (fallbackShift.AssignedEmployees == null || fallbackShift.AssignedEmployees.Count == 0)
            {
                fallbackShift.AssignedEmployees = new List<Employee?>(new Employee?[fallbackShift.Capacity]);
            }
            return fallbackShift;
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
            try
            {
                System.Diagnostics.Debug.WriteLine($"RemoveEmployeeFromShift: employee={employee?.FullName}, shiftType={shiftType}");
                
                var shift = GetShift(shiftType);
                if (shift == null)
                {
                    System.Diagnostics.Debug.WriteLine($"RemoveEmployeeFromShift: shift is null for type {shiftType}");
                    return false;
                }
                
                System.Diagnostics.Debug.WriteLine($"RemoveEmployeeFromShift: found shift, calling RemoveEmployee");
                return shift.RemoveEmployee(employee);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveEmployeeFromShift: Exception - {ex.Message}");
                throw;
            }
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
            try
            {
                // Try to deserialize as the expected ShiftManagerData structure first
                var managerData = JsonConvert.DeserializeObject<ShiftManagerData>(json);
                if (managerData != null && !string.IsNullOrEmpty(managerData.MorningShift))
                {
                    var manager = new ShiftManager(managerData.Capacity);
                    
                    if (managerData.MorningShift != null)
                        manager.MorningShift = Shift.FromJson(managerData.MorningShift, employeesDict);
                    
                    if (managerData.EveningShift != null)
                        manager.EveningShift = Shift.FromJson(managerData.EveningShift, employeesDict);

                    return manager;
                }
            }
            catch
            {
                // If that fails, try to deserialize as the actual structure being saved
            }

            // Try to deserialize as the actual structure being saved (Dictionary<string, object>)
            var shiftsData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (shiftsData != null)
            {
                var manager = new ShiftManager(15); // Default capacity
                
                // Load morning shift
                if (shiftsData.ContainsKey("morning"))
                {
                    var morningData = shiftsData["morning"];
                    if (morningData != null)
                    {
                        var morningJson = JsonConvert.SerializeObject(morningData);
                        manager.MorningShift = Shift.FromJson(morningJson, employeesDict);
                    }
                }
                
                // Load evening shift
                if (shiftsData.ContainsKey("evening"))
                {
                    var eveningData = shiftsData["evening"];
                    if (eveningData != null)
                    {
                        var eveningJson = JsonConvert.SerializeObject(eveningData);
                        manager.EveningShift = Shift.FromJson(eveningJson, employeesDict);
                    }
                }
                
                return manager;
            }

            // If all else fails, return a new manager
            return new ShiftManager();
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
