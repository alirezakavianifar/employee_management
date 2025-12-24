using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Shared.Models
{
    public class ShiftGroup
    {
        public string GroupId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = "#4CAF50"; // Default green color
        public int MorningCapacity { get; set; } = 15;
        public int EveningCapacity { get; set; } = 15;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Shifts for this group
        public Shift MorningShift { get; set; }
        public Shift EveningShift { get; set; }

        public ShiftGroup()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            // Ensure properties have valid default values
            if (string.IsNullOrEmpty(GroupId))
                GroupId = $"group_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
            if (string.IsNullOrEmpty(Name))
                Name = "گروه جدید";
            if (string.IsNullOrEmpty(Description))
                Description = "بدون توضیحات";
            if (string.IsNullOrEmpty(Color))
                Color = "#4CAF50";
            if (MorningCapacity <= 0)
                MorningCapacity = 15;
            if (EveningCapacity <= 0)
                EveningCapacity = 15;
            
            MorningShift = new Shift("morning", MorningCapacity);
            EveningShift = new Shift("evening", EveningCapacity);
        }

        public ShiftGroup(string groupId, string name, string description = "", string color = "#4CAF50", 
                         int morningCapacity = 15, int eveningCapacity = 15)
        {
            GroupId = groupId ?? $"group_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
            Name = name ?? "گروه جدید";
            Description = description ?? "بدون توضیحات";
            Color = color ?? "#4CAF50";
            MorningCapacity = morningCapacity > 0 ? morningCapacity : 15;
            EveningCapacity = eveningCapacity > 0 ? eveningCapacity : 15;
            IsActive = true;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            
            MorningShift = new Shift("morning", MorningCapacity);
            EveningShift = new Shift("evening", EveningCapacity);
        }

        public void Update(string? name = null, string? description = null, string? color = null, 
                          int? morningCapacity = null, int? eveningCapacity = null, bool? isActive = null)
        {
            if (!string.IsNullOrEmpty(name))
                Name = name;
            if (!string.IsNullOrEmpty(description))
                Description = description;
            if (!string.IsNullOrEmpty(color))
                Color = color;
            if (morningCapacity.HasValue)
            {
                MorningCapacity = morningCapacity.Value;
                if (MorningShift == null)
                    MorningShift = new Shift("morning", MorningCapacity);
                else
                    MorningShift.SetCapacity(morningCapacity.Value);
            }
            if (eveningCapacity.HasValue)
            {
                EveningCapacity = eveningCapacity.Value;
                if (EveningShift == null)
                    EveningShift = new Shift("evening", EveningCapacity);
                else
                    EveningShift.SetCapacity(eveningCapacity.Value);
            }
            if (isActive.HasValue)
                IsActive = isActive.Value;
            
            UpdatedAt = DateTime.Now;
        }

        public Shift? GetShift(string shiftType)
        {
            // Ensure shifts are initialized
            if (MorningShift == null)
                MorningShift = new Shift("morning", MorningCapacity);
            if (EveningShift == null)
                EveningShift = new Shift("evening", EveningCapacity);
                
            return shiftType switch
            {
                "morning" => MorningShift,
                "evening" => EveningShift,
                _ => null
            };
        }

        public bool AssignEmployee(Employee employee, string shiftType)
        {
            var shift = GetShift(shiftType);
            if (shift == null) return false;

            // Check if employee is already assigned to another shift in this group
            var currentShifts = GetEmployeeShifts(employee);
            if (currentShifts.Any() && !currentShifts.Contains(shiftType))
                return false;

            return shift.AddEmployee(employee);
        }

        public bool AssignEmployeeToSlot(Employee employee, string shiftType, int slotIndex)
        {
            var shift = GetShift(shiftType);
            if (shift == null) return false;

            // Check if employee is already assigned to another shift in this group
            var currentShifts = GetEmployeeShifts(employee);
            if (currentShifts.Any() && !currentShifts.Contains(shiftType))
                return false;

            return shift.AssignEmployeeToSlot(employee, slotIndex);
        }

        public bool RemoveEmployeeFromShift(Employee employee, string shiftType)
        {
            var shift = GetShift(shiftType);
            return shift?.RemoveEmployee(employee) ?? false;
        }

        public List<string> GetEmployeeShifts(Employee employee)
        {
            var shifts = new List<string>();
            if (MorningShift != null && MorningShift.IsEmployeeAssigned(employee))
                shifts.Add("morning");
            if (EveningShift != null && EveningShift.IsEmployeeAssigned(employee))
                shifts.Add("evening");
            return shifts;
        }

        public void ClearShift(string shiftType)
        {
            var shift = GetShift(shiftType);
            shift?.ClearShift();
        }

        public void SwapShifts()
        {
            // Ensure shifts are initialized
            if (MorningShift == null)
                MorningShift = new Shift("morning", MorningCapacity);
            if (EveningShift == null)
                EveningShift = new Shift("evening", EveningCapacity);
            
            // Swap AssignedEmployees lists
            var tempEmployees = new List<Employee?>(MorningShift.AssignedEmployees);
            MorningShift.AssignedEmployees = new List<Employee?>(EveningShift.AssignedEmployees);
            EveningShift.AssignedEmployees = tempEmployees;
            
            // Swap TeamLeaderId values
            var tempTeamLeader = MorningShift.TeamLeaderId;
            MorningShift.TeamLeaderId = EveningShift.TeamLeaderId;
            EveningShift.TeamLeaderId = tempTeamLeader;
            
            // Update timestamps
            MorningShift.UpdatedAt = DateTime.Now;
            EveningShift.UpdatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public void SetTeamLeader(string shiftType, string employeeId)
        {
            // Ensure shifts are initialized before accessing them
            if (MorningShift == null)
                MorningShift = new Shift("morning", MorningCapacity);
            if (EveningShift == null)
                EveningShift = new Shift("evening", EveningCapacity);
                
            var shift = GetShift(shiftType);
            if (shift != null)
            {
                shift.SetTeamLeader(employeeId);
            }
            UpdatedAt = DateTime.Now;
        }

        public Employee? GetTeamLeader(string shiftType, Dictionary<string, Employee> employees)
        {
            var shift = GetShift(shiftType);
            return shift?.GetTeamLeader(employees);
        }

        public int GetTotalAssignedEmployees()
        {
            var morningCount = MorningShift?.AssignedEmployees.Count(emp => emp != null) ?? 0;
            var eveningCount = EveningShift?.AssignedEmployees.Count(emp => emp != null) ?? 0;
            return morningCount + eveningCount;
        }

        public int GetTotalCapacity()
        {
            return MorningCapacity + EveningCapacity;
        }

        public override bool Equals(object? obj)
        {
            return obj is ShiftGroup group && GroupId == group.GroupId;
        }

        public override int GetHashCode()
        {
            return GroupId.GetHashCode();
        }

        public override string ToString()
        {
            return $"ShiftGroup({GroupId}: {Name})";
        }

        public static ShiftGroup FromJson(string json, Dictionary<string, Employee> employeesDict)
        {
            try
            {
                var groupData = JsonConvert.DeserializeObject<ShiftGroupData>(json);
                if (groupData != null && !string.IsNullOrEmpty(groupData.GroupId))
                {
                    var group = new ShiftGroup(groupData.GroupId, groupData.Name, groupData.Description, 
                                             groupData.Color, groupData.MorningCapacity, groupData.EveningCapacity);
                    group.IsActive = groupData.IsActive;
                    group.CreatedAt = groupData.CreatedAt;
                    group.UpdatedAt = groupData.UpdatedAt;

                    // Load shifts
                    if (!string.IsNullOrEmpty(groupData.MorningShift))
                        group.MorningShift = Shift.FromJson(groupData.MorningShift, employeesDict);
                    if (!string.IsNullOrEmpty(groupData.EveningShift))
                        group.EveningShift = Shift.FromJson(groupData.EveningShift, employeesDict);

                    return group;
                }
            }
            catch
            {
                // If that fails, try to deserialize as the actual structure being saved
            }

            // Try to deserialize as the actual structure being saved (Dictionary<string, object>)
            var groupDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (groupDict != null)
            {
                var groupId = groupDict.GetValueOrDefault("group_id", "").ToString() ?? "";
                var name = groupDict.GetValueOrDefault("name", "").ToString() ?? "";
                var description = groupDict.GetValueOrDefault("description", "").ToString() ?? "";
                var color = groupDict.GetValueOrDefault("color", "#4CAF50").ToString() ?? "#4CAF50";
                var morningCapacity = 15;
                var eveningCapacity = 15;
                var isActive = true;

                if (groupDict.ContainsKey("morning_capacity") && int.TryParse(groupDict["morning_capacity"].ToString(), out int parsedMorningCapacity))
                    morningCapacity = parsedMorningCapacity;
                if (groupDict.ContainsKey("evening_capacity") && int.TryParse(groupDict["evening_capacity"].ToString(), out int parsedEveningCapacity))
                    eveningCapacity = parsedEveningCapacity;
                if (groupDict.ContainsKey("is_active") && bool.TryParse(groupDict["is_active"].ToString(), out bool parsedIsActive))
                    isActive = parsedIsActive;

                var group = new ShiftGroup(groupId, name, description, color, morningCapacity, eveningCapacity);
                group.IsActive = isActive;

                // Load shifts
                if (groupDict.ContainsKey("morning_shift"))
                {
                    var morningShiftData = groupDict["morning_shift"];
                    if (morningShiftData != null)
                    {
                        var morningShiftJson = JsonConvert.SerializeObject(morningShiftData);
                        group.MorningShift = Shift.FromJson(morningShiftJson, employeesDict);
                    }
                }

                if (groupDict.ContainsKey("evening_shift"))
                {
                    var eveningShiftData = groupDict["evening_shift"];
                    if (eveningShiftData != null)
                    {
                        var eveningShiftJson = JsonConvert.SerializeObject(eveningShiftData);
                        group.EveningShift = Shift.FromJson(eveningShiftJson, employeesDict);
                    }
                }

                return group;
            }

            // If all else fails, return a new group
            return new ShiftGroup();
        }

        public string ToJson()
        {
            var groupData = new ShiftGroupData
            {
                GroupId = GroupId,
                Name = Name,
                Description = Description,
                Color = Color,
                MorningCapacity = MorningCapacity,
                EveningCapacity = EveningCapacity,
                IsActive = IsActive,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                MorningShift = MorningShift.ToJson(),
                EveningShift = EveningShift.ToJson()
            };
            return JsonConvert.SerializeObject(groupData, Formatting.Indented);
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "group_id", GroupId },
                { "name", Name },
                { "description", Description },
                { "color", Color },
                { "morning_capacity", MorningCapacity },
                { "evening_capacity", EveningCapacity },
                { "is_active", IsActive },
                { "created_at", CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) },
                { "updated_at", UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) },
                { "morning_shift", JsonConvert.DeserializeObject<Dictionary<string, object>>(MorningShift.ToJson()) ?? new Dictionary<string, object>() },
                { "evening_shift", JsonConvert.DeserializeObject<Dictionary<string, object>>(EveningShift.ToJson()) ?? new Dictionary<string, object>() }
            };
        }

        private class ShiftGroupData
        {
            public string GroupId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Color { get; set; } = "#4CAF50";
            public int MorningCapacity { get; set; } = 15;
            public int EveningCapacity { get; set; } = 15;
            public bool IsActive { get; set; } = true;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public string MorningShift { get; set; } = string.Empty;
            public string EveningShift { get; set; } = string.Empty;
        }
    }

    public class ShiftGroupManager
    {
        public Dictionary<string, ShiftGroup> ShiftGroups { get; set; } = new();
        public string DefaultGroupId { get; set; } = "default";

        public ShiftGroupManager()
        {
            // Create default group for backward compatibility
            var defaultGroup = new ShiftGroup("default", "گروه پیش‌فرض", "گروه پیش‌فرض برای سازگاری با نسخه‌های قبلی");
            ShiftGroups["default"] = defaultGroup;
        }

        public bool AddShiftGroup(string groupId, string name, string description = "", string color = "#4CAF50", 
                                 int morningCapacity = 15, int eveningCapacity = 15)
        {
            if (string.IsNullOrEmpty(groupId) || ShiftGroups.ContainsKey(groupId))
                return false;

            var group = new ShiftGroup(groupId, name, description, color, morningCapacity, eveningCapacity);
            ShiftGroups[groupId] = group;
            return true;
        }

        public bool UpdateShiftGroup(string groupId, string? name = null, string? description = null, 
                                    string? color = null, int? morningCapacity = null, int? eveningCapacity = null, 
                                    bool? isActive = null)
        {
            if (!ShiftGroups.ContainsKey(groupId))
                return false;

            ShiftGroups[groupId].Update(name, description, color, morningCapacity, eveningCapacity, isActive);
            return true;
        }

        public bool DeleteShiftGroup(string groupId)
        {
            if (groupId == "default" || !ShiftGroups.ContainsKey(groupId))
                return false;

            ShiftGroups.Remove(groupId);
            return true;
        }

        public ShiftGroup? GetShiftGroup(string groupId)
        {
            return ShiftGroups.GetValueOrDefault(groupId);
        }

        public List<ShiftGroup> GetAllShiftGroups()
        {
            return ShiftGroups.Values.ToList();
        }

        public List<ShiftGroup> GetActiveShiftGroups()
        {
            return ShiftGroups.Values.Where(g => g.IsActive).ToList();
        }

        public Shift? GetShift(string groupId, string shiftType)
        {
            var group = GetShiftGroup(groupId);
            return group?.GetShift(shiftType);
        }

        public bool AssignEmployee(Employee employee, string groupId, string shiftType)
        {
            var group = GetShiftGroup(groupId);
            if (group == null) return false;

            // Check if employee is already assigned to another group
            var currentGroup = GetEmployeeGroup(employee);
            if (currentGroup != null && currentGroup != groupId)
                return false;

            return group.AssignEmployee(employee, shiftType);
        }

        public bool AssignEmployeeToSlot(Employee employee, string groupId, string shiftType, int slotIndex)
        {
            var group = GetShiftGroup(groupId);
            if (group == null) return false;

            // Check if employee is already assigned to another group
            var currentGroup = GetEmployeeGroup(employee);
            if (currentGroup != null && currentGroup != groupId)
                return false;

            return group.AssignEmployeeToSlot(employee, shiftType, slotIndex);
        }

        public bool RemoveEmployeeFromShift(Employee employee, string groupId, string shiftType)
        {
            var group = GetShiftGroup(groupId);
            return group?.RemoveEmployeeFromShift(employee, shiftType) ?? false;
        }

        public string? GetEmployeeGroup(Employee employee)
        {
            foreach (var group in ShiftGroups.Values)
            {
                var shifts = group.GetEmployeeShifts(employee);
                if (shifts.Any())
                    return group.GroupId;
            }
            return null;
        }

        public List<string> GetEmployeeShifts(Employee employee)
        {
            var groupId = GetEmployeeGroup(employee);
            if (groupId == null) return new List<string>();

            var group = GetShiftGroup(groupId);
            return group?.GetEmployeeShifts(employee) ?? new List<string>();
        }

        public void ClearShift(string groupId, string shiftType)
        {
            var group = GetShiftGroup(groupId);
            group?.ClearShift(shiftType);
        }

        public int GetTotalAssignedEmployees()
        {
            return ShiftGroups.Values.Sum(g => g.GetTotalAssignedEmployees());
        }

        public int GetTotalCapacity()
        {
            return ShiftGroups.Values.Sum(g => g.GetTotalCapacity());
        }

        public override string ToString()
        {
            return $"ShiftGroupManager({ShiftGroups.Count} groups)";
        }

        public static ShiftGroupManager FromJson(string json, Dictionary<string, Employee> employeesDict)
        {
            try
            {
                var managerData = JsonConvert.DeserializeObject<ShiftGroupManagerData>(json);
                if (managerData != null)
                {
                    var manager = new ShiftGroupManager();
                    manager.DefaultGroupId = managerData.DefaultGroupId;

                    foreach (var groupData in managerData.ShiftGroups)
                    {
                        var group = ShiftGroup.FromJson(groupData.Value, employeesDict);
                        manager.ShiftGroups[group.GroupId] = group;
                    }

                    return manager;
                }
            }
            catch
            {
                // If that fails, try to deserialize as the actual structure being saved
            }

            // Try to deserialize as the actual structure being saved (Dictionary<string, object>)
            var managerDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (managerDict != null)
            {
                var manager = new ShiftGroupManager();

                if (managerDict.ContainsKey("default_group_id"))
                    manager.DefaultGroupId = managerDict["default_group_id"].ToString() ?? "default";

                if (managerDict.ContainsKey("shift_groups"))
                {
                    var groupsData = managerDict["shift_groups"];
                    if (groupsData is Dictionary<string, object> groupsDict)
                    {
                        foreach (var kvp in groupsDict)
                        {
                            var groupJson = JsonConvert.SerializeObject(kvp.Value);
                            var group = ShiftGroup.FromJson(groupJson, employeesDict);
                            manager.ShiftGroups[kvp.Key] = group;
                        }
                    }
                }

                return manager;
            }

            // If all else fails, return a new manager
            return new ShiftGroupManager();
        }

        public string ToJson()
        {
            var managerData = new ShiftGroupManagerData
            {
                DefaultGroupId = DefaultGroupId,
                ShiftGroups = ShiftGroups.ToDictionary(g => g.Key, g => g.Value.ToJson())
            };
            return JsonConvert.SerializeObject(managerData, Formatting.Indented);
        }

        private class ShiftGroupManagerData
        {
            public string DefaultGroupId { get; set; } = "default";
            public Dictionary<string, string> ShiftGroups { get; set; } = new();
        }
    }
}
