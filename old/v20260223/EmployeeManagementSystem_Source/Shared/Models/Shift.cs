using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Shared.Models
{
    public class Shift
    {
        public string ShiftType { get; set; } = string.Empty; // "morning", "afternoon", or "night"
        public int Capacity { get; set; } = 15;
        public List<Employee?> AssignedEmployees { get; set; } = new();
        public List<string?> StatusCardIds { get; set; } = new();  // Status card ID per slot (null = no status card)
        public string TeamLeaderId { get; set; } = string.Empty; // Foreman/Team Leader ID
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Shift()
        {
            Capacity = 15;
            AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
            StatusCardIds = new List<string?>(new string?[Capacity]);
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public Shift(string shiftType, int capacity = 15)
        {
            ShiftType = shiftType;
            Capacity = capacity;
            AssignedEmployees = new List<Employee?>(new Employee?[capacity]);
            StatusCardIds = new List<string?>(new string?[capacity]);
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
            "morning" => "Morning",
            "afternoon" => "Afternoon",
            "evening" => "Afternoon", // Legacy support - map to afternoon
            "night" => "Night",
            _ => ShiftType
        };

        public bool AssignEmployeeToSlot(Employee employee, int slotIndex)
        {
            // Ensure AssignedEmployees is properly initialized
            if (AssignedEmployees == null || AssignedEmployees.Count == 0)
            {
                AssignedEmployees = new List<Employee?>(new Employee?[Capacity]);
            }
            
            // Ensure StatusCardIds is properly initialized
            EnsureStatusCardIdsInitialized();
            
            if (slotIndex < 0 || slotIndex >= Capacity)
                return false;

            // Check if slot has a status card - if so, clear it first (employee takes priority)
            if (IsSlotOccupiedByStatusCard(slotIndex))
            {
                ClearStatusCardFromSlot(slotIndex);
            }

            // If slot is occupied, clear it first (replace)
            if (AssignedEmployees[slotIndex] != null)
            {
                ClearSlot(slotIndex);
            }

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
            bool cleared = false;
            if (slotIndex >= 0 && slotIndex < AssignedEmployees.Count)
            {
                if (AssignedEmployees[slotIndex] != null)
                {
                    AssignedEmployees[slotIndex] = null;
                    cleared = true;
                }
            }
            
            // Also clear status card if present
            EnsureStatusCardIdsInitialized();
            if (slotIndex >= 0 && slotIndex < StatusCardIds.Count && StatusCardIds[slotIndex] != null)
            {
                StatusCardIds[slotIndex] = null;
                cleared = true;
            }
            
            if (cleared)
                UpdatedAt = DateTime.Now;
            
            return cleared;
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
                    if (AssignedEmployees[i] != null && AssignedEmployees[i].Equals(employee))
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

            return AssignedEmployees.Any(emp => emp != null && emp.Equals(employee));
        }

        public void SetCapacity(int newCapacity)
        {
            if (newCapacity < 0)
                newCapacity = 0;

            if (newCapacity > Capacity)
            {
                // Expand: add null slots
                var newEmployeeList = AssignedEmployees.ToList();
                var newStatusCardList = StatusCardIds?.ToList() ?? new List<string?>();
                for (int i = Capacity; i < newCapacity; i++)
                {
                    newEmployeeList.Add(null);
                    newStatusCardList.Add(null);
                }
                AssignedEmployees = newEmployeeList;
                StatusCardIds = newStatusCardList;
            }
            else
            {
                // Shrink: remove excess employees and slots
                AssignedEmployees = AssignedEmployees.Take(newCapacity).ToList();
                if (StatusCardIds != null)
                    StatusCardIds = StatusCardIds.Take(newCapacity).ToList();
            }

            Capacity = newCapacity;
            UpdatedAt = DateTime.Now;
        }

        #region Status Card Methods

        /// <summary>
        /// Ensures StatusCardIds list is properly initialized with the correct capacity.
        /// </summary>
        private void EnsureStatusCardIdsInitialized()
        {
            if (StatusCardIds == null)
            {
                StatusCardIds = new List<string?>(new string?[Capacity]);
            }
            else if (StatusCardIds.Count < Capacity)
            {
                // Expand to match capacity
                while (StatusCardIds.Count < Capacity)
                {
                    StatusCardIds.Add(null);
                }
            }
        }

        /// <summary>
        /// Assigns a status card to a specific slot. Clears any existing employee in that slot.
        /// </summary>
        public bool AssignStatusCardToSlot(string statusCardId, int slotIndex)
        {
            if (string.IsNullOrEmpty(statusCardId))
                return false;

            if (slotIndex < 0 || slotIndex >= Capacity)
                return false;

            EnsureStatusCardIdsInitialized();

            // Clear any existing employee in this slot
            if (AssignedEmployees != null && slotIndex < AssignedEmployees.Count && AssignedEmployees[slotIndex] != null)
            {
                AssignedEmployees[slotIndex] = null;
            }

            StatusCardIds[slotIndex] = statusCardId;
            UpdatedAt = DateTime.Now;
            return true;
        }

        /// <summary>
        /// Gets the status card ID at a specific slot, or null if no status card is assigned.
        /// </summary>
        public string? GetStatusCardAtSlot(int slotIndex)
        {
            EnsureStatusCardIdsInitialized();
            
            if (slotIndex >= 0 && slotIndex < StatusCardIds.Count)
                return StatusCardIds[slotIndex];
            return null;
        }

        /// <summary>
        /// Removes the status card from a specific slot.
        /// </summary>
        public bool ClearStatusCardFromSlot(int slotIndex)
        {
            EnsureStatusCardIdsInitialized();
            
            if (slotIndex >= 0 && slotIndex < StatusCardIds.Count && StatusCardIds[slotIndex] != null)
            {
                StatusCardIds[slotIndex] = null;
                UpdatedAt = DateTime.Now;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a slot is occupied by a status card.
        /// </summary>
        public bool IsSlotOccupiedByStatusCard(int slotIndex)
        {
            EnsureStatusCardIdsInitialized();
            return slotIndex >= 0 && slotIndex < StatusCardIds.Count && !string.IsNullOrEmpty(StatusCardIds[slotIndex]);
        }

        /// <summary>
        /// Gets the count of slots with status cards assigned.
        /// </summary>
        public int GetStatusCardCount()
        {
            EnsureStatusCardIdsInitialized();
            return StatusCardIds.Count(id => !string.IsNullOrEmpty(id));
        }

        #endregion

        public void SetTeamLeader(string employeeId)
        {
            TeamLeaderId = employeeId ?? string.Empty;
            UpdatedAt = DateTime.Now;
        }

        public Employee? GetTeamLeader(Dictionary<string, Employee> employees)
        {
            if (string.IsNullOrEmpty(TeamLeaderId) || employees == null)
                return null;

            return employees.ContainsKey(TeamLeaderId) ? employees[TeamLeaderId] : null;
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

                    // Restore team leader ID
                    shift.TeamLeaderId = shiftData.TeamLeaderId ?? string.Empty;

                    // Restore status card IDs (migration: if not present, use empty list)
                    if (shiftData.StatusCardIds != null && shiftData.StatusCardIds.Count > 0)
                    {
                        shift.StatusCardIds = new List<string?>(new string?[shift.Capacity]);
                        for (int i = 0; i < shiftData.StatusCardIds.Count && i < shift.Capacity; i++)
                        {
                            shift.StatusCardIds[i] = shiftData.StatusCardIds[i];
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
            EnsureStatusCardIdsInitialized();
            var shiftData = new ShiftData
            {
                ShiftType = ShiftType,
                Capacity = Capacity,
                AssignedEmployeeIds = AssignedEmployees.Select(emp => emp?.EmployeeId).ToList(),
                StatusCardIds = StatusCardIds.ToList(),
                TeamLeaderId = TeamLeaderId,
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
            public List<string?> StatusCardIds { get; set; } = new();  // Status card IDs per slot
            public string TeamLeaderId { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }

    public class ShiftManager
    {
        public Shift MorningShift { get; set; }
        public Shift AfternoonShift { get; set; }
        public Shift NightShift { get; set; }
        public int Capacity { get; set; } = 15;

        public ShiftManager()
        {
            Capacity = 15;
            MorningShift = new Shift("morning", Capacity);
            AfternoonShift = new Shift("afternoon", Capacity);
            NightShift = new Shift("night", Capacity);
        }

        public ShiftManager(int capacity = 15)
        {
            Capacity = capacity;
            MorningShift = new Shift("morning", capacity);
            AfternoonShift = new Shift("afternoon", capacity);
            NightShift = new Shift("night", capacity);
        }

        public void SetCapacity(int newCapacity)
        {
            var oldCapacity = Capacity;
            Capacity = newCapacity;
            MorningShift.SetCapacity(newCapacity);
            AfternoonShift.SetCapacity(newCapacity);
            NightShift.SetCapacity(newCapacity);
            
            // Log capacity changes for debugging
            System.Diagnostics.Debug.WriteLine($"ShiftManager.SetCapacity: Changed from {oldCapacity} to {newCapacity}");
        }

        public Shift? GetShift(string shiftType)
        {
            return shiftType switch
            {
                "morning" => MorningShift,
                "afternoon" => AfternoonShift,
                "evening" => AfternoonShift, // Legacy support
                "night" => NightShift,
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
            if (AfternoonShift.IsEmployeeAssigned(employee))
                shifts.Add("afternoon");
            if (NightShift.IsEmployeeAssigned(employee))
                shifts.Add("night");
            return shifts;
        }

        public void ClearShift(string shiftType)
        {
            var shift = GetShift(shiftType);
            shift?.ClearShift();
        }

        public override string ToString()
        {
            return $"ShiftManager(Morning: {MorningShift}, Afternoon: {AfternoonShift}, Night: {NightShift})";
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
                    
                    // Migration: try AfternoonShift first, fallback to EveningShift
                    if (managerData.AfternoonShift != null)
                        manager.AfternoonShift = Shift.FromJson(managerData.AfternoonShift, employeesDict);
                    else if (managerData.EveningShift != null)
                    {
                        manager.AfternoonShift = Shift.FromJson(managerData.EveningShift, employeesDict);
                        manager.AfternoonShift.ShiftType = "afternoon"; // Update type
                    }
                    
                    if (managerData.NightShift != null)
                        manager.NightShift = Shift.FromJson(managerData.NightShift, employeesDict);

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
                
                // Migration: Load afternoon shift (try afternoon first, fallback to evening)
                if (shiftsData.ContainsKey("afternoon"))
                {
                    var afternoonData = shiftsData["afternoon"];
                    if (afternoonData != null)
                    {
                        var afternoonJson = JsonConvert.SerializeObject(afternoonData);
                        manager.AfternoonShift = Shift.FromJson(afternoonJson, employeesDict);
                    }
                }
                else if (shiftsData.ContainsKey("evening"))
                {
                    var eveningData = shiftsData["evening"];
                    if (eveningData != null)
                    {
                        var eveningJson = JsonConvert.SerializeObject(eveningData);
                        manager.AfternoonShift = Shift.FromJson(eveningJson, employeesDict);
                        manager.AfternoonShift.ShiftType = "afternoon"; // Migrate type
                    }
                }
                
                // Load night shift
                if (shiftsData.ContainsKey("night"))
                {
                    var nightData = shiftsData["night"];
                    if (nightData != null)
                    {
                        var nightJson = JsonConvert.SerializeObject(nightData);
                        manager.NightShift = Shift.FromJson(nightJson, employeesDict);
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
                AfternoonShift = AfternoonShift.ToJson(),
                NightShift = NightShift.ToJson(),
                Capacity = Capacity
            };
            return JsonConvert.SerializeObject(managerData, Formatting.Indented);
        }

        private class ShiftManagerData
        {
            public string MorningShift { get; set; } = string.Empty;
            public string? AfternoonShift { get; set; } // Nullable for backward compatibility
            public string? EveningShift { get; set; } // Legacy - for reading old data
            public string? NightShift { get; set; } // Nullable for backward compatibility
            public int Capacity { get; set; }
        }
    }
}
