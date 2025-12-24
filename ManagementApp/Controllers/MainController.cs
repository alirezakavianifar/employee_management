
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared.Models;
using Shared.Services;
using Shared.Utils;
using ManagementApp.Services;

namespace ManagementApp.Controllers
{
    public class MainController
    {
        private string _dataDir;
        private JsonHandler _jsonHandler;
        private SyncManager _syncManager;
        private readonly ILogger<MainController> _logger;
        private DateTime _lastCapacityChange = DateTime.MinValue;
        private System.Threading.Timer? _rotationTimer;
        private DateTime _lastRotationCheck = DateTime.MinValue;
        private BadgeGeneratorService _badgeGenerator;

        // Data structures
        public Dictionary<string, Employee> Employees { get; private set; } = new();
        public RoleManager RoleManager { get; private set; } = new();
        public ShiftManager ShiftManager { get; private set; } = new(15); // Start with 15 instead of 5
        public ShiftGroupManager ShiftGroupManager { get; private set; } = new(); // New shift group manager
        public AbsenceManager AbsenceManager { get; private set; } = new();
        public TaskManager TaskManager { get; private set; } = new();
        public Dictionary<string, object> Settings { get; private set; } = new();
        public string SelectedDisplayGroupId { get; set; } = "default";

        // Events
        public event Action? EmployeesUpdated;
        public event Action? RolesUpdated;
        public event Action? ShiftsUpdated;
        public event Action? ShiftGroupsUpdated;
        public event Action? AbsencesUpdated;
        public event Action? TasksUpdated;
        public event Action? SettingsUpdated;
        public event Action? SyncTriggered;

        public void NotifySettingsUpdated()
        {
            SettingsUpdated?.Invoke();
        }

        public MainController(string dataDir = "Data")
        {
            // Use configuration system to get data directory
            var config = Shared.Utils.AppConfigHelper.Config;
            _dataDir = config.DataDirectory;
            _jsonHandler = new JsonHandler(_dataDir);
            _syncManager = new SyncManager(_dataDir);
            _logger = LoggingService.CreateLogger<MainController>();
            _badgeGenerator = new BadgeGeneratorService();

            InitializeSettings();
            LoadData();
            SetupSync();
            SetupRotationScheduler();
            
            // Subscribe to configuration changes
            Shared.Utils.AppConfigHelper.ConfigurationChanged += OnConfigurationChanged;
        }

        private void InitializeSettings()
        {
            Settings = new Dictionary<string, object>
            {
                { "shift_capacity", 15 }, // Start with 15 instead of 5
                { "morning_capacity", 15 },
                { "evening_capacity", 15 },
                { "shared_folder_path", _dataDir },
                { "managers", new List<object>() },
                { "badge_template_path", Path.Combine(_dataDir, "BadgeTemplate.png") }
            };
        }

        private void SetupSync()
        {
            _syncManager.AddSyncCallback(OnSyncTriggered);
            _syncManager.StartSync();
        }

        private void SetupRotationScheduler()
        {
            // Check for rotation every hour
            _rotationTimer = new System.Threading.Timer(CheckRotationSchedule, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }

        private void CheckRotationSchedule(object? state)
        {
            try
            {
                if (Settings.TryGetValue("auto_rotate_shifts", out var autoRotate) && autoRotate is bool enabled && enabled)
                {
                    var today = DateTime.Now;
                    var rotationDay = Settings.GetValueOrDefault("auto_rotate_day", "Saturday").ToString() ?? "Saturday";
                    
                    // Check if today is the rotation day and we haven't rotated today
                    if (today.DayOfWeek.ToString() == rotationDay && _lastRotationCheck.Date < today.Date)
                    {
                        _logger.LogInformation("Automatic shift rotation triggered for {Day}", rotationDay);
                        
                        // Rotate shifts for all active groups
                        foreach (var group in ShiftGroupManager.GetActiveShiftGroups())
                        {
                            SwapShifts(group.GroupId);
                        }
                        
                        _lastRotationCheck = today;
                        SaveData();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rotation schedule");
            }
        }

        private void LoadData()
        {
            try
            {
                _logger.LogInformation("Loading data from JSON files. Data directory: {DataDir}", _dataDir);

                // Load today's report
                var reportData = _jsonHandler.EnsureTodayReportExists();
                if (reportData == null)
                {
                    _logger.LogError("Failed to load or create today's report");
                    return;
                }

                var employeeCount = (reportData.GetValueOrDefault("employees") as List<object>)?.Count ?? 0;
                var managerCount = (reportData.GetValueOrDefault("managers") as List<object>)?.Count ?? 0;
                
                _logger.LogInformation("Successfully loaded report data with {EmployeeCount} employees and {ManagerCount} managers", 
                    employeeCount, managerCount);
                
                // Log all available keys in the report data for debugging
                _logger.LogInformation("Report data keys: {Keys}", string.Join(", ", reportData.Keys));

                // Load settings
                if (reportData.ContainsKey("settings"))
                {
                    var settingsData = reportData["settings"] as Dictionary<string, object>;
                    if (settingsData != null)
                    {
                        foreach (var kvp in settingsData)
                        {
                            Settings[kvp.Key] = kvp.Value;
                        }
                        
                        // Load selected display group from settings
                        if (Settings.ContainsKey("selected_display_group_id") && Settings["selected_display_group_id"] is string savedGroupId)
                        {
                            SelectedDisplayGroupId = savedGroupId;
                            _logger.LogInformation("Loaded selected display group from settings: {GroupId}", savedGroupId);
                        }
                        else
                        {
                            _logger.LogInformation("No saved display group found, using default: {GroupId}", SelectedDisplayGroupId);
                        }

                        // Ensure shift manager capacity matches settings
                        if (Settings.ContainsKey("shift_capacity") && Settings["shift_capacity"] is int capacity)
                        {
                            // Check if capacity was recently changed by user (within last 60 seconds)
                            var timeSinceCapacityChange = DateTime.Now - _lastCapacityChange;
                            if (timeSinceCapacityChange.TotalSeconds < 60.0)
                            {
                                _logger.LogInformation("Preserving user's capacity change ({UserCapacity}) over loaded capacity ({LoadedCapacity})", 
                                    ShiftManager.Capacity, capacity);
                                // Keep the user's capacity and update settings to match
                                Settings["shift_capacity"] = ShiftManager.Capacity;
                                Settings["morning_capacity"] = ShiftManager.Capacity;
                                Settings["evening_capacity"] = ShiftManager.Capacity;
                            }
                            else
                            {
                                ShiftManager.SetCapacity(capacity);
                                Settings["morning_capacity"] = capacity;
                                Settings["evening_capacity"] = capacity;
                            }
                        }
                        else
                        {
                            Settings["shift_capacity"] = ShiftManager.Capacity;
                            Settings["morning_capacity"] = ShiftManager.Capacity;
                            Settings["evening_capacity"] = ShiftManager.Capacity;
                        }
                    }
                }

                // Load employees from both employees and managers lists
                if (reportData.ContainsKey("employees"))
                {
                    var employeesData = reportData["employees"];
                    var employeesList = new List<object>();
                    
                    if (employeesData is List<object> empList)
                    {
                        employeesList = empList;
                    }
                    else if (employeesData is Newtonsoft.Json.Linq.JArray empJArray)
                    {
                        employeesList = empJArray.ToObject<List<object>>() ?? new List<object>();
                    }
                    
                    LoadEmployeesFromList(employeesList);
                }

                if (reportData.ContainsKey("managers"))
                {
                    var managersData = reportData["managers"];
                    var managersList = new List<object>();
                    
                    if (managersData is List<object> mgrList)
                    {
                        managersList = mgrList;
                    }
                    else if (managersData is Newtonsoft.Json.Linq.JArray mgrJArray)
                    {
                        managersList = mgrJArray.ToObject<List<object>>() ?? new List<object>();
                    }
                    
                    LoadEmployeesFromList(managersList);
                }

                // Load shifts
                if (reportData.ContainsKey("shifts"))
                {
                    LoadShiftsFromData(reportData["shifts"]);
                }

                // Load shift groups
                if (reportData.ContainsKey("shift_groups"))
                {
                    LoadShiftGroupsFromData(reportData["shift_groups"]);
                }

                // Load absences
                if (reportData.ContainsKey("absences"))
                {
                    LoadAbsencesFromData(reportData["absences"]);
                }

                // Load roles
                if (reportData.ContainsKey("roles"))
                {
                    LoadRolesFromData(reportData["roles"]);
                }

                // Load tasks
                if (reportData.ContainsKey("tasks"))
                {
                    LoadTasksFromData(reportData["tasks"]);
                }

                // Ensure minimum capacity of 15 after all data is loaded
                if (ShiftManager.Capacity <= 0)
                {
                    ShiftManager.SetCapacity(15);
                }

                _logger.LogInformation("Data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data");
                ShowErrorDialog("خطا در بارگذاری داده‌ها", ex.Message);
            }
        }

        private void LoadEmployeesFromList(List<object> employeesData)
        {
            _logger.LogInformation("LoadEmployeesFromList called with {Count} employees", employeesData.Count);
            
            foreach (var employeeObj in employeesData)
            {
                try
                {
                    _logger.LogInformation("Processing employee object of type: {Type}", employeeObj?.GetType().Name ?? "null");
                    
                    Dictionary<string, object> employeeDict = null;
                    
                    if (employeeObj is Dictionary<string, object> dict)
                    {
                        employeeDict = dict;
                    }
                    else if (employeeObj is Newtonsoft.Json.Linq.JObject jObj)
                    {
                        employeeDict = jObj.ToObject<Dictionary<string, object>>();
                    }
                    else if (employeeObj is Newtonsoft.Json.Linq.JToken jToken)
                    {
                        // Handle JToken objects that might be nested
                        var jObject = jToken as Newtonsoft.Json.Linq.JObject ?? jToken.ToObject<Newtonsoft.Json.Linq.JObject>();
                        if (jObject != null)
                        {
                            employeeDict = jObject.ToObject<Dictionary<string, object>>();
                        }
                    }
                    
                    if (employeeDict == null)
                    {
                        _logger.LogWarning("Employee object could not be converted to Dictionary, skipping. Object type: {Type}, Value: {Value}", 
                            employeeObj?.GetType().Name ?? "null", 
                            employeeObj?.ToString()?.Substring(0, Math.Min(100, employeeObj.ToString()?.Length ?? 0)) ?? "null");
                        continue;
                    }

                    var employeeId = employeeDict.GetValueOrDefault("employee_id", "").ToString() ?? "";
                    if (string.IsNullOrEmpty(employeeId))
                    {
                        _logger.LogWarning("Employee object missing employee_id, skipping. Available keys: {Keys}", 
                            string.Join(", ", employeeDict.Keys));
                        continue;
                    }

                    // Skip if employee already exists
                    if (Employees.ContainsKey(employeeId))
                    {
                        _logger.LogInformation("Employee {EmployeeId} already exists, skipping", employeeId);
                        continue;
                    }

                    var firstName = employeeDict.GetValueOrDefault("first_name", "").ToString() ?? "";
                    var lastName = employeeDict.GetValueOrDefault("last_name", "").ToString() ?? "";
                    
                    // Handle both old "role" and new "role_id" fields for backward compatibility
                    var roleId = employeeDict.GetValueOrDefault("role_id", "").ToString() ?? "";
                    if (string.IsNullOrEmpty(roleId))
                    {
                        roleId = employeeDict.GetValueOrDefault("role", "employee").ToString() ?? "employee";
                    }
                    
                    // Handle shift group assignment
                    var shiftGroupId = employeeDict.GetValueOrDefault("shift_group_id", "default").ToString() ?? "default";
                    
                    var photoPath = employeeDict.GetValueOrDefault("photo_path", "").ToString() ?? "";
                    var isManager = employeeDict.GetValueOrDefault("is_manager", false);
                    bool isManagerBool = false;
                    if (isManager is bool b)
                        isManagerBool = b;
                    else if (isManager is string s && bool.TryParse(s, out bool parsed))
                        isManagerBool = parsed;

                    var employee = new Employee(employeeId, firstName, lastName, roleId, shiftGroupId, photoPath, isManagerBool);

                    // Load new properties (shield_color, show_shield, sticker_paths, medal_badge_path, personnel_id)
                    if (employeeDict.TryGetValue("shield_color", out var shieldColorObj))
                    {
                        employee.ShieldColor = shieldColorObj?.ToString() ?? "Blue";
                    }
                    
                    if (employeeDict.TryGetValue("show_shield", out var showShieldObj))
                    {
                        if (showShieldObj is bool showShieldBool)
                        {
                            employee.ShowShield = showShieldBool;
                        }
                        else if (bool.TryParse(showShieldObj?.ToString(), out bool showShieldParsed))
                        {
                            employee.ShowShield = showShieldParsed;
                        }
                    }
                    // Default to true if not specified (for backward compatibility)
                    
                    if (employeeDict.TryGetValue("sticker_paths", out var stickerPathsObj))
                    {
                        if (stickerPathsObj is List<object> stickerList)
                        {
                            employee.StickerPaths = stickerList.Select(s => s?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
                        }
                        else if (stickerPathsObj is Newtonsoft.Json.Linq.JArray jArray)
                        {
                            employee.StickerPaths = jArray.ToObject<List<string>>() ?? new List<string>();
                        }
                    }
                    
                    if (employeeDict.TryGetValue("medal_badge_path", out var medalBadgePathObj))
                    {
                        employee.MedalBadgePath = medalBadgePathObj?.ToString() ?? "";
                    }
                    
                    if (employeeDict.TryGetValue("personnel_id", out var personnelIdObj))
                    {
                        employee.PersonnelId = personnelIdObj?.ToString() ?? "";
                    }

                    // Set creation/update times if available, with validation for Persian calendar
                    if (employeeDict.ContainsKey("created_at"))
                    {
                        try
                        {
                            var createdAtValue = employeeDict["created_at"];
                            DateTime createdAt;
                            
                            if (createdAtValue is DateTime dt)
                            {
                                createdAt = dt;
                            }
                            else
                            {
                                var createdAtStr = createdAtValue?.ToString() ?? "";
                                if (!DateTime.TryParse(createdAtStr, out createdAt))
                                {
                                    _logger.LogWarning("Failed to parse created_at for employee {EmployeeId}, using current time", employeeId);
                                    createdAt = DateTime.Now;
                                }
                            }
                            
                            if (IsValidGeorgianDate(createdAt))
                            {
                                employee.CreatedAt = createdAt;
                            }
                            else
                            {
                                _logger.LogWarning("Invalid created_at date for employee {EmployeeId}, using current time", employeeId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing created_at for employee {EmployeeId}, using current time", employeeId);
                        }
                    }
                    
                    if (employeeDict.ContainsKey("updated_at"))
                    {
                        try
                        {
                            var updatedAtValue = employeeDict["updated_at"];
                            DateTime updatedAt;
                            
                            if (updatedAtValue is DateTime dt)
                            {
                                updatedAt = dt;
                            }
                            else
                            {
                                var updatedAtStr = updatedAtValue?.ToString() ?? "";
                                if (!DateTime.TryParse(updatedAtStr, out updatedAt))
                                {
                                    _logger.LogWarning("Failed to parse updated_at for employee {EmployeeId}, using current time", employeeId);
                                    updatedAt = DateTime.Now;
                                }
                            }
                            
                            if (IsValidGeorgianDate(updatedAt))
                            {
                                employee.UpdatedAt = updatedAt;
                            }
                            else
                            {
                                _logger.LogWarning("Invalid updated_at date for employee {EmployeeId}, using current time", employeeId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error processing updated_at for employee {EmployeeId}, using current time", employeeId);
                        }
                    }

                    Employees[employeeId] = employee;
                    _logger.LogInformation("Successfully loaded employee: {EmployeeId} - {FullName}", employeeId, employee.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading employee from data: {EmployeeData}", employeeObj);
                }
            }
            
            _logger.LogInformation("LoadEmployeesFromList completed. Total employees loaded: {Count}", Employees.Count);
        }

        private bool IsValidGeorgianDate(DateTime date)
        {
            try
            {
                // Check if the date is within valid range for Georgian calendar
                var year = date.Year;
                return year >= 1 && year <= 9999; // Valid Georgian calendar range
            }
            catch
            {
                return false;
            }
        }

        private void LoadShiftsFromData(object shiftsData)
        {
            try
            {
                _logger.LogInformation("Loading shifts from data. Data type: {Type}", shiftsData?.GetType().Name ?? "null");
                
                var shiftsJson = JsonConvert.SerializeObject(shiftsData);
                _logger.LogInformation("Shifts JSON: {ShiftsJson}", shiftsJson);
                
                ShiftManager = ShiftManager.FromJson(shiftsJson, Employees);
                
                // Log shift assignments after loading
                var morningCount = ShiftManager.MorningShift.AssignedEmployees.Count(emp => emp != null);
                var eveningCount = ShiftManager.EveningShift.AssignedEmployees.Count(emp => emp != null);
                _logger.LogInformation("Loaded shifts - Morning: {MorningCount}, Evening: {EveningCount}, Capacity: {Capacity}", 
                    morningCount, eveningCount, ShiftManager.Capacity);
                
                // Ensure minimum capacity of 15
                if (ShiftManager.Capacity <= 0)
                {
                    ShiftManager.SetCapacity(15);
                    _logger.LogInformation("Set minimum capacity to 15");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shifts from data: {ShiftsData}", shiftsData);
                // If loading fails, ensure we have a valid ShiftManager with capacity 15
                ShiftManager = new ShiftManager(15);
                _logger.LogInformation("Created new ShiftManager with capacity 15 due to loading error");
            }
        }

        private void LoadShiftGroupsFromData(object shiftGroupsData)
        {
            try
            {
                _logger.LogInformation("Loading shift groups from data. Data type: {Type}", shiftGroupsData?.GetType().Name ?? "null");
                
                var shiftGroupsJson = JsonConvert.SerializeObject(shiftGroupsData);
                _logger.LogInformation("Shift Groups JSON: {ShiftGroupsJson}", shiftGroupsJson);
                
                ShiftGroupManager = ShiftGroupManager.FromJson(shiftGroupsJson, Employees);
                
                // Migration: Copy default group team leader to ShiftManager shifts if they don't have one
                // Note: TeamLeaderId functionality not yet implemented
                // var defaultGroup = ShiftGroupManager.GetShiftGroup("default");
                // if (defaultGroup != null && !string.IsNullOrEmpty(defaultGroup.TeamLeaderId))
                // {
                //     if (string.IsNullOrEmpty(ShiftManager.MorningShift.TeamLeaderId))
                //     {
                //         ShiftManager.MorningShift.SetTeamLeader(defaultGroup.TeamLeaderId);
                //         _logger.LogInformation("Migrated default group team leader to morning shift");
                //     }
                //     if (string.IsNullOrEmpty(ShiftManager.EveningShift.TeamLeaderId))
                //     {
                //         ShiftManager.EveningShift.SetTeamLeader(defaultGroup.TeamLeaderId);
                //         _logger.LogInformation("Migrated default group team leader to evening shift");
                //     }
                // }
                
                // Log shift group assignments after loading
                var totalGroups = ShiftGroupManager.GetAllShiftGroups().Count;
                var totalAssigned = ShiftGroupManager.GetTotalAssignedEmployees();
                _logger.LogInformation("Loaded shift groups - Total Groups: {TotalGroups}, Total Assigned: {TotalAssigned}", 
                    totalGroups, totalAssigned);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shift groups from data: {ShiftGroupsData}", shiftGroupsData);
                // If loading fails, ensure we have a valid ShiftGroupManager
                ShiftGroupManager = new ShiftGroupManager();
                _logger.LogInformation("Created new ShiftGroupManager due to loading error");
            }
        }

        private void LoadAbsencesFromData(object absencesData)
        {
            try
            {
                var absencesJson = JsonConvert.SerializeObject(absencesData);
                var loadedAbsenceManager = AbsenceManager.FromJson(absencesJson, Employees);
                
                // Ensure the loaded manager is valid, otherwise keep the existing one
                if (loadedAbsenceManager != null && loadedAbsenceManager.Absences != null)
                {
                    AbsenceManager = loadedAbsenceManager;
                }
                else
                {
                    _logger.LogWarning("Failed to load absences from data, keeping existing AbsenceManager");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading absences");
            }
        }

        private void LoadRolesFromData(object rolesData)
        {
            try
            {
                var rolesJson = JsonConvert.SerializeObject(rolesData);
                var loadedRoleManager = RoleManager.FromJson(rolesJson);
                
                // Ensure the loaded manager is valid, otherwise keep the existing one
                if (loadedRoleManager != null && loadedRoleManager.Roles != null)
                {
                    RoleManager = loadedRoleManager;
                    _logger.LogInformation("Loaded {Count} roles", RoleManager.GetRoleCount());
                }
                else
                {
                    _logger.LogWarning("Failed to load roles from data, keeping existing RoleManager");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roles");
            }
        }

        private void LoadTasksFromData(object tasksData)
        {
            try
            {
                _logger.LogInformation("LoadTasksFromData: Starting to load tasks from data, type: {Type}", 
                    tasksData?.GetType().Name ?? "null");
                
                Dictionary<string, object> tasksDict = null;
                
                // Handle different data types
                if (tasksData is Dictionary<string, object> dict)
                {
                    tasksDict = dict;
                }
                else if (tasksData is Newtonsoft.Json.Linq.JObject jObject)
                {
                    // Convert JObject to Dictionary
                    tasksDict = jObject.ToObject<Dictionary<string, object>>();
                    _logger.LogInformation("LoadTasksFromData: Converted JObject to Dictionary");
                }
                
                if (tasksDict != null)
                {
                    _logger.LogInformation("LoadTasksFromData: Tasks data is Dictionary with {Count} keys: {Keys}", 
                        tasksDict.Count, string.Join(", ", tasksDict.Keys));
                    
                    // Check if this is the nested structure from TaskManager.ToJson()
                    if (tasksDict.ContainsKey("Tasks") && tasksDict.ContainsKey("NextTaskId"))
                    {
                        _logger.LogInformation("LoadTasksFromData: Detected nested TaskManager structure");
                        
                        // This is the structure from TaskManager.ToJson()
                        var tasksJson = JsonConvert.SerializeObject(tasksDict);
                        TaskManager = TaskManager.FromJson(tasksJson);
                        _logger.LogInformation("LoadTasksFromData: Loaded TaskManager with {Count} tasks", TaskManager.Tasks.Count);
                    }
                    else
                    {
                        // This is the flat structure (legacy or direct task storage)
                        _logger.LogInformation("LoadTasksFromData: Detected flat task structure");
                        
                        // Create a new TaskManager
                        TaskManager = new TaskManager();
                        
                        // Load NextTaskId if available
                        if (tasksDict.TryGetValue("NextTaskId", out var nextTaskIdObj))
                        {
                            if (int.TryParse(nextTaskIdObj.ToString(), out int nextTaskId))
                            {
                                TaskManager.NextTaskId = nextTaskId;
                                _logger.LogInformation("LoadTasksFromData: Set NextTaskId to {NextTaskId}", nextTaskId);
                            }
                        }
                        
                        // Load individual tasks
                        foreach (var kvp in tasksDict)
                        {
                            if (kvp.Key == "NextTaskId") continue; // Skip NextTaskId, we already handled it
                            
                            var taskId = kvp.Key;
                            var taskJson = kvp.Value?.ToString();
                            
                            if (!string.IsNullOrEmpty(taskJson))
                            {
                                try
                                {
                                    var task = Shared.Models.Task.FromJson(taskJson);
                                    TaskManager.Tasks[taskId] = task;
                                    _logger.LogInformation("LoadTasksFromData: Successfully loaded task {TaskId}: {Title}", 
                                        taskId, task.Title);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "LoadTasksFromData: Error loading task {TaskId}: {TaskJson}", 
                                        taskId, taskJson);
                                }
                            }
                        }
                        
                        _logger.LogInformation("LoadTasksFromData: Successfully loaded {Count} tasks", TaskManager.Tasks.Count);
                    }
                }
                else
                {
                    _logger.LogWarning("LoadTasksFromData: Tasks data is not a Dictionary or JObject, type: {Type}", 
                        tasksData?.GetType().Name ?? "null");
                    
                    // Fallback: try to deserialize as JSON string
                    var tasksJson = JsonConvert.SerializeObject(tasksData);
                    TaskManager = TaskManager.FromJson(tasksJson);
                }
                
                TasksUpdated?.Invoke();
                _logger.LogInformation("LoadTasksFromData: TasksUpdated event invoked");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tasks");
                // Create a new TaskManager if loading fails
                TaskManager = new TaskManager();
            }
        }

        private Dictionary<string, object> CreateShiftsData()
        {
            try
            {
                // Create shifts data in the format expected by Display App
                // Include both default shifts and shift group data
                // Get team leader from each shift individually (per-shift foremen)
                // Note: TeamLeaderId functionality not yet implemented
                var morningTeamLeaderId = string.Empty; // ShiftManager.MorningShift.TeamLeaderId ?? string.Empty;
                var eveningTeamLeaderId = string.Empty; // ShiftManager.EveningShift.TeamLeaderId ?? string.Empty;
                
                var shiftsData = new Dictionary<string, object>
                {
                    { "morning", new Dictionary<string, object>
                        {
                            { "shift_type", "morning" },
                            { "capacity", ShiftManager.MorningShift.Capacity },
                            { "team_leader_id", morningTeamLeaderId },
                            { "assigned_employees", ShiftManager.MorningShift.AssignedEmployees
                                .Where(emp => emp != null)
                                .Select(emp => emp.ToDictionary())
                                .Cast<object>()
                                .ToList()
                            }
                        }
                    },
                    { "evening", new Dictionary<string, object>
                        {
                            { "shift_type", "evening" },
                            { "capacity", ShiftManager.EveningShift.Capacity },
                            { "team_leader_id", eveningTeamLeaderId },
                            { "assigned_employees", ShiftManager.EveningShift.AssignedEmployees
                                .Where(emp => emp != null)
                                .Select(emp => emp.ToDictionary())
                                .Cast<object>()
                                .ToList()
                            }
                        }
                    },
                    { "selected_group", CreateSelectedGroupData() }
                };

                var morningShift = shiftsData["morning"] as Dictionary<string, object>;
                var eveningShift = shiftsData["evening"] as Dictionary<string, object>;
                var morningCount = (morningShift?["assigned_employees"] as List<object>)?.Count ?? 0;
                var eveningCount = (eveningShift?["assigned_employees"] as List<object>)?.Count ?? 0;
                
                _logger.LogInformation("Created shifts data with {MorningCount} morning and {EveningCount} evening employees", 
                    morningCount, eveningCount);

                return shiftsData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shifts data");
                return new Dictionary<string, object>
                {
                    { "morning", new Dictionary<string, object>
                        {
                            { "shift_type", "morning" },
                            { "capacity", 15 },
                            { "assigned_employees", new List<object>() }
                        }
                    },
                    { "evening", new Dictionary<string, object>
                        {
                            { "shift_type", "evening" },
                            { "capacity", 15 },
                            { "assigned_employees", new List<object>() }
                        }
                    },
                    { "selected_group", new Dictionary<string, object>() }
                };
            }
        }

        private Dictionary<string, object> CreateSelectedGroupData()
        {
            try
            {
                var selectedGroup = ShiftGroupManager.GetShiftGroup(SelectedDisplayGroupId);
                if (selectedGroup == null)
                {
                    _logger.LogWarning("Selected display group not found: {GroupId}, falling back to default", SelectedDisplayGroupId);
                    selectedGroup = ShiftGroupManager.GetShiftGroup("default");
                }

                if (selectedGroup == null)
                {
                    _logger.LogError("No shift groups available, including default");
                    return new Dictionary<string, object>();
                }

                // Get team leaders from each shift individually (per-shift foremen)
                // Note: TeamLeaderId functionality not yet implemented
                var morningTeamLeaderId = string.Empty; // selectedGroup.MorningShift.TeamLeaderId ?? string.Empty;
                var eveningTeamLeaderId = string.Empty; // selectedGroup.EveningShift.TeamLeaderId ?? string.Empty;
                
                var groupData = new Dictionary<string, object>
                {
                    { "group_id", selectedGroup.GroupId },
                    { "name", selectedGroup.Name },
                    { "description", selectedGroup.Description },
                    { "color", selectedGroup.Color },
                    { "is_active", selectedGroup.IsActive },
                    { "morning_capacity", selectedGroup.MorningCapacity },
                    { "evening_capacity", selectedGroup.EveningCapacity },
                    { "morning_shift", new Dictionary<string, object>
                        {
                            { "shift_type", "morning" },
                            { "capacity", selectedGroup.MorningCapacity },
                            { "team_leader_id", morningTeamLeaderId },
                            { "assigned_employees", selectedGroup.MorningShift.AssignedEmployees
                                .Where(emp => emp != null)
                                .Select(emp => emp.ToDictionary())
                                .Cast<object>()
                                .ToList()
                            }
                        }
                    },
                    { "evening_shift", new Dictionary<string, object>
                        {
                            { "shift_type", "evening" },
                            { "capacity", selectedGroup.EveningCapacity },
                            { "team_leader_id", eveningTeamLeaderId },
                            { "assigned_employees", selectedGroup.EveningShift.AssignedEmployees
                                .Where(emp => emp != null)
                                .Select(emp => emp.ToDictionary())
                                .Cast<object>()
                                .ToList()
                            }
                        }
                    }
                };
                
                _logger.LogInformation("Created selected group data for group: {GroupId} ({GroupName})", 
                    selectedGroup.GroupId, selectedGroup.Name);
                return groupData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating selected group data");
                return new Dictionary<string, object>();
            }
        }

        public bool SaveData()
        {
            try
            {
                _syncManager.NotifySave();

                // Determine managers to display
                var managersToDisplay = new List<object>();

                // First, check if user has selected specific managers in settings
                if (Settings.ContainsKey("managers") && Settings["managers"] is List<object> managers && managers.Count > 0)
                {
                    _logger.LogInformation("Using specific managers from settings: {Count} managers", managers.Count);
                    foreach (var managerObj in managers)
                    {
                        var managerDict = managerObj as Dictionary<string, object>;
                        if (managerDict?.ContainsKey("employee_id") == true)
                        {
                            var employeeId = managerDict["employee_id"].ToString();
                            if (!string.IsNullOrEmpty(employeeId) && Employees.ContainsKey(employeeId))
                            {
                                managersToDisplay.Add(Employees[employeeId].ToDictionary());
                            }
                        }
                    }
                }
                else
                {
                    // Fallback to auto-categorized managers based on IsManager property and role
                    var managersByProperty = Employees.Values.Where(emp => emp.IsManager).ToList();
                    var managersByRole = Employees.Values
                        .Where(emp => emp.Role.ToLower().StartsWith("مدیر") || emp.Role.ToLower().StartsWith("manager"))
                        .ToList();
                    
                    // Combine both approaches and remove duplicates
                    var allManagers = managersByProperty.Union(managersByRole).Distinct().ToList();
                    
                    _logger.LogInformation("Auto-categorized managers: {Count} by IsManager property, {Count} by role, {Count} total", 
                        managersByProperty.Count, managersByRole.Count, allManagers.Count);
                    
                    managersToDisplay = allManagers
                        .Select(emp => emp.ToDictionary())
                        .Cast<object>()
                        .ToList();
                }

                // Prepare report data
                var reportData = new Dictionary<string, object>
                {
                    { "date", DateTime.Now.ToString("yyyy-MM-dd") },
                    { "employees", Employees.Values.Where(emp => !emp.IsManager && !emp.Role.ToLower().StartsWith("مدیر") && !emp.Role.ToLower().StartsWith("manager")).Select(emp => emp.ToDictionary()).Cast<object>().ToList() },
                    { "managers", managersToDisplay },
                    { "roles", JsonConvert.DeserializeObject<Dictionary<string, object>>(RoleManager.ToJson()) ?? new Dictionary<string, object>() },
                    { "shifts", CreateShiftsData() },
                    { "shift_groups", JsonConvert.DeserializeObject<Dictionary<string, object>>(ShiftGroupManager.ToJson()) ?? new Dictionary<string, object>() },
                    { "absences", JsonConvert.DeserializeObject<Dictionary<string, object>>(AbsenceManager.ToJson()) ?? new Dictionary<string, object>() },
                    { "tasks", JsonConvert.DeserializeObject<Dictionary<string, object>>(TaskManager.ToJson()) ?? new Dictionary<string, object>() },
                    { "settings", Settings },
                    { "last_modified", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
                };

                var success = _jsonHandler.WriteTodayReport(reportData);
                if (success)
                {
                    _logger.LogInformation("Data saved successfully");
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to save data");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving data");
                ShowErrorDialog("خطا در ذخیره داده‌ها", ex.Message);
                return false;
            }
        }

        // Role Management Methods
        public bool AddRole(string roleId, string name, string description = "", string color = "#4CAF50", int priority = 0)
        {
            try
            {
                var success = RoleManager.AddRole(roleId, name, description, color, priority);
                if (success)
                {
                    RolesUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Role added: {Name}", name);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding role");
                ShowErrorDialog("خطا در افزودن نقش", ex.Message);
                return false;
            }
        }

        public bool UpdateRole(string roleId, string? name = null, string? description = null, string? color = null, int? priority = null, bool? isActive = null)
        {
            try
            {
                var success = RoleManager.UpdateRole(roleId, name, description, color, priority, isActive);
                if (success)
                {
                    RolesUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Role updated: {RoleId}", roleId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating role");
                ShowErrorDialog("خطا در بروزرسانی نقش", ex.Message);
                return false;
            }
        }

        public bool DeleteRole(string roleId)
        {
            try
            {
                var success = RoleManager.DeleteRole(roleId);
                if (success)
                {
                    RolesUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Role deleted: {RoleId}", roleId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role");
                ShowErrorDialog("خطا در حذف نقش", ex.Message);
                return false;
            }
        }

        public Role? GetRole(string roleId)
        {
            return RoleManager.GetRole(roleId);
        }

        public List<Role> GetAllRoles()
        {
            return RoleManager.GetAllRoles();
        }

        public List<Role> GetActiveRoles()
        {
            return RoleManager.GetActiveRoles();
        }

        // Shift Group Management Methods
        public bool AddShiftGroup(string groupId, string name, string description = "", string supervisorName = "", string color = "#4CAF50", 
                                 int morningCapacity = 15, int eveningCapacity = 15)
        {
            try
            {
                var success = ShiftGroupManager.AddShiftGroup(groupId, name, description, color, morningCapacity, eveningCapacity);
                if (success)
                {
                    ShiftGroupsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Shift group added: {Name}", name);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding shift group");
                ShowErrorDialog("خطا در افزودن گروه شیفت", ex.Message);
                return false;
            }
        }

        public bool UpdateShiftGroup(string groupId, string? name = null, string? description = null, string? supervisorName = null,
                                    string? color = null, int? morningCapacity = null, int? eveningCapacity = null, 
                                    bool? isActive = null)
        {
            try
            {
                var success = ShiftGroupManager.UpdateShiftGroup(groupId, name, description, color, morningCapacity, eveningCapacity, isActive);
                if (success)
                {
                    ShiftGroupsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Shift group updated: {GroupId}", groupId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shift group");
                ShowErrorDialog("خطا در بروزرسانی گروه شیفت", ex.Message);
                return false;
            }
        }

        public bool DeleteShiftGroup(string groupId)
        {
            try
            {
                var success = ShiftGroupManager.DeleteShiftGroup(groupId);
                if (success)
                {
                    ShiftGroupsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Shift group deleted: {GroupId}", groupId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shift group");
                ShowErrorDialog("خطا در حذف گروه شیفت", ex.Message);
                return false;
            }
        }

        public ShiftGroup? GetShiftGroup(string groupId)
        {
            return ShiftGroupManager.GetShiftGroup(groupId);
        }

        public List<ShiftGroup> GetAllShiftGroups()
        {
            return ShiftGroupManager.GetAllShiftGroups();
        }

        public List<ShiftGroup> GetActiveShiftGroups()
        {
            return ShiftGroupManager.GetActiveShiftGroups();
        }

        public bool SetSelectedDisplayGroup(string groupId)
        {
            try
            {
                // Validate that the group exists
                var group = ShiftGroupManager.GetShiftGroup(groupId);
                if (group == null)
                {
                    _logger.LogWarning("Attempted to set non-existent group as display group: {GroupId}", groupId);
                    return false;
                }

                SelectedDisplayGroupId = groupId;
                
                // Save to settings for persistence
                Settings["selected_display_group_id"] = groupId;
                
                _logger.LogInformation("Selected display group changed to: {GroupId}", groupId);
                
                // Trigger data save to update reports
                SaveData();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting selected display group");
                return false;
            }
        }

        public string GetSelectedDisplayGroupId()
        {
            return SelectedDisplayGroupId;
        }

        // Employee Management Methods
        public bool AddEmployee(string firstName, string lastName, string roleId = "employee", string shiftGroupId = "default", string photoPath = "", bool isManager = false,
                               string shieldColor = "Blue", bool showShield = true, List<string>? stickerPaths = null, string medalBadgePath = "", string personnelId = "")
        {
            try
            {
                var employeeId = $"emp_{Employees.Count}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                var employee = new Employee(employeeId, firstName, lastName, roleId, shiftGroupId, photoPath, isManager);
                employee.ShieldColor = shieldColor;
                employee.ShowShield = showShield;
                employee.StickerPaths = stickerPaths ?? new List<string>();
                employee.MedalBadgePath = medalBadgePath;
                employee.PersonnelId = personnelId;

                // Create worker folder structure
                var workerFolder = GetWorkerFolderPath(firstName, lastName);
                if (!Directory.Exists(workerFolder))
                {
                    Directory.CreateDirectory(workerFolder);
                    _logger.LogInformation("Created worker folder: {Folder}", workerFolder);
                }

                // If photo path is provided and file exists, copy it to worker folder
                if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
                {
                    var photoFileName = $"{firstName}_{lastName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}{Path.GetExtension(photoPath)}";
                    var destPhotoPath = Path.Combine(workerFolder, photoFileName);
                    File.Copy(photoPath, destPhotoPath, true);
                    employee.PhotoPath = destPhotoPath;
                    _logger.LogInformation("Copied photo to worker folder: {Path}", destPhotoPath);
                }

                Employees[employeeId] = employee;
                EmployeesUpdated?.Invoke();
                SaveData();

                _logger.LogInformation("Employee added: {FullName}", employee.FullName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding employee");
                ShowErrorDialog("خطا در افزودن کارمند", ex.Message);
                return false;
            }
        }

        public bool UpdateEmployee(string employeeId, string? firstName = null, string? lastName = null, string? roleId = null, string? shiftGroupId = null, string? photoPath = null, bool? isManager = null,
                                  string? shieldColor = null, bool? showShield = null, List<string>? stickerPaths = null, string? medalBadgePath = null, string? personnelId = null)
        {
            try
            {
                if (!Employees.ContainsKey(employeeId))
                    return false;

                var employee = Employees[employeeId];
                var oldFirstName = employee.FirstName;
                var oldLastName = employee.LastName;
                var newFirstName = firstName ?? oldFirstName;
                var newLastName = lastName ?? oldLastName;

                // Update worker folder if name changed
                if ((firstName != null || lastName != null) && (oldFirstName != newFirstName || oldLastName != newLastName))
                {
                    var oldFolder = GetWorkerFolderPath(oldFirstName, oldLastName);
                    var newFolder = GetWorkerFolderPath(newFirstName, newLastName);
                    
                    if (Directory.Exists(oldFolder) && oldFolder != newFolder)
                    {
                        if (Directory.Exists(newFolder))
                        {
                            // Move files from old folder to new folder
                            foreach (var file in Directory.GetFiles(oldFolder))
                            {
                                var fileName = Path.GetFileName(file);
                                var destPath = Path.Combine(newFolder, fileName);
                                File.Copy(file, destPath, true);
                            }
                            // Optionally delete old folder after moving
                            // Directory.Delete(oldFolder, true);
                        }
                        else
                        {
                            Directory.Move(oldFolder, newFolder);
                        }
                        _logger.LogInformation("Updated worker folder from {OldFolder} to {NewFolder}", oldFolder, newFolder);
                    }
                    else if (!Directory.Exists(newFolder))
                    {
                        Directory.CreateDirectory(newFolder);
                        _logger.LogInformation("Created new worker folder: {Folder}", newFolder);
                    }
                }

                // Handle photo path update - copy to worker folder if new photo provided
                if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
                {
                    var workerFolder = GetWorkerFolderPath(newFirstName, newLastName);
                    if (!Directory.Exists(workerFolder))
                    {
                        Directory.CreateDirectory(workerFolder);
                    }
                    
                    // Check if photo is already in worker folder
                    if (!photoPath.StartsWith(workerFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        var photoFileName = $"{newFirstName}_{newLastName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}{Path.GetExtension(photoPath)}";
                        var destPhotoPath = Path.Combine(workerFolder, photoFileName);
                        File.Copy(photoPath, destPhotoPath, true);
                        photoPath = destPhotoPath;
                        _logger.LogInformation("Copied photo to worker folder: {Path}", destPhotoPath);
                    }
                }

                employee.Update(firstName, lastName, roleId, shiftGroupId, photoPath, isManager, shieldColor, showShield, stickerPaths, medalBadgePath, personnelId);

                EmployeesUpdated?.Invoke();
                SaveData();

                _logger.LogInformation("Employee updated: {FullName}", employee.FullName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee");
                ShowErrorDialog("خطا در بروزرسانی کارمند", ex.Message);
                return false;
            }
        }

        public bool DeleteEmployee(string employeeId)
        {
            try
            {
                _logger.LogInformation("Starting employee deletion for ID: {EmployeeId}", employeeId);
                
                if (!Employees.ContainsKey(employeeId))
                {
                    _logger.LogWarning("Employee {EmployeeId} not found in Employees dictionary", employeeId);
                    return false;
                }

                var employee = Employees[employeeId];
                _logger.LogInformation("Found employee: {FullName}", employee.FullName);

                // Remove from shifts
                _logger.LogInformation("Removing employee from morning shift");
                ShiftManager.RemoveEmployeeFromShift(employee, "morning");
                
                _logger.LogInformation("Removing employee from evening shift");
                ShiftManager.RemoveEmployeeFromShift(employee, "evening");

                // Remove absences
                _logger.LogInformation("Removing employee absences");
                AbsenceManager.RemoveEmployeeAbsences(employee);

                // Remove employee
                _logger.LogInformation("Removing employee from Employees dictionary");
                Employees.Remove(employeeId);

                _logger.LogInformation("Invoking update events");
                EmployeesUpdated?.Invoke();
                ShiftsUpdated?.Invoke();
                AbsencesUpdated?.Invoke();
                
                _logger.LogInformation("Saving data");
                SaveData();

                _logger.LogInformation("Employee deleted successfully: {FullName}", employee.FullName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee {EmployeeId}: {Message}", employeeId, ex.Message);
                ShowErrorDialog("خطا در حذف کارمند", ex.Message);
                return false;
            }
        }

        public (int imported, int skipped) ImportEmployeesFromCsv(string filePath)
        {
            try
            {
                var imported = 0;
                var skipped = 0;

                var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
                if (lines.Length < 2)
                    return (0, 0);

                var headers = lines[0].Split(',');
                var firstNameIndex = Array.IndexOf(headers, "first_name");
                var lastNameIndex = Array.IndexOf(headers, "last_name");
                var roleIndex = Array.IndexOf(headers, "role");

                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    if (values.Length <= firstNameIndex || values.Length <= lastNameIndex)
                        continue;

                    var firstName = values[firstNameIndex].Trim();
                    var lastName = values[lastNameIndex].Trim();
                    var role = roleIndex >= 0 && roleIndex < values.Length ? values[roleIndex].Trim() : "";

                    if (!string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
                    {
                        // Check for duplicates
                        var isDuplicate = Employees.Values.Any(emp => 
                            emp.FirstName.Equals(firstName, StringComparison.OrdinalIgnoreCase) &&
                            emp.LastName.Equals(lastName, StringComparison.OrdinalIgnoreCase));

                        if (!isDuplicate)
                        {
                            AddEmployee(firstName, lastName, role);
                            imported++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                }

                _logger.LogInformation("CSV import completed: {Imported} imported, {Skipped} skipped", imported, skipped);
                return (imported, skipped);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing CSV");
                ShowErrorDialog("خطا در وارد کردن CSV", ex.Message);
                return (0, 0);
            }
        }

        // Shift Management Methods
        public bool AssignEmployeeToShift(Employee employee, string shiftType, int? slotIndex = null, string? groupId = null)
        {
            try
            {
                // Use employee's group if not specified
                var targetGroupId = groupId ?? employee.ShiftGroupId;
                
                // Check if shift group exists
                var group = ShiftGroupManager.GetShiftGroup(targetGroupId);
                if (group == null)
                {
                    ShowErrorDialog("خطا", "گروه شیفت مورد نظر یافت نشد");
                    return false;
                }

                // Check if shift exists in the group
                var shift = group.GetShift(shiftType);
                if (shift == null)
                {
                    ShowErrorDialog("خطا", "شیفت مورد نظر یافت نشد");
                    return false;
                }

                // Check if employee is marked as absent
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                if (AbsenceManager.HasAbsenceForEmployee(employee, today))
                {
                    var absence = AbsenceManager.GetAbsenceForEmployee(employee, today);
                    var absenceType = absence?.Category ?? "غایب";
                    ShowErrorDialog("خطا در تخصیص شیفت", 
                        $"کارمند {employee.FullName} به عنوان {absenceType} ثبت شده است.\nکارمندان غایب نمی‌توانند به شیفت تخصیص داده شوند.\n\nلطفاً ابتدا غیبت کارمند را حذف کنید.");
                    return false;
                }

                // Check if employee is already assigned to another shift in this group
                var currentShifts = group.GetEmployeeShifts(employee);
                if (currentShifts.Any() && !currentShifts.Contains(shiftType))
                {
                    var shiftNames = currentShifts.Select(s => s == "morning" ? "صبح" : s == "evening" ? "عصر" : s).ToArray();
                    ShowErrorDialog("خطا در تخصیص شیفت", 
                        $"کارمند {employee.FullName} قبلاً به شیفت {string.Join(", ", shiftNames)} در این گروه تخصیص داده شده است.\nهر کارمند فقط می‌تواند به یک شیفت در هر گروه تخصیص داده شود.");
                    return false;
                }

                // Check if employee is assigned to a different group
                var currentGroupId = ShiftGroupManager.GetEmployeeGroup(employee);
                if (currentGroupId != null && currentGroupId != targetGroupId)
                {
                    var currentGroup = ShiftGroupManager.GetShiftGroup(currentGroupId);
                    ShowErrorDialog("خطا در تخصیص شیفت", 
                        $"کارمند {employee.FullName} قبلاً به گروه {currentGroup?.Name} تخصیص داده شده است.\nهر کارمند فقط می‌تواند به یک گروه تخصیص داده شود.");
                    return false;
                }

                bool success;
                if (slotIndex.HasValue)
                {
                    success = ShiftGroupManager.AssignEmployeeToSlot(employee, targetGroupId, shiftType, slotIndex.Value);
                }
                else
                {
                    success = ShiftGroupManager.AssignEmployee(employee, targetGroupId, shiftType);
                }

                if (success)
                {
                    ShiftsUpdated?.Invoke();
                    ShiftGroupsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Employee {FullName} assigned to {ShiftType} shift in group {GroupId}", employee.FullName, shiftType, targetGroupId);
                    return true;
                }
                else
                {
                    ShowErrorDialog("خطا", "نمی‌توان کارمند را به این شیفت اضافه کرد");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning employee to shift");
                ShowErrorDialog("خطا در تخصیص شیفت", ex.Message);
                return false;
            }
        }

        public bool RemoveEmployeeFromShift(Employee employee, string shiftType, string? groupId = null)
        {
            try
            {
                // Use employee's group if not specified
                var targetGroupId = groupId ?? employee.ShiftGroupId;
                
                var success = ShiftGroupManager.RemoveEmployeeFromShift(employee, targetGroupId, shiftType);
                if (success)
                {
                    ShiftsUpdated?.Invoke();
                    ShiftGroupsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Employee {FullName} removed from {ShiftType} shift in group {GroupId}", employee.FullName, shiftType, targetGroupId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing employee from shift");
                ShowErrorDialog("خطا در حذف از شیفت", ex.Message);
                return false;
            }
        }

        public bool ClearShift(string shiftType, string? groupId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(groupId))
                {
                    // Clear shift in all groups
                    foreach (var group in ShiftGroupManager.GetAllShiftGroups())
                    {
                        group.ClearShift(shiftType);
                    }
                }
                else
                {
                    // Clear shift in specific group
                    var group = ShiftGroupManager.GetShiftGroup(groupId);
                    group?.ClearShift(shiftType);
                }
                
                ShiftsUpdated?.Invoke();
                ShiftGroupsUpdated?.Invoke();
                SaveData();
                _logger.LogInformation("Shift {ShiftType} cleared in group {GroupId}", shiftType, groupId ?? "all");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing shift");
                ShowErrorDialog("خطا در پاک کردن شیفت", ex.Message);
                return false;
            }
        }

        public void SetShiftCapacity(int capacity)
        {
            try
            {
                _logger.LogInformation("Setting shift capacity to {Capacity} (current: {CurrentCapacity})", capacity, ShiftManager.Capacity);
                
                // Record the time of capacity change
                _lastCapacityChange = DateTime.Now;
                
                // Temporarily stop sync manager to prevent interference
                _syncManager.StopSync();
                
                Settings["shift_capacity"] = capacity;
                Settings["morning_capacity"] = capacity;
                Settings["evening_capacity"] = capacity;

                ShiftManager.SetCapacity(capacity);
                
                // Update all shift groups' capacities to match the new global capacity
                foreach (var group in ShiftGroupManager.GetAllShiftGroups())
                {
                    group.Update(morningCapacity: capacity, eveningCapacity: capacity);
                    _logger.LogInformation("Updated shift group {GroupId} capacity to {Capacity}", group.GroupId, capacity);
                }
                
                _logger.LogInformation("After SetCapacity: {Capacity}", ShiftManager.Capacity);

                // Save the data to persist the capacity change
                SaveData();
                
                _logger.LogInformation("Data saved, capacity is now {Capacity}", ShiftManager.Capacity);

                SettingsUpdated?.Invoke();
                ShiftGroupsUpdated?.Invoke(); // Notify that shift groups were updated
                
                // Restart sync manager after a delay
                System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ => _syncManager.StartSync());
                _logger.LogInformation("Shift capacity set to {Capacity}", capacity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting shift capacity");
                throw;
            }
        }

        // Absence Management Methods
        public bool MarkEmployeeAbsent(Employee employee, string category, string notes = "", string? customDate = null)
        {
            try
            {
                var absence = new Absence(employee, category, customDate, notes);
                var success = AbsenceManager.AddAbsence(absence);

                if (success)
                {
                    // Always remove from shifts when employee is marked as absent, regardless of date
                    // Remove from shift groups (new system) - find actual assigned group
                    var assignedGroupId = ShiftGroupManager.GetEmployeeGroup(employee);
                    if (!string.IsNullOrEmpty(assignedGroupId))
                    {
                        _logger.LogInformation("Removing employee {FullName} from assigned group {GroupId} due to absence", employee.FullName, assignedGroupId);
                        ShiftGroupManager.RemoveEmployeeFromShift(employee, assignedGroupId, "morning");
                        ShiftGroupManager.RemoveEmployeeFromShift(employee, assignedGroupId, "evening");
                    }
                    else
                    {
                        _logger.LogInformation("Employee {FullName} not found in any shift group", employee.FullName);
                    }
                    
                    // Also remove from old ShiftManager for backward compatibility
                    ShiftManager.RemoveEmployeeFromShift(employee, "morning");
                    ShiftManager.RemoveEmployeeFromShift(employee, "evening");

                    AbsencesUpdated?.Invoke();
                    ShiftsUpdated?.Invoke();
                    SaveData();

                    _logger.LogInformation("Employee {FullName} marked as {Category} for date {Date} and removed from all shifts", employee.FullName, category, absence.Date);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking employee absent");
                ShowErrorDialog("خطا در ثبت غیبت", ex.Message);
                return false;
            }
        }

        public bool RemoveAbsence(Employee employee)
        {
            try
            {
                var absences = AbsenceManager.GetAbsencesByEmployee(employee);
                foreach (var absence in absences)
                {
                    AbsenceManager.RemoveAbsence(absence);
                }

                AbsencesUpdated?.Invoke();
                SaveData();

                _logger.LogInformation("Absence removed for {FullName}", employee.FullName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing absence");
                ShowErrorDialog("خطا در حذف غیبت", ex.Message);
                return false;
            }
        }

        public List<Absence> GetAllAbsences()
        {
            try
            {
                var allAbsences = new List<Absence>();
                foreach (var absences in AbsenceManager.Absences.Values)
                {
                    allAbsences.AddRange(absences);
                }
                return allAbsences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all absences");
                return new List<Absence>();
            }
        }

        // Task Management Methods
        public string AddTask(string title, string description = "", string priority = "متوسط", 
                             double estimatedHours = 1.0, string? targetDate = null)
        {
            try
            {
                var priorityEnum = Enum.TryParse<TaskPriority>(priority, out var parsedPriority) 
                    ? parsedPriority : TaskPriority.Medium;

                var taskId = TaskManager.AddTask(title, description, priorityEnum, estimatedHours, targetDate);
                TasksUpdated?.Invoke();
                SaveData();

                _logger.LogInformation("Task added: {Title}", title);
                return taskId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding task");
                ShowErrorDialog("خطا در افزودن وظیفه", ex.Message);
                return string.Empty;
            }
        }

        public bool UpdateTask(string taskId, string? title = null, string? description = null,
                              string? priority = null, double? estimatedHours = null,
                              string? targetDate = null, string? status = null,
                              double? actualHours = null, string? notes = null)
        {
            try
            {
                TaskPriority? priorityEnum = null;
                if (!string.IsNullOrEmpty(priority) && Enum.TryParse<TaskPriority>(priority, out var parsedPriority))
                    priorityEnum = parsedPriority;

                Shared.Models.TaskStatus? statusEnum = null;
                if (!string.IsNullOrEmpty(status))
                {
                    // Convert Persian status strings to English enum values
                    switch (status)
                    {
                        case "در انتظار":
                            statusEnum = Shared.Models.TaskStatus.Pending;
                            break;
                        case "در حال انجام":
                            statusEnum = Shared.Models.TaskStatus.InProgress;
                            break;
                        case "تکمیل شده":
                            statusEnum = Shared.Models.TaskStatus.Completed;
                            break;
                        case "لغو شده":
                            statusEnum = Shared.Models.TaskStatus.Cancelled;
                            break;
                        default:
                            // Try to parse as English enum value as fallback
                            if (Enum.TryParse<Shared.Models.TaskStatus>(status, out var parsedStatus))
                                statusEnum = parsedStatus;
                            break;
                    }
                }

                _logger.LogInformation("UpdateTask: Converting status '{Status}' to enum: {StatusEnum}", status, statusEnum);
                
                var success = TaskManager.UpdateTask(taskId, title, description, priorityEnum, 
                    estimatedHours, targetDate, statusEnum, actualHours, notes);

                if (success)
                {
                    TasksUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Task updated: {TaskId}, new status: {StatusEnum}", taskId, statusEnum);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task");
                ShowErrorDialog("خطا در بروزرسانی وظیفه", ex.Message);
                return false;
            }
        }

        public bool DeleteTask(string taskId)
        {
            try
            {
                var success = TaskManager.DeleteTask(taskId);
                if (success)
                {
                    TasksUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Task deleted: {TaskId}", taskId);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task");
                ShowErrorDialog("خطا در حذف وظیفه", ex.Message);
                return false;
            }
        }

        // Sync Methods
        private void OnSyncTriggered()
        {
            try
            {
                // Check if we just made a capacity change recently (within last 5 minutes)
                var timeSinceCapacityChange = DateTime.Now - _lastCapacityChange;
                if (timeSinceCapacityChange.TotalMinutes < 5.0)
                {
                    _logger.LogInformation("Skipping sync - capacity was changed {Minutes} minutes ago", timeSinceCapacityChange.TotalMinutes);
                    return;
                }

                // Check if we need to reload data
                var currentFile = _jsonHandler.GetTodayFilepath();
                if (!File.Exists(currentFile))
                    return;

                var currentMtime = File.GetLastWriteTime(currentFile);
                var timeSinceModification = DateTime.Now - currentMtime;

                // Skip if this is our own save operation
                if (timeSinceModification.TotalSeconds < 3.0)
                    return;

                _logger.LogInformation("External file change detected, reloading data");

                // Store current data to preserve during reload
                var currentCount = Employees.Count;
                var currentAbsences = AbsenceManager.ToJson();
                var currentMorningAssignments = ShiftManager.MorningShift.AssignedEmployees.Select(emp => emp?.EmployeeId).ToList();
                var currentEveningAssignments = ShiftManager.EveningShift.AssignedEmployees.Select(emp => emp?.EmployeeId).ToList();
                var currentCapacity = ShiftManager.Capacity; // Preserve current capacity

                // Reload data
                LoadData();

                // Restore capacity if it was changed recently (within last 2 minutes)
                timeSinceCapacityChange = DateTime.Now - _lastCapacityChange;
                if (timeSinceCapacityChange.TotalMinutes < 2.0 && currentCapacity != ShiftManager.Capacity)
                {
                    _logger.LogInformation("Restoring capacity from {OldCapacity} to {CurrentCapacity} (changed {Minutes} minutes ago)", 
                        ShiftManager.Capacity, currentCapacity, timeSinceCapacityChange.TotalMinutes);
                    ShiftManager.SetCapacity(currentCapacity);
                    Settings["shift_capacity"] = currentCapacity;
                    Settings["morning_capacity"] = currentCapacity;
                    Settings["evening_capacity"] = currentCapacity;
                }

                // Restore shift assignments if they were lost during reload
                if (currentMorningAssignments.Any() || currentEveningAssignments.Any())
                {
                    // Restore morning shift assignments
                    for (int i = 0; i < currentMorningAssignments.Count && i < ShiftManager.MorningShift.AssignedEmployees.Count; i++)
                    {
                        var empId = currentMorningAssignments[i];
                        if (!string.IsNullOrEmpty(empId) && Employees.ContainsKey(empId) && ShiftManager.MorningShift.AssignedEmployees[i] == null)
                        {
                            ShiftManager.MorningShift.AssignedEmployees[i] = Employees[empId];
                        }
                    }

                    // Restore evening shift assignments
                    for (int i = 0; i < currentEveningAssignments.Count && i < ShiftManager.EveningShift.AssignedEmployees.Count; i++)
                    {
                        var empId = currentEveningAssignments[i];
                        if (!string.IsNullOrEmpty(empId) && Employees.ContainsKey(empId) && ShiftManager.EveningShift.AssignedEmployees[i] == null)
                        {
                            ShiftManager.EveningShift.AssignedEmployees[i] = Employees[empId];
                        }
                    }
                }

                // Restore absences if they were lost during reload
                if (!string.IsNullOrEmpty(currentAbsences))
                {
                    var currentAbsenceCount = AbsenceManager.GetTotalAbsences();
                    var restoredAbsenceManager = AbsenceManager.FromJson(currentAbsences, Employees);
                    if (restoredAbsenceManager.GetTotalAbsences() > currentAbsenceCount)
                    {
                        AbsenceManager = restoredAbsenceManager;
                    }
                }

                // Only emit signals if data actually changed
                var newCount = Employees.Count;
                if (newCount != currentCount)
                {
                    EmployeesUpdated?.Invoke();
                    ShiftsUpdated?.Invoke();
                    AbsencesUpdated?.Invoke();
                }
                else
                {
                    ShiftsUpdated?.Invoke();
                }

                SyncTriggered?.Invoke();
                _logger.LogInformation("Sync completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync");
            }
        }

        public void ForceSync()
        {
            _syncManager.ForceSync();
        }

        // Utility Methods
        private void ShowErrorDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public Employee? GetEmployeeById(string employeeId)
        {
            return Employees.GetValueOrDefault(employeeId);
        }

        public List<Employee> GetAllEmployees()
        {
            return Employees.Values.ToList();
        }

        public List<Employee> SearchEmployees(string query)
        {
            var lowerQuery = query.ToLower();
            return Employees.Values.Where(emp =>
                emp.FirstName.ToLower().Contains(lowerQuery) ||
                emp.LastName.ToLower().Contains(lowerQuery) ||
                emp.FullName.ToLower().Contains(lowerQuery)).ToList();
        }

        public List<Shared.Models.Task> GetAllTasks()
        {
            return TaskManager.Tasks.Values.ToList();
        }

        public Shared.Models.Task? GetTask(string taskId)
        {
            return TaskManager.GetTask(taskId);
        }

        public bool AssignTaskToEmployee(string taskId, string employeeId)
        {
            try
            {
                var task = TaskManager.GetTask(taskId);
                if (task == null)
                {
                    _logger.LogWarning("Task not found: {TaskId}", taskId);
                    return false;
                }

                var employee = Employees.GetValueOrDefault(employeeId);
                if (employee == null)
                {
                    _logger.LogWarning("Employee not found: {EmployeeId}", employeeId);
                    return false;
                }

                var success = task.AssignEmployee(employeeId);
                if (success)
                {
                    TasksUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Task {TaskId} assigned to employee {EmployeeId}", taskId, employeeId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning task to employee");
                ShowErrorDialog("خطا در تخصیص وظیفه", ex.Message);
                return false;
            }
        }

        public bool RemoveTaskFromEmployee(string taskId, string employeeId)
        {
            try
            {
                var task = TaskManager.GetTask(taskId);
                if (task == null)
                {
                    _logger.LogWarning("Task not found: {TaskId}", taskId);
                    return false;
                }

                var success = task.RemoveEmployee(employeeId);
                if (success)
                {
                    TasksUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Task {TaskId} removed from employee {EmployeeId}", taskId, employeeId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing task from employee");
                ShowErrorDialog("خطا در حذف تخصیص وظیفه", ex.Message);
                return false;
            }
        }

        public List<Employee> GetAvailableEmployeesForTask(string taskId)
        {
            try
            {
                var task = TaskManager.GetTask(taskId);
                if (task == null)
                    return new List<Employee>();

                // Get all employees not already assigned to this task
                return Employees.Values.Where(emp => !task.AssignedEmployees.Contains(emp.EmployeeId)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available employees for task");
                return new List<Employee>();
            }
        }

        public List<Shared.Models.Task> GetTasksForEmployee(string employeeId)
        {
            try
            {
                return TaskManager.Tasks.Values.Where(task => task.AssignedEmployees.Contains(employeeId)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tasks for employee");
                return new List<Shared.Models.Task>();
            }
        }

        public void Cleanup()
        {
            try
            {
                _syncManager.Dispose();
                _logger.LogInformation("Controller cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }

        // Historical data access methods for reporting
        public List<string> GetAllReportFiles()
        {
            return _jsonHandler.GetAllReports();
        }

        public Dictionary<string, object>? ReadHistoricalReport(string dateStr)
        {
            try
            {
                var filename = $"report_{dateStr}.json";
                var filepath = Path.Combine(_dataDir, "Reports", filename);
                return _jsonHandler.ReadJson(filepath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading historical report for date {Date}", dateStr);
                return null;
            }
        }

        public string GetDataDirectory()
        {
            return _dataDir;
        }

        // Debug method to help identify persistence issues
        public void DebugDataPersistence()
        {
            try
            {
                _logger.LogInformation("=== Data Persistence Debug Info ===");
                _logger.LogInformation("Data Directory: {DataDir}", _dataDir);
                _logger.LogInformation("Data Directory Exists: {Exists}", Directory.Exists(_dataDir));
                
                var todayFile = _jsonHandler.GetTodayFilepath();
                _logger.LogInformation("Today's File Path: {FilePath}", todayFile);
                _logger.LogInformation("Today's File Exists: {Exists}", File.Exists(todayFile));
                
                if (File.Exists(todayFile))
                {
                    var fileInfo = new FileInfo(todayFile);
                    _logger.LogInformation("File Size: {Size} bytes", fileInfo.Length);
                    _logger.LogInformation("File Last Modified: {LastModified}", fileInfo.LastWriteTime);
                }
                
                var allReports = _jsonHandler.GetAllReports();
                _logger.LogInformation("Total Report Files: {Count}", allReports.Count);
                _logger.LogInformation("Report Files: {Files}", string.Join(", ", allReports.Take(5)));
                
                _logger.LogInformation("Current Employee Count: {Count}", Employees.Count);
                _logger.LogInformation("Current Shift Capacity: {Capacity}", ShiftManager.Capacity);
                _logger.LogInformation("=== End Debug Info ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in debug data persistence");
            }
        }

        public string GetWorkerFolderPath(string firstName, string lastName)
        {
            var workersDir = Path.Combine(_dataDir, "Workers");
            if (!Directory.Exists(workersDir))
            {
                Directory.CreateDirectory(workersDir);
            }
            var folderName = $"{firstName}_{lastName}";
            return Path.Combine(workersDir, folderName);
        }

        public (string? FirstName, string? LastName) DetectNameFromFolder(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory))
                    return (null, null);

                var folderName = Path.GetFileName(directory);
                var workersDir = Path.Combine(_dataDir, "Workers");
                
                // Check if file is in a Workers subfolder
                if (directory.StartsWith(workersDir, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = folderName.Split('_');
                    if (parts.Length >= 2)
                    {
                        return (parts[0], parts[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting name from folder path: {Path}", filePath);
            }
            
            return (null, null);
        }

        public string? DetectPersonnelIdFromFilename(string filePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(fileName))
                    return null;

                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory))
                    return null;

                var workersDir = Path.Combine(_dataDir, "Workers");
                
                // Check if file is in a Workers subfolder
                if (directory.StartsWith(workersDir, StringComparison.OrdinalIgnoreCase))
                {
                    // Expected format: FirstName_LastName_PersonnelID
                    // Example: Ali_Rezaei_123.jpg -> Ali_Rezaei_123 -> extract 123
                    var parts = fileName.Split('_');
                    if (parts.Length >= 3)
                    {
                        // The last part should be the personnel ID
                        var personnelId = parts[parts.Length - 1];
                        // Validate that it's a numeric value (personnel ID)
                        if (!string.IsNullOrEmpty(personnelId) && personnelId.All(char.IsDigit))
                        {
                            return personnelId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting personnel ID from filename: {Path}", filePath);
            }
            
            return null;
        }

        public bool SwapShifts(string? groupId = null)
        {
            try
            {
                var targetGroupId = groupId ?? "default";
                var group = ShiftGroupManager.GetShiftGroup(targetGroupId);
                if (group == null)
                    return false;

                // Note: SwapShifts functionality not yet implemented on ShiftGroup
                // group.SwapShifts();
                ShiftsUpdated?.Invoke();
                SaveData();
                _logger.LogInformation("Swapped shifts for group {GroupId}", targetGroupId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error swapping shifts");
                return false;
            }
        }

        public bool SetTeamLeader(string shiftType, string employeeId, string? groupId = null)
        {
            try
            {
                var targetGroupId = groupId ?? SelectedDisplayGroupId ?? "default";
                var group = ShiftGroupManager.GetShiftGroup(targetGroupId);
                if (group == null)
                    return false;

                // Validate employee exists
                if (!string.IsNullOrEmpty(employeeId) && !Employees.ContainsKey(employeeId))
                {
                    _logger.LogWarning("Employee {EmployeeId} not found when setting team leader", employeeId);
                    return false;
                }

                group.SetTeamLeader(shiftType, employeeId ?? string.Empty);
                ShiftsUpdated?.Invoke();
                SaveData();
                _logger.LogInformation("Set team leader {EmployeeId} for {ShiftType} shift in group {GroupId}", employeeId, shiftType, targetGroupId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting team leader");
                return false;
            }
        }

        public Employee? GetTeamLeader(string shiftType, string? groupId = null)
        {
            try
            {
                var targetGroupId = groupId ?? SelectedDisplayGroupId ?? "default";
                var group = ShiftGroupManager.GetShiftGroup(targetGroupId);
                if (group == null)
                    return null;

                return group.GetTeamLeader(shiftType, Employees);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team leader");
                return null;
            }
        }

        // Badge Generation Methods
        public string GetBadgeTemplatePath()
        {
            if (Settings.TryGetValue("badge_template_path", out var path) && path is string templatePath)
            {
                return templatePath;
            }
            // Default path
            return Path.Combine(_dataDir, "BadgeTemplate.png");
        }

        public bool SetBadgeTemplatePath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    _logger.LogWarning("Badge template path cannot be empty");
                    return false;
                }

                Settings["badge_template_path"] = path;
                SaveData();
                _logger.LogInformation("Badge template path set to: {Path}", path);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting badge template path");
                return false;
            }
        }

        public string? GenerateEmployeeBadge(string employeeId, string? outputPath = null)
        {
            try
            {
                if (!Employees.ContainsKey(employeeId))
                {
                    _logger.LogWarning("Employee not found: {EmployeeId}", employeeId);
                    return null;
                }

                var employee = Employees[employeeId];

                // Validate employee has photo
                if (string.IsNullOrEmpty(employee.PhotoPath) || !File.Exists(employee.PhotoPath))
                {
                    _logger.LogWarning("Employee {EmployeeId} does not have a valid photo", employeeId);
                    return null;
                }

                // Get badge template path
                var templatePath = GetBadgeTemplatePath();
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    _logger.LogError("Badge template not found: {TemplatePath}", templatePath);
                    ShowErrorDialog("خطا", $"قالب کارت شناسایی یافت نشد: {templatePath}");
                    return null;
                }

                // Determine output path
                if (string.IsNullOrEmpty(outputPath))
                {
                    var workerFolder = GetWorkerFolderPath(employee.FirstName, employee.LastName);
                    if (!Directory.Exists(workerFolder))
                    {
                        Directory.CreateDirectory(workerFolder);
                    }
                    var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                    outputPath = Path.Combine(workerFolder, $"{employee.FirstName}_{employee.LastName}_badge_{timestamp}.png");
                }

                // Generate badge
                var employeeName = employee.FullName;
                var success = _badgeGenerator.GenerateBadge(templatePath, employee.PhotoPath, employeeName, outputPath);

                if (success)
                {
                    _logger.LogInformation("Badge generated successfully for employee {EmployeeId}: {OutputPath}", employeeId, outputPath);
                    return outputPath;
                }
                else
                {
                    _logger.LogError("Failed to generate badge for employee {EmployeeId}", employeeId);
                    ShowErrorDialog("خطا", "خطا در تولید کارت شناسایی");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating badge for employee {EmployeeId}", employeeId);
                ShowErrorDialog("خطا", $"خطا در تولید کارت شناسایی: {ex.Message}");
                return null;
            }
        }

        private void OnConfigurationChanged(Shared.Utils.AppConfig newConfig)
        {
            try
            {
                _logger.LogInformation("Configuration changed, updating MainController with new data path: {DataPath}", 
                    newConfig.DataDirectory);
                
                // Update data directory
                _dataDir = newConfig.DataDirectory;
                
                // Recreate services with new path
                _jsonHandler = new JsonHandler(_dataDir);
                
                // Stop current sync manager
                _syncManager?.Dispose();
                
                // Create new sync manager with new path
                _syncManager = new SyncManager(_dataDir);
                SetupSync();
                
                // Reload data with new path
                LoadData();
                
                _logger.LogInformation("MainController updated with new configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating MainController configuration");
            }
        }
    }
}
