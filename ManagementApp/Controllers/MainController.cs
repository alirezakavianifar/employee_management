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

namespace ManagementApp.Controllers
{
    public class MainController
    {
        private readonly string _dataDir;
        private readonly JsonHandler _jsonHandler;
        private readonly SyncManager _syncManager;
        private readonly ILogger<MainController> _logger;
        private DateTime _lastCapacityChange = DateTime.MinValue;

        // Data structures
        public Dictionary<string, Employee> Employees { get; private set; } = new();
        public ShiftManager ShiftManager { get; private set; } = new(15); // Start with 15 instead of 5
        public AbsenceManager AbsenceManager { get; private set; } = new();
        public TaskManager TaskManager { get; private set; } = new();
        public Dictionary<string, object> Settings { get; private set; } = new();

        // Events
        public event Action? EmployeesUpdated;
        public event Action? ShiftsUpdated;
        public event Action? AbsencesUpdated;
        public event Action? TasksUpdated;
        public event Action? SettingsUpdated;
        public event Action? SyncTriggered;

        public MainController(string dataDir = "Data")
        {
            // Use shared data directory directly
            _dataDir = @"D:\projects\New folder (8)\SharedData";
            _jsonHandler = new JsonHandler(_dataDir);
            _syncManager = new SyncManager(_dataDir);
            _logger = LoggingService.CreateLogger<MainController>();

            InitializeSettings();
            LoadData();
            SetupSync();
        }

        private void InitializeSettings()
        {
            Settings = new Dictionary<string, object>
            {
                { "shift_capacity", 15 }, // Start with 15 instead of 5
                { "morning_capacity", 15 },
                { "evening_capacity", 15 },
                { "shared_folder_path", _dataDir },
                { "managers", new List<object>() }
            };
        }

        private void SetupSync()
        {
            _syncManager.AddSyncCallback(OnSyncTriggered);
            _syncManager.StartSync();
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

                // Load absences
                if (reportData.ContainsKey("absences"))
                {
                    LoadAbsencesFromData(reportData["absences"]);
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
                    var role = employeeDict.GetValueOrDefault("role", "Employee").ToString() ?? "Employee";
                    var photoPath = employeeDict.GetValueOrDefault("photo_path", "").ToString() ?? "";
                    var isManager = employeeDict.GetValueOrDefault("is_manager", false);
                    bool isManagerBool = false;
                    if (isManager is bool b)
                        isManagerBool = b;
                    else if (isManager is string s && bool.TryParse(s, out bool parsed))
                        isManagerBool = parsed;

                    var employee = new Employee(employeeId, firstName, lastName, role, photoPath, isManagerBool);

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
                            
                            if (IsValidPersianDate(createdAt))
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
                            
                            if (IsValidPersianDate(updatedAt))
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

        private bool IsValidPersianDate(DateTime date)
        {
            try
            {
                // Check if the date is within valid range for Persian calendar
                var persianCalendar = new System.Globalization.PersianCalendar();
                var year = persianCalendar.GetYear(date);
                return year >= 1 && year <= 9378; // Valid Persian calendar range
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

        private void LoadTasksFromData(object tasksData)
        {
            try
            {
                var tasksJson = JsonConvert.SerializeObject(tasksData);
                TaskManager = TaskManager.FromJson(tasksJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tasks");
            }
        }

        private Dictionary<string, object> CreateShiftsData()
        {
            try
            {
                // Create shifts data in the format expected by Display App
                var shiftsData = new Dictionary<string, object>
                {
                    { "morning", new Dictionary<string, object>
                        {
                            { "shift_type", "morning" },
                            { "capacity", ShiftManager.MorningShift.Capacity },
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
                            { "assigned_employees", ShiftManager.EveningShift.AssignedEmployees
                                .Where(emp => emp != null)
                                .Select(emp => emp.ToDictionary())
                                .Cast<object>()
                                .ToList()
                            }
                        }
                    }
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
                    }
                };
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
                    { "shifts", CreateShiftsData() },
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

        // Employee Management Methods
        public bool AddEmployee(string firstName, string lastName, string role = "", string photoPath = "", bool isManager = false)
        {
            try
            {
                var employeeId = $"emp_{Employees.Count}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                var employee = new Employee(employeeId, firstName, lastName, role, photoPath, isManager);

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

        public bool UpdateEmployee(string employeeId, string? firstName = null, string? lastName = null, string? role = null, string? photoPath = null, bool? isManager = null)
        {
            try
            {
                if (!Employees.ContainsKey(employeeId))
                    return false;

                var employee = Employees[employeeId];
                employee.Update(firstName, lastName, role, photoPath, isManager);

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
        public bool AssignEmployeeToShift(Employee employee, string shiftType, int? slotIndex = null)
        {
            try
            {
                // Check if shift exists
                var shift = ShiftManager.GetShift(shiftType);
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

                // Check if employee is already assigned to another shift
                var currentShifts = ShiftManager.GetEmployeeShifts(employee);
                if (currentShifts.Any() && !currentShifts.Contains(shiftType))
                {
                    var shiftNames = currentShifts.Select(s => s == "morning" ? "صبح" : s == "evening" ? "عصر" : s).ToArray();
                    ShowErrorDialog("خطا در تخصیص شیفت", 
                        $"کارمند {employee.FullName} قبلاً به شیفت {string.Join(", ", shiftNames)} تخصیص داده شده است.\nهر کارمند فقط می‌تواند به یک شیفت تخصیص داده شود.");
                    return false;
                }

                bool success;
                if (slotIndex.HasValue)
                {
                    success = ShiftManager.AssignEmployeeToSlot(employee, shiftType, slotIndex.Value);
                }
                else
                {
                    success = ShiftManager.AssignEmployee(employee, shiftType);
                }

                if (success)
                {
                    ShiftsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Employee {FullName} assigned to {ShiftType} shift", employee.FullName, shiftType);
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

        public bool RemoveEmployeeFromShift(Employee employee, string shiftType)
        {
            try
            {
                var success = ShiftManager.RemoveEmployeeFromShift(employee, shiftType);
                if (success)
                {
                    ShiftsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Employee {FullName} removed from {ShiftType} shift", employee.FullName, shiftType);
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

        public bool ClearShift(string shiftType)
        {
            try
            {
                ShiftManager.ClearShift(shiftType);
                ShiftsUpdated?.Invoke();
                SaveData();
                _logger.LogInformation("Shift {ShiftType} cleared", shiftType);
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
                
                _logger.LogInformation("After SetCapacity: {Capacity}", ShiftManager.Capacity);

                // Save the data to persist the capacity change
                SaveData();
                
                _logger.LogInformation("Data saved, capacity is now {Capacity}", ShiftManager.Capacity);

                SettingsUpdated?.Invoke();
                
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
                    // Remove from shifts only if the absence is for today
                    var todayShamsi = ShamsiDateHelper.GetCurrentShamsiDate();
                    if (absence.Date == todayShamsi)
                    {
                        ShiftManager.RemoveEmployeeFromShift(employee, "morning");
                        ShiftManager.RemoveEmployeeFromShift(employee, "evening");
                    }

                    AbsencesUpdated?.Invoke();
                    ShiftsUpdated?.Invoke();
                    SaveData();

                    _logger.LogInformation("Employee {FullName} marked as {Category} for date {Date}", employee.FullName, category, absence.Date);
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
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<Shared.Models.TaskStatus>(status, out var parsedStatus))
                    statusEnum = parsedStatus;

                var success = TaskManager.UpdateTask(taskId, title, description, priorityEnum, 
                    estimatedHours, targetDate, statusEnum, actualHours, notes);

                if (success)
                {
                    TasksUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Task updated: {TaskId}", taskId);
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
    }
}
