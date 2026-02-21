
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
    public enum ConflictType
    {
        None,
        Absent,              // Employee is absent/sick/on leave
        DifferentShift,      // Employee assigned to different shift in same group
        DifferentGroup       // Employee assigned to different group
    }

    public class AssignmentConflict
    {
        public ConflictType Type { get; set; }
        public string? CurrentGroupId { get; set; }
        public string? CurrentGroupName { get; set; }
        public string? CurrentShiftType { get; set; }
        public string? AbsenceType { get; set; } // "Absent", "Sick", "Leave"
    }

    public class AssignmentResult
    {
        public bool Success { get; set; }
        public AssignmentConflict? Conflict { get; set; }
        public string? ErrorMessage { get; set; }
    }

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
        private FileSystemWatcher? _imageWatcher;
        private DateTime _lastImageProcessTime = DateTime.MinValue;
        private readonly HashSet<string> _processedImageFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Data structures
        public Dictionary<string, Employee> Employees { get; private set; } = new();
        public RoleManager RoleManager { get; private set; } = new();
        public ShiftManager ShiftManager { get; private set; } = new(15); // Start with 15 instead of 5
        public ShiftGroupManager ShiftGroupManager { get; private set; } = new(); // New shift group manager
        public AbsenceManager AbsenceManager { get; private set; } = new();
        public TaskManager TaskManager { get; private set; } = new();
        public DailyTaskProgressManager DailyTaskProgressManager { get; private set; } = new();
        public Dictionary<string, object> Settings { get; private set; } = new();
        public string SelectedDisplayGroupId { get; set; } = "default";
        
        // Status Cards
        private StatusCardService _statusCardService;
        public Dictionary<string, StatusCard> StatusCards { get; private set; } = new();

        // Events
        public event Action? EmployeesUpdated;
        public event Action? RolesUpdated;
        public event Action? ShiftsUpdated;
        public event Action? ShiftGroupsUpdated;
        public event Action? AbsencesUpdated;
        public event Action? TasksUpdated;
        public event Action? SettingsUpdated;
        public event Action? SyncTriggered;
        public event Action? StatusCardsUpdated;

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
            _statusCardService = new StatusCardService(_dataDir);

            InitializeSettings();
            LoadData();
            SetupSync();
            SetupRotationScheduler();
            SetupImageWatcher();
            
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

        private void SetupImageWatcher()
        {
            try
            {
                var imagesFolder = GetEmployeeImagesFolder();
                if (!Directory.Exists(imagesFolder))
                {
                    Directory.CreateDirectory(imagesFolder);
                }

                _imageWatcher = new FileSystemWatcher(imagesFolder)
                {
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true
                };

                _imageWatcher.Created += OnImageFileCreated;
                _imageWatcher.Changed += OnImageFileChanged;

                _logger.LogInformation("Started image file monitoring for directory: {ImagesFolder}", imagesFolder);
                
                // Scan existing files in the directory on startup
                ScanExistingImageFiles(imagesFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start image file monitoring");
            }
        }

        private void ScanExistingImageFiles(string imagesFolder)
        {
            try
            {
                _logger.LogInformation("Scanning existing image files in directory: {ImagesFolder}", imagesFolder);
                
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
                var imageFiles = Directory.GetFiles(imagesFolder, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower(), StringComparer.OrdinalIgnoreCase))
                    .ToList();

                _logger.LogInformation("Found {Count} existing image files to process", imageFiles.Count);

                // Process each file (with a small delay between files to avoid overwhelming the system)
                foreach (var imageFile in imageFiles)
                {
                    // Check if this file already corresponds to an existing employee
                    var (firstName, lastName) = DetectNameFromFolder(imageFile);
                    var personnelId = DetectPersonnelIdFromFilename(imageFile);
                    
                    if (firstName != null && lastName != null)
                    {
                        var existingEmployee = FindEmployeeByNameOrPersonnelId(firstName, lastName, personnelId);
                        
                        // Only process if employee doesn't exist or if photo path is different
                        if (existingEmployee == null || existingEmployee.PhotoPath != imageFile)
                        {
                            // Process the file (ProcessImageFile will add it to _processedImageFiles after processing)
                            ProcessImageFile(imageFile);
                        }
                        else
                        {
                            // File already processed for this employee, mark as processed to avoid reprocessing
                            lock (_processedImageFiles)
                            {
                                _processedImageFiles.Add(imageFile);
                            }
                        }
                    }
                    
                    // Small delay to avoid overwhelming the system when processing many files
                    System.Threading.Thread.Sleep(50);
                }

                _logger.LogInformation("Finished scanning existing image files");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning existing image files");
            }
        }

        private void OnImageFileCreated(object sender, FileSystemEventArgs e)
        {
            // Small delay to ensure file is fully written
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => ProcessImageFile(e.FullPath));
        }

        private void OnImageFileChanged(object sender, FileSystemEventArgs e)
        {
            // Process file changes with debouncing (only process once per 2 seconds per file)
            var timeSinceLastProcess = DateTime.Now - _lastImageProcessTime;
            if (timeSinceLastProcess.TotalSeconds < 2.0)
                return;

            // Only process if it's an image file
            var ext = Path.GetExtension(e.FullPath).ToLower();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp")
                return;

            _lastImageProcessTime = DateTime.Now;
            System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => ProcessImageFile(e.FullPath));
        }

        private void ProcessImageFile(string imagePath)
        {
            try
            {
                // Check if file exists and is an image
                if (!File.Exists(imagePath))
                    return;

                var ext = Path.GetExtension(imagePath).ToLower();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp")
                    return;

                // Check if file is already in the images folder (to avoid processing files we just created)
                var imagesFolder = GetEmployeeImagesFolder();
                if (!imagePath.StartsWith(imagesFolder, StringComparison.OrdinalIgnoreCase))
                    return;

                // Skip if we've already processed this file recently (to avoid duplicate processing)
                lock (_processedImageFiles)
                {
                    if (_processedImageFiles.Contains(imagePath))
                    {
                        // Remove from set after 5 minutes to allow reprocessing if file is updated
                        System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                        {
                            lock (_processedImageFiles)
                            {
                                _processedImageFiles.Remove(imagePath);
                            }
                        });
                        return;
                    }
                    _processedImageFiles.Add(imagePath);
                }

                _logger.LogInformation("Processing new image file: {ImagePath}", imagePath);

                // Detect employee name and personnel ID from filename
            var (firstName, lastName) = DetectNameFromFolder(imagePath);
            var personnelId = DetectPersonnelIdFromFilename(imagePath);

            _logger.LogInformation("Detected name: {FirstName} {LastName}, ID: {PersonnelId} from path: {ImagePath}", 
                firstName ?? "NULL", lastName ?? "NULL", personnelId ?? "NULL", imagePath);

            if (firstName == null || lastName == null)
            {
                _logger.LogWarning("Could not detect employee name from image filename: {ImagePath}. Expected format: FirstName_LastName_PersonnelId.ext", imagePath);
                return;
            }

            // Check if employee already exists by name or personnel ID
            var existingEmployee = FindEmployeeByNameOrPersonnelId(firstName, lastName, personnelId);

            if (existingEmployee != null)
            {
                _logger.LogInformation("Found existing employee: {FullName} (ID: {EmployeeId})", existingEmployee.FullName, existingEmployee.EmployeeId);
                // Update existing employee's photo path if different
                if (existingEmployee.PhotoPath != imagePath)
                {
                    _logger.LogInformation("Updating photo path for existing employee: {FullName}", existingEmployee.FullName);
                    UpdateEmployee(existingEmployee.EmployeeId, photoPath: imagePath);
                }
                else
                {
                    _logger.LogInformation("Employee {FullName} already has this photo path", existingEmployee.FullName);
                }
            }
            else
            {
                // Create new employee automatically
                _logger.LogInformation("Auto-creating new employee from image: {FirstName} {LastName} (PersonnelId: {PersonnelId})",
                    firstName, lastName, personnelId ?? "none");

                // Check if file is already in the correct location with correct name format
                // If so, we can create employee directly without AddEmployee copying it
                // Reuse imagesFolder from above
                var expectedFileName = string.IsNullOrEmpty(personnelId)
                    ? $"{firstName}_{lastName}_"
                    : $"{firstName}_{lastName}_{personnelId}";
                var fileName = Path.GetFileNameWithoutExtension(imagePath);
                var isCorrectlyNamed = fileName.StartsWith(expectedFileName, StringComparison.OrdinalIgnoreCase);

                _logger.LogInformation("Is correctly named: {IsCorrectlyNamed}, Expected: {ExpectedFileName}, Actual: {FileName}", 
                    isCorrectlyNamed, expectedFileName, fileName);

                bool success;
                if (isCorrectlyNamed && imagePath.StartsWith(imagesFolder, StringComparison.OrdinalIgnoreCase))
                {
                    // File is already in correct location with correct name, create employee directly
                    var employeeId = $"emp_{Employees.Count}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                    var employee = new Employee(employeeId, firstName, lastName, "employee", "default", imagePath, false);
                    employee.PersonnelId = personnelId ?? "";
                    Employees[employeeId] = employee;
                    EmployeesUpdated?.Invoke();
                    SaveData();
                    success = true;
                    _logger.LogInformation("Created employee directly (file already in correct location): {FullName}", employee.FullName);
                }
                else
                {
                    _logger.LogInformation("Letting AddEmployee handle file copying/naming for: {FirstName} {LastName}", firstName, lastName);
                    // Let AddEmployee handle the file copying/naming
                    success = AddEmployee(
                        firstName: firstName,
                        lastName: lastName,
                        photoPath: imagePath,
                        personnelId: personnelId ?? ""
                    );
                }

                if (success)
                {
                    _logger.LogInformation("Successfully auto-created employee from image: {FirstName} {LastName}", firstName, lastName);
                }
                else
                {
                    _logger.LogWarning("Failed to auto-create employee from image: {FirstName} {LastName}", firstName, lastName);
                }
            }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image file: {ImagePath}", imagePath);
            }
        }

        private Employee? FindEmployeeByNameOrPersonnelId(string firstName, string lastName, string? personnelId)
        {
            // First try to find by personnel ID if provided
            if (!string.IsNullOrEmpty(personnelId))
            {
                var byPersonnelId = Employees.Values.FirstOrDefault(emp => 
                    !string.IsNullOrEmpty(emp.PersonnelId) && 
                    emp.PersonnelId.Equals(personnelId, StringComparison.OrdinalIgnoreCase));
                
                if (byPersonnelId != null)
                    return byPersonnelId;
            }

            // Then try to find by exact name match
            var byName = Employees.Values.FirstOrDefault(emp =>
                emp.FirstName.Equals(firstName, StringComparison.OrdinalIgnoreCase) &&
                emp.LastName.Equals(lastName, StringComparison.OrdinalIgnoreCase));

            return byName;
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

        /// <summary>
        /// Calculates the next rotation date based on the current rotation settings
        /// </summary>
        /// <returns>The next rotation date, or null if auto-rotation is disabled</returns>
        public DateTime? GetNextRotationDate()
        {
            try
            {
                if (!Settings.TryGetValue("auto_rotate_shifts", out var autoRotate) || 
                    !(autoRotate is bool enabled && enabled))
                {
                    return null;
                }

                var rotationDay = Settings.GetValueOrDefault("auto_rotate_day", "Saturday").ToString() ?? "Saturday";
                
                // Map English day name to DayOfWeek enum
                var dayMapping = new Dictionary<string, DayOfWeek>
                {
                    { "Sunday", DayOfWeek.Sunday },
                    { "Monday", DayOfWeek.Monday },
                    { "Tuesday", DayOfWeek.Tuesday },
                    { "Wednesday", DayOfWeek.Wednesday },
                    { "Thursday", DayOfWeek.Thursday },
                    { "Friday", DayOfWeek.Friday },
                    { "Saturday", DayOfWeek.Saturday }
                };
                
                if (!dayMapping.TryGetValue(rotationDay, out var targetDay))
                {
                    targetDay = DayOfWeek.Saturday; // Default
                }
                
                var today = DateTime.Now;
                var currentDay = today.DayOfWeek;
                
                // Calculate days until next rotation day
                int daysUntilTarget = ((int)targetDay - (int)currentDay + 7) % 7;
                
                // If today is the rotation day, check if rotation already happened today
                if (daysUntilTarget == 0)
                {
                    // If rotation already happened today, next rotation is next week
                    if (_lastRotationCheck.Date >= today.Date)
                    {
                        daysUntilTarget = 7;
                    }
                    // Otherwise, next rotation is today (but we'll return today + 0 days = today)
                }
                
                // If daysUntilTarget is 0, it means we're on the target day and rotation hasn't happened yet
                // Otherwise, add the calculated days
                return daysUntilTarget == 0 ? today : today.AddDays(daysUntilTarget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating next rotation date");
                return null;
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

                // Load daily task progress
                if (reportData.ContainsKey("daily_task_progress"))
                {
                    LoadDailyTaskProgressFromData(reportData["daily_task_progress"]);
                }

                // Ensure minimum capacity of 15 after all data is loaded
                if (ShiftManager.Capacity <= 0)
                {
                    ShiftManager.SetCapacity(15);
                }

                // Load status cards (from separate file)
                LoadStatusCards();

                _logger.LogInformation("Data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data");
                ShowErrorDialog("Error loading data", ex.Message);
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

                    // Load new properties (shield_color, show_shield, sticker_paths, medal_badge_path, personnel_id, phone, show_phone)
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

                    // Load phone number if present
                    if (employeeDict.TryGetValue("phone", out var phoneObj) && phoneObj != null)
                    {
                        employee.Phone = phoneObj.ToString() ?? string.Empty;
                    }

                    // Load ShowPhone flag if present, handling both bool and string representations
                    if (employeeDict.TryGetValue("show_phone", out var showPhoneObj) && showPhoneObj != null)
                    {
                        if (showPhoneObj is bool showPhoneBool)
                        {
                            employee.ShowPhone = showPhoneBool;
                        }
                        else if (bool.TryParse(showPhoneObj.ToString(), out var showPhoneParsed))
                        {
                            employee.ShowPhone = showPhoneParsed;
                        }
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
                var afternoonCount = ShiftManager.AfternoonShift.AssignedEmployees.Count(emp => emp != null);
                var nightCount = ShiftManager.NightShift.AssignedEmployees.Count(emp => emp != null);
                _logger.LogInformation("Loaded shifts - Morning: {MorningCount}, Afternoon: {AfternoonCount}, Night: {NightCount}, Capacity: {Capacity}", 
                    morningCount, afternoonCount, nightCount, ShiftManager.Capacity);
                
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

        private void LoadDailyTaskProgressFromData(object progressData)
        {
            try
            {
                var progressJson = JsonConvert.SerializeObject(progressData);
                DailyTaskProgressManager = DailyTaskProgressManager.FromJson(progressJson);
                _logger.LogInformation("Loaded daily task progress with {Count} records", DailyTaskProgressManager.ProgressRecords.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily task progress");
                DailyTaskProgressManager = new DailyTaskProgressManager();
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
                var afternoonTeamLeaderId = string.Empty; // ShiftManager.AfternoonShift.TeamLeaderId ?? string.Empty;
                var nightTeamLeaderId = string.Empty; // ShiftManager.NightShift.TeamLeaderId ?? string.Empty;
                
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
                    { "afternoon", new Dictionary<string, object>
                        {
                            { "shift_type", "afternoon" },
                            { "capacity", ShiftManager.AfternoonShift.Capacity },
                            { "team_leader_id", afternoonTeamLeaderId },
                            { "assigned_employees", ShiftManager.AfternoonShift.AssignedEmployees
                                .Where(emp => emp != null)
                                .Select(emp => emp.ToDictionary())
                                .Cast<object>()
                                .ToList()
                            }
                        }
                    },
                    { "night", new Dictionary<string, object>
                        {
                            { "shift_type", "night" },
                            { "capacity", ShiftManager.NightShift.Capacity },
                            { "team_leader_id", nightTeamLeaderId },
                            { "assigned_employees", ShiftManager.NightShift.AssignedEmployees
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
                var afternoonShift = shiftsData["afternoon"] as Dictionary<string, object>;
                var nightShift = shiftsData["night"] as Dictionary<string, object>;
                var morningCount = (morningShift?["assigned_employees"] as List<object>)?.Count ?? 0;
                var afternoonCount = (afternoonShift?["assigned_employees"] as List<object>)?.Count ?? 0;
                var nightCount = (nightShift?["assigned_employees"] as List<object>)?.Count ?? 0;
                
                _logger.LogInformation("Created shifts data with {MorningCount} morning, {AfternoonCount} afternoon, {NightCount} night employees", 
                    morningCount, afternoonCount, nightCount);

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
                    { "afternoon", new Dictionary<string, object>
                        {
                            { "shift_type", "afternoon" },
                            { "capacity", 15 },
                            { "assigned_employees", new List<object>() }
                        }
                    },
                    { "night", new Dictionary<string, object>
                        {
                            { "shift_type", "night" },
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
                var afternoonTeamLeaderId = string.Empty; // selectedGroup.AfternoonShift.TeamLeaderId ?? string.Empty;
                var nightTeamLeaderId = string.Empty; // selectedGroup.NightShift.TeamLeaderId ?? string.Empty;
                
                var groupData = new Dictionary<string, object>
                {
                    { "group_id", selectedGroup.GroupId },
                    { "name", selectedGroup.Name },
                    { "description", selectedGroup.Description },
                    { "color", selectedGroup.Color },
                    { "is_active", selectedGroup.IsActive },
                    { "morning_capacity", selectedGroup.MorningCapacity },
                    { "afternoon_capacity", selectedGroup.AfternoonCapacity },
                    { "night_capacity", selectedGroup.NightCapacity },
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
                    { "afternoon_shift", new Dictionary<string, object>
                        {
                            { "shift_type", "afternoon" },
                            { "capacity", selectedGroup.AfternoonCapacity },
                            { "team_leader_id", afternoonTeamLeaderId },
                            { "assigned_employees", selectedGroup.AfternoonShift.AssignedEmployees
                                .Where(emp => emp != null)
                                .Select(emp => emp.ToDictionary())
                                .Cast<object>()
                                .ToList()
                            }
                        }
                    },
                    { "night_shift", new Dictionary<string, object>
                        {
                            { "shift_type", "night" },
                            { "capacity", selectedGroup.NightCapacity },
                            { "team_leader_id", nightTeamLeaderId },
                            {  "assigned_employees", selectedGroup.NightShift.AssignedEmployees
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
                        .Where(emp => emp.Role.ToLower().StartsWith("manager"))
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

                // Populate transient SupervisorName and SupervisorPhotoPath for all shift groups before saving
                foreach (var group in ShiftGroupManager.GetAllShiftGroups())
                {
                    if (!string.IsNullOrEmpty(group.SupervisorId) && Employees.TryGetValue(group.SupervisorId, out var supervisor))
                    {
                        group.SupervisorName = supervisor.FullName;
                        group.SupervisorPhotoPath = supervisor.PhotoPath ?? string.Empty;
                    }
                    else
                    {
                        group.SupervisorName = string.Empty;
                        group.SupervisorPhotoPath = string.Empty;
                    }
                }

                // Prepare report data (include status_cards so DisplayApp can show them in shift cells)
                var statusCardsDict = new Dictionary<string, object>();
                if (StatusCards != null)
                {
                    foreach (var kvp in StatusCards)
                        statusCardsDict[kvp.Key] = kvp.Value.ToDictionary();
                }
                var reportData = new Dictionary<string, object>
                {
                    { "date", DateTime.Now.ToString("yyyy-MM-dd") },
                    { "employees", Employees.Values.Where(emp => !emp.IsManager && !emp.Role.ToLower().StartsWith("manager")).Select(emp => emp.ToDictionary()).Cast<object>().ToList() },
                    { "managers", managersToDisplay },
                    { "roles", JsonConvert.DeserializeObject<Dictionary<string, object>>(RoleManager.ToJson()) ?? new Dictionary<string, object>() },
                    { "shifts", CreateShiftsData() },
                    { "shift_groups", JsonConvert.DeserializeObject<Dictionary<string, object>>(ShiftGroupManager.ToJson()) ?? new Dictionary<string, object>() },
                    { "status_cards", statusCardsDict },
                    { "absences", JsonConvert.DeserializeObject<Dictionary<string, object>>(AbsenceManager.ToJson()) ?? new Dictionary<string, object>() },
                    { "tasks", JsonConvert.DeserializeObject<Dictionary<string, object>>(TaskManager.ToJson()) ?? new Dictionary<string, object>() },
                    { "daily_task_progress", JsonConvert.DeserializeObject<Dictionary<string, object>>(DailyTaskProgressManager.ToJson()) ?? new Dictionary<string, object>() },
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
                ShowErrorDialog("Error saving data", ex.Message);
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
                ShowErrorDialog("Error adding role", ex.Message);
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
                ShowErrorDialog("Error updating role", ex.Message);
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
                ShowErrorDialog("Error deleting role", ex.Message);
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

        #region Status Card Management Methods

        /// <summary>
        /// Loads status cards from the service.
        /// </summary>
        public void LoadStatusCards()
        {
            try
            {
                StatusCards = _statusCardService.LoadStatusCards();
                _logger.LogInformation("Loaded {Count} status cards", StatusCards.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading status cards");
            }
        }

        /// <summary>
        /// Adds a new status card.
        /// </summary>
        public bool AddStatusCard(string statusCardId, string name, string color = "#FF5722", string textColor = "#FFFFFF")
        {
            try
            {
                var success = _statusCardService.AddStatusCard(statusCardId, name, color, textColor);
                if (success)
                {
                    StatusCards = _statusCardService.GetStatusCardsDictionary();
                    StatusCardsUpdated?.Invoke();
                    _logger.LogInformation("Status card added: {Name}", name);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding status card");
                ShowErrorDialog("Error adding status card", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Updates an existing status card.
        /// </summary>
        public bool UpdateStatusCard(string statusCardId, string? name = null, string? color = null, string? textColor = null, bool? isActive = null)
        {
            try
            {
                var success = _statusCardService.UpdateStatusCard(statusCardId, name, color, textColor, isActive);
                if (success)
                {
                    StatusCards = _statusCardService.GetStatusCardsDictionary();
                    StatusCardsUpdated?.Invoke();
                    _logger.LogInformation("Status card updated: {StatusCardId}", statusCardId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status card");
                ShowErrorDialog("Error updating status card", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Deletes a status card.
        /// </summary>
        public bool DeleteStatusCard(string statusCardId)
        {
            try
            {
                var success = _statusCardService.DeleteStatusCard(statusCardId);
                if (success)
                {
                    StatusCards = _statusCardService.GetStatusCardsDictionary();
                    StatusCardsUpdated?.Invoke();
                    _logger.LogInformation("Status card deleted: {StatusCardId}", statusCardId);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting status card");
                ShowErrorDialog("Error deleting status card", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Gets a status card by ID.
        /// </summary>
        public StatusCard? GetStatusCard(string statusCardId)
        {
            return StatusCards.TryGetValue(statusCardId, out var card) ? card : null;
        }

        /// <summary>
        /// Gets all status cards.
        /// </summary>
        public List<StatusCard> GetAllStatusCards()
        {
            return StatusCards.Values.ToList();
        }

        /// <summary>
        /// Gets only active status cards.
        /// </summary>
        public List<StatusCard> GetActiveStatusCards()
        {
            return StatusCards.Values.Where(c => c.IsActive).ToList();
        }

        /// <summary>
        /// Assigns a status card to a shift slot. Clears any existing employee in that slot.
        /// </summary>
        public bool AssignStatusCardToShift(string statusCardId, string groupId, string shiftType, int slotIndex)
        {
            try
            {
                var group = ShiftGroupManager.GetShiftGroup(groupId);
                if (group == null)
                {
                    _logger.LogWarning("Cannot assign status card: Group {GroupId} not found", groupId);
                    return false;
                }

                var shift = group.GetShift(shiftType);
                if (shift == null)
                {
                    _logger.LogWarning("Cannot assign status card: Shift {ShiftType} not found in group {GroupId}", shiftType, groupId);
                    return false;
                }

                var success = shift.AssignStatusCardToSlot(statusCardId, slotIndex);
                if (success)
                {
                    ShiftsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Status card {StatusCardId} assigned to {GroupId}/{ShiftType}/slot {SlotIndex}", 
                        statusCardId, groupId, shiftType, slotIndex);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning status card to shift");
                return false;
            }
        }

        /// <summary>
        /// Removes a status card from a shift slot.
        /// </summary>
        public bool RemoveStatusCardFromShift(string groupId, string shiftType, int slotIndex)
        {
            try
            {
                var group = ShiftGroupManager.GetShiftGroup(groupId);
                if (group == null)
                {
                    _logger.LogWarning("Cannot remove status card: Group {GroupId} not found", groupId);
                    return false;
                }

                var shift = group.GetShift(shiftType);
                if (shift == null)
                {
                    _logger.LogWarning("Cannot remove status card: Shift {ShiftType} not found in group {GroupId}", shiftType, groupId);
                    return false;
                }

                var success = shift.ClearStatusCardFromSlot(slotIndex);
                if (success)
                {
                    ShiftsUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Status card removed from {GroupId}/{ShiftType}/slot {SlotIndex}", 
                        groupId, shiftType, slotIndex);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing status card from shift");
                return false;
            }
        }

        /// <summary>
        /// Gets the status card at a specific shift slot.
        /// </summary>
        public StatusCard? GetStatusCardAtSlot(string groupId, string shiftType, int slotIndex)
        {
            try
            {
                var group = ShiftGroupManager.GetShiftGroup(groupId);
                if (group == null) return null;

                var shift = group.GetShift(shiftType);
                if (shift == null) return null;

                var statusCardId = shift.GetStatusCardAtSlot(slotIndex);
                if (string.IsNullOrEmpty(statusCardId)) return null;

                return GetStatusCard(statusCardId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status card at slot");
                return null;
            }
        }

        #endregion

        // Shift Group Management Methods
        public bool AddShiftGroup(string groupId, string name, string description = "", string supervisorId = "", string color = "#4CAF50", 
                                 int morningCapacity = 15, int afternoonCapacity = 15, int nightCapacity = 15)
        {
            try
            {
                var success = ShiftGroupManager.AddShiftGroup(groupId, name, description, color, morningCapacity, afternoonCapacity, nightCapacity, supervisorId);
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
                ShowErrorDialog("Error adding shift group", ex.Message);
                return false;
            }
        }

        public bool UpdateShiftGroup(string groupId, string? name = null, string? description = null, string? supervisorId = null,
                                    string? color = null, int? morningCapacity = null, int? afternoonCapacity = null, 
                                    int? nightCapacity = null, bool? isActive = null,
                                    string? morningForemanId = null, string? afternoonForemanId = null, string? nightForemanId = null)
        {
            try
            {
                var success = ShiftGroupManager.UpdateShiftGroup(groupId, name, description, color, morningCapacity, afternoonCapacity, nightCapacity, supervisorId, isActive, morningForemanId, afternoonForemanId, nightForemanId);
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
                ShowErrorDialog("Error updating shift group", ex.Message);
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
                ShowErrorDialog("Error deleting shift group", ex.Message);
                return false;
            }
        }

        public ShiftGroup? GetShiftGroup(string groupId)
        {
            return ShiftGroupManager.GetShiftGroup(groupId);
        }

        public List<ShiftGroup> GetAllShiftGroups()
        {
            var groups = ShiftGroupManager.GetAllShiftGroups();
            // Populate transient SupervisorName and SupervisorPhotoPath
            foreach (var group in groups)
            {
                if (!string.IsNullOrEmpty(group.SupervisorId) && Employees.TryGetValue(group.SupervisorId, out var supervisor))
                {
                    group.SupervisorName = supervisor.FullName;
                    group.SupervisorPhotoPath = supervisor.PhotoPath ?? string.Empty;
                }
                else
                {
                    group.SupervisorName = string.Empty;
                    group.SupervisorPhotoPath = string.Empty;
                }
            }
            return groups;
        }

        public bool AssignSupervisor(string groupId, string employeeId)
        {
            try
            {
                // Validate employee exists
                if (!string.IsNullOrEmpty(employeeId) && !Employees.ContainsKey(employeeId))
                {
                    _logger.LogWarning("Supervisor assignment failed: Employee {EmployeeId} not found", employeeId);
                    return false;
                }
                
                // Update specific field
                return UpdateShiftGroup(groupId, supervisorId: employeeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning supervisor");
                return false;
            }
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
                               string shieldColor = "Blue", bool showShield = true, List<string>? stickerPaths = null, string medalBadgePath = "", string personnelId = "",
                               string phone = "", bool showPhone = true)
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
                employee.Phone = phone;
                employee.ShowPhone = showPhone;

                // Get the flat images folder: Data/Images/Staff/
                var workersRoot = GetEmployeeImagesFolder();

                // If photo path is provided and file exists, copy it to staff folder
                if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
                {
                    // Format: FirstName_LastName_PersonnelId.ext (use timestamp if PersonnelId is empty)
                    var fileNamePart = string.IsNullOrEmpty(personnelId) 
                        ? $"{firstName}_{lastName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}"
                        : $"{firstName}_{lastName}_{personnelId}";
                    var photoFileName = $"{fileNamePart}{Path.GetExtension(photoPath)}";
                    var destPhotoPath = Path.Combine(workersRoot, photoFileName);
                    
                    // Only copy if source is different from destination
                    if (!Path.GetFullPath(photoPath).Equals(Path.GetFullPath(destPhotoPath), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(photoPath, destPhotoPath, true);
                    }
                    employee.PhotoPath = destPhotoPath;
                    _logger.LogInformation("Copied photo to staff folder: {Path}", destPhotoPath);
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
                ShowErrorDialog("Error adding employee", ex.Message);
                return false;
            }
        }

        public bool UpdateEmployee(string employeeId, string? firstName = null, string? lastName = null, string? roleId = null, string? shiftGroupId = null, string? photoPath = null, bool? isManager = null,
                                  string? shieldColor = null, bool? showShield = null, List<string>? stickerPaths = null, string? medalBadgePath = null, string? personnelId = null,
                                  string? phone = null, bool? showPhone = null)
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
                var newPersonnelId = personnelId ?? employee.PersonnelId;

                var imagesRoot = GetEmployeeImagesFolder();
                
                // Handle Name/ID change for existing photo if it's in our managed directory
                if (!string.IsNullOrEmpty(employee.PhotoPath) && employee.PhotoPath.StartsWith(imagesRoot, StringComparison.OrdinalIgnoreCase))
                {
                    if (firstName != null || lastName != null || personnelId != null)
                    {
                        var ext = Path.GetExtension(employee.PhotoPath);
                        var newFileNamePart = string.IsNullOrEmpty(newPersonnelId)
                            ? $"{newFirstName}_{newLastName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}"
                            : $"{newFirstName}_{newLastName}_{newPersonnelId}";
                        var newPhotoPath = Path.Combine(imagesRoot, $"{newFileNamePart}{ext}");
                        
                        if (!string.Equals(employee.PhotoPath, newPhotoPath, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                if (File.Exists(employee.PhotoPath))
                                {
                                    File.Move(employee.PhotoPath, newPhotoPath);
                                    employee.PhotoPath = newPhotoPath;
                                    _logger.LogInformation("Renamed photo file to match new employee data: {Path}", newPhotoPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to rename photo file during update");
                            }
                        }
                    }
                }

                // Handle New Photo
                if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
                {
                    // Check if photo is already in the managed folder
                    var isInImagesRoot = photoPath.StartsWith(imagesRoot, StringComparison.OrdinalIgnoreCase);
                    
                    if (!isInImagesRoot)
                    {
                        var fileNamePart = string.IsNullOrEmpty(newPersonnelId)
                            ? $"{newFirstName}_{newLastName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}"
                            : $"{newFirstName}_{newLastName}_{newPersonnelId}";
                        var photoFileName = $"{fileNamePart}{Path.GetExtension(photoPath)}";
                        var destPhotoPath = Path.Combine(imagesRoot, photoFileName);
                        
                        if (!Path.GetFullPath(photoPath).Equals(Path.GetFullPath(destPhotoPath), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(photoPath, destPhotoPath, true);
                        }
                        photoPath = destPhotoPath;
                        _logger.LogInformation("Copied new photo to staff folder: {Path}", destPhotoPath);
                    }
                }

                employee.Update(firstName, lastName, roleId, shiftGroupId, photoPath, isManager, shieldColor, showShield, stickerPaths, medalBadgePath, personnelId, null, phone, showPhone);

                EmployeesUpdated?.Invoke();
                SaveData();

                _logger.LogInformation("Employee updated: {FullName}", employee.FullName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee");
                ShowErrorDialog("Error updating employee", ex.Message);
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
                
                _logger.LogInformation("Removing employee from afternoon shift");
                ShiftManager.RemoveEmployeeFromShift(employee, "afternoon");
                
                _logger.LogInformation("Removing employee from night shift");
                ShiftManager.RemoveEmployeeFromShift(employee, "night");

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
                ShowErrorDialog("Error deleting employee", ex.Message);
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
                ShowErrorDialog("Error importing CSV", ex.Message);
                return (0, 0);
            }
        }

        // Shift Management Methods
        public AssignmentResult AssignEmployeeToShift(Employee employee, string shiftType, int? slotIndex = null, string? groupId = null)
        {
            try
            {
                // Use employee's group if not specified
                var targetGroupId = groupId ?? employee.ShiftGroupId;
                
                // Check if shift group exists
                var group = ShiftGroupManager.GetShiftGroup(targetGroupId);
                if (group == null)
                {
                    ShowErrorDialog("Error", "Shift group not found");
                    return new AssignmentResult
                    {
                        Success = false,
                        ErrorMessage = "Shift group not found"
                    };
                }

                // Check if shift exists in the group
                var shift = group.GetShift(shiftType);
                if (shift == null)
                {
                    ShowErrorDialog("Error", "Shift not found");
                    return new AssignmentResult
                    {
                        Success = false,
                        ErrorMessage = "Shift not found"
                    };
                }

                // Check if employee is marked as absent
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                if (AbsenceManager.HasAbsenceForEmployee(employee, today))
                {
                    var absence = AbsenceManager.GetAbsenceForEmployee(employee, today);
                    var absenceType = absence?.Category ?? "Absent";
                    return new AssignmentResult
                    {
                        Success = false,
                        Conflict = new AssignmentConflict
                        {
                            Type = ConflictType.Absent,
                            AbsenceType = absenceType
                        }
                    };
                }

                // Check if employee is already assigned to another shift in this group
                var currentShifts = group.GetEmployeeShifts(employee);
                if (currentShifts.Any() && !currentShifts.Contains(shiftType))
                {
                    var conflictingShift = currentShifts.First();
                    return new AssignmentResult
                    {
                        Success = false,
                        Conflict = new AssignmentConflict
                        {
                            Type = ConflictType.DifferentShift,
                            CurrentShiftType = conflictingShift
                        }
                    };
                }

                // Check if employee is assigned to a different group
                var currentGroupId = ShiftGroupManager.GetEmployeeGroup(employee);
                if (currentGroupId != null && currentGroupId != targetGroupId)
                {
                    var currentGroup = ShiftGroupManager.GetShiftGroup(currentGroupId);
                    return new AssignmentResult
                    {
                        Success = false,
                        Conflict = new AssignmentConflict
                        {
                            Type = ConflictType.DifferentGroup,
                            CurrentGroupId = currentGroupId,
                            CurrentGroupName = currentGroup?.Name
                        }
                    };
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
                    return new AssignmentResult { Success = true };
                }
                else
                {
                    ShowErrorDialog("Error", "Cannot add employee to this shift");
                    return new AssignmentResult
                    {
                        Success = false,
                        ErrorMessage = "Cannot add employee to this shift"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning employee to shift");
                ShowErrorDialog("Error assigning to shift", ex.Message);
                return new AssignmentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
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
                ShowErrorDialog("Error removing from shift", ex.Message);
                return false;
            }
        }

        public bool RemoveEmployeeFromPreviousAssignment(Employee employee, AssignmentConflict conflict, string? targetGroupId = null)
        {
            try
            {
                bool removed = false;

                switch (conflict.Type)
                {
                    case ConflictType.Absent:
                        // Remove today's absence
                        var today = DateTime.Now.ToString("yyyy-MM-dd");
                        var absences = AbsenceManager.GetAbsencesByEmployee(employee);
                        var todayAbsence = absences.FirstOrDefault(a => a.Date == today);
                        if (todayAbsence != null)
                        {
                            AbsenceManager.RemoveAbsence(todayAbsence);
                            AbsencesUpdated?.Invoke();
                            removed = true;
                            _logger.LogInformation("Removed absence for {EmployeeName}", employee.FullName);
                        }
                        break;

                    case ConflictType.DifferentShift:
                        // Remove employee from the conflicting shift in the current group
                        // For DifferentShift, employee is in the same group as target, so we can use targetGroupId or get from employee
                        var groupIdForShift = targetGroupId ?? ShiftGroupManager.GetEmployeeGroup(employee);
                        if (groupIdForShift != null && conflict.CurrentShiftType != null)
                        {
                            var shiftRemoved = RemoveEmployeeFromShift(employee, conflict.CurrentShiftType, groupIdForShift);
                            if (shiftRemoved)
                            {
                                removed = true;
                                _logger.LogInformation("Removed {EmployeeName} from {ShiftType} shift in group {GroupId}", 
                                    employee.FullName, conflict.CurrentShiftType, groupIdForShift);
                            }
                        }
                        break;

                    case ConflictType.DifferentGroup:
                        // Remove employee from all shifts in the previous group
                        if (conflict.CurrentGroupId != null)
                        {
                            var previousGroup = ShiftGroupManager.GetShiftGroup(conflict.CurrentGroupId);
                            if (previousGroup != null)
                            {
                                var employeeShifts = previousGroup.GetEmployeeShifts(employee);
                                foreach (var shiftType in employeeShifts)
                                {
                                    var shiftRemoved = RemoveEmployeeFromShift(employee, shiftType, conflict.CurrentGroupId);
                                    if (shiftRemoved)
                                    {
                                        removed = true;
                                        _logger.LogInformation("Removed {EmployeeName} from {ShiftType} shift in group {GroupId}", 
                                            employee.FullName, shiftType, conflict.CurrentGroupId);
                                    }
                                }
                            }
                        }
                        break;
                }

                if (removed)
                {
                    SaveData();
                }

                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing employee from previous assignment");
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
                ShowErrorDialog("Error clearing shift", ex.Message);
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
                Settings["afternoon_capacity"] = capacity;
                Settings["night_capacity"] = capacity;

                ShiftManager.SetCapacity(capacity);
                
                // Update all shift groups' capacities to match the new global capacity
                foreach (var group in ShiftGroupManager.GetAllShiftGroups())
                {
                    group.Update(morningCapacity: capacity, afternoonCapacity: capacity, nightCapacity: capacity);
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
                        ShiftGroupManager.RemoveEmployeeFromShift(employee, assignedGroupId, "afternoon");
                        ShiftGroupManager.RemoveEmployeeFromShift(employee, assignedGroupId, "night");
                    }
                    else
                    {
                        _logger.LogInformation("Employee {FullName} not found in any shift group", employee.FullName);
                    }
                    
                    // Also remove from old ShiftManager for backward compatibility
                    ShiftManager.RemoveEmployeeFromShift(employee, "morning");
                    ShiftManager.RemoveEmployeeFromShift(employee, "afternoon");
                    ShiftManager.RemoveEmployeeFromShift(employee, "night");

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
                ShowErrorDialog("Error recording absence", ex.Message);
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
                ShowErrorDialog("Error removing absence", ex.Message);
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
        public string AddTask(string title, string description = "", string priority = "Medium", 
                             double estimatedHours = 8.0, string? targetDate = null)
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
                ShowErrorDialog("Error adding task", ex.Message);
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
                    // Convert status strings to enum
                    switch (status)
                    {
                        case "Pending":
                            statusEnum = Shared.Models.TaskStatus.Pending;
                            break;
                        case "In Progress":
                            statusEnum = Shared.Models.TaskStatus.InProgress;
                            break;
                        case "Completed":
                            statusEnum = Shared.Models.TaskStatus.Completed;
                            break;
                        case "Cancelled":
                            statusEnum = Shared.Models.TaskStatus.Cancelled;
                            break;
                        default:
                            if (Enum.TryParse<Shared.Models.TaskStatus>(status, true, out var parsedStatus))
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
                ShowErrorDialog("Error updating task", ex.Message);
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
                ShowErrorDialog("Error deleting task", ex.Message);
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
                var currentAfternoonAssignments = ShiftManager.AfternoonShift.AssignedEmployees.Select(emp => emp?.EmployeeId).ToList();
                var currentNightAssignments = ShiftManager.NightShift.AssignedEmployees.Select(emp => emp?.EmployeeId).ToList();
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
                    Settings["afternoon_capacity"] = currentCapacity;
                    Settings["night_capacity"] = currentCapacity;
                }

                // Restore shift assignments if they were lost during reload
                if (currentMorningAssignments.Any() || currentAfternoonAssignments.Any() || currentNightAssignments.Any())
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

                    // Restore afternoon  shift assignments
                    for (int i = 0; i < currentAfternoonAssignments.Count && i < ShiftManager.AfternoonShift.AssignedEmployees.Count; i++)
                    {
                        var empId = currentAfternoonAssignments[i];
                        if (!string.IsNullOrEmpty(empId) && Employees.ContainsKey(empId) && ShiftManager.AfternoonShift.AssignedEmployees[i] == null)
                        {
                            ShiftManager.AfternoonShift.AssignedEmployees[i] = Employees[empId];
                        }
                    }
                    
                    // Restore night shift assignments
                    for (int i = 0; i < currentNightAssignments.Count && i < ShiftManager.NightShift.AssignedEmployees.Count; i++)
                    {
                        var empId = currentNightAssignments[i];
                        if (!string.IsNullOrEmpty(empId) && Employees.ContainsKey(empId) && ShiftManager.NightShift.AssignedEmployees[i] == null)
                        {
                            ShiftManager.NightShift.AssignedEmployees[i] = Employees[empId];
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
                ShowErrorDialog("Error assigning task", ex.Message);
                return false;
            }
        }

        public List<Employee> GetEmployeesFromShiftGroup(string groupId)
        {
            try
            {
                var group = ShiftGroupManager.GetShiftGroup(groupId);
                if (group == null)
                {
                    _logger.LogWarning("Shift group not found: {GroupId}", groupId);
                    return new List<Employee>();
                }

                var employees = new List<Employee>();

                // Get employees from morning shift
                if (group.MorningShift != null)
                {
                    employees.AddRange(group.MorningShift.AssignedEmployees
                        .Where(emp => emp != null)
                        .Cast<Employee>());
                }

                // Get employees from afternoon shift
                if (group.AfternoonShift != null)
                {
                    employees.AddRange(group.AfternoonShift.AssignedEmployees
                        .Where(emp => emp != null)
                        .Cast<Employee>());
                }

                // Get employees from night shift
                if (group.NightShift != null)
                {
                    employees.AddRange(group.NightShift.AssignedEmployees
                        .Where(emp => emp != null)
                        .Cast<Employee>());
                }

                // Return unique employees (in case someone is in both shifts, though unlikely)
                return employees.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees from shift group {GroupId}", groupId);
                return new List<Employee>();
            }
        }

        public bool AssignTaskToShiftGroup(string taskId, string groupId)
        {
            try
            {
                var task = TaskManager.GetTask(taskId);
                if (task == null)
                {
                    _logger.LogWarning("Task not found: {TaskId}", taskId);
                    return false;
                }

                var employees = GetEmployeesFromShiftGroup(groupId);
                if (employees.Count == 0)
                {
                    _logger.LogWarning("No employees found in shift group {GroupId}", groupId);
                    return false;
                }

                bool allSuccess = true;
                int successCount = 0;
                foreach (var employee in employees)
                {
                    var success = AssignTaskToEmployee(taskId, employee.EmployeeId);
                    if (success)
                    {
                        successCount++;
                    }
                    else
                    {
                        allSuccess = false;
                    }
                }

                if (successCount > 0)
                {
                    TasksUpdated?.Invoke();
                    SaveData();
                    _logger.LogInformation("Task {TaskId} assigned to {Count} employees from shift group {GroupId}", 
                        taskId, successCount, groupId);
                }

                return allSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning task to shift group");
                ShowErrorDialog("Error assigning task to group", ex.Message);
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
                ShowErrorDialog("Error removing task assignment", ex.Message);
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

        // Daily Task Progress Methods
        public bool RecordDailyProgress(string groupId, string shiftType, string date, int completedBoxes)
        {
            try
            {
                var progressId = $"{groupId}_{shiftType}_{date}";
                var existingProgress = DailyTaskProgressManager.GetProgress(progressId);
                
                if (existingProgress != null)
                {
                    DailyTaskProgressManager.UpdateProgress(progressId, completedBoxes);
                }
                else
                {
                    var progress = new DailyTaskProgress(groupId, shiftType, date, completedBoxes);
                    DailyTaskProgressManager.AddProgress(progress);
                }
                
                SaveData();
                _logger.LogInformation("Recorded daily progress: Group={GroupId}, Shift={ShiftType}, Date={Date}, Boxes={CompletedBoxes}", 
                    groupId, shiftType, date, completedBoxes);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording daily progress");
                return false;
            }
        }

        public DailyTaskProgress? GetDailyProgress(string groupId, string shiftType, string date)
        {
            return DailyTaskProgressManager.GetProgress(groupId, shiftType, date);
        }

        public List<DailyTaskProgress> GetWeeklyProgress(string groupId, string shiftType, string weekStartDate)
        {
            return DailyTaskProgressManager.GetWeeklyProgress(groupId, shiftType, weekStartDate);
        }

        public ProgressStatus CalculateProgressStatus(string groupId, string shiftType, string date)
        {
            var progress = GetDailyProgress(groupId, shiftType, date);
            var completed = progress?.CompletedBoxes ?? 0;
            var target = progress?.DailyTarget ?? 100;
            
            var percentage = target > 0 ? (completed / (double)target * 100) : 0;
            var difference = completed - target;
            
            var status = new ProgressStatus
            {
                Completed = completed,
                Target = target,
                Percentage = Math.Round(percentage, 1),
                Difference = difference
            };
            
            if (completed > target)
            {
                status.IsAhead = true;
                status.StatusText = "Ahead";
            }
            else if (completed < target)
            {
                status.IsBehind = true;
                status.StatusText = "Behind";
            }
            else
            {
                status.IsOnTrack = true;
                status.StatusText = "On track";
            }
            
            return status;
        }

        public WeeklyProgressStatus GetWeeklyProgressStatus(string groupId, string shiftType, string weekStartDate)
        {
            var weeklyProgress = GetWeeklyProgress(groupId, shiftType, weekStartDate);
            var totalCompleted = weeklyProgress.Sum(p => p.CompletedBoxes);
            var weeklyTarget = 1000;
            
            var percentage = weeklyTarget > 0 ? (totalCompleted / (double)weeklyTarget * 100) : 0;
            var difference = totalCompleted - weeklyTarget;
            
            var status = new WeeklyProgressStatus
            {
                GroupId = groupId,
                ShiftType = shiftType,
                WeekStartDate = weekStartDate,
                TotalCompleted = totalCompleted,
                WeeklyTarget = weeklyTarget,
                Percentage = Math.Round(percentage, 1),
                Difference = difference,
                DailyProgress = weeklyProgress
            };
            
            if (totalCompleted > weeklyTarget)
            {
                status.IsAhead = true;
                status.StatusText = "Ahead";
            }
            else if (totalCompleted < weeklyTarget)
            {
                status.IsBehind = true;
                status.StatusText = "Behind";
            }
            else
            {
                status.IsOnTrack = true;
                status.StatusText = "On track";
            }
            
            return status;
        }

        public string GetWeekStartDate(string date)
        {
            // Get the date as DateTime (Shamsi)
            var currentDate = Shared.Utils.ShamsiDateHelper.FromShamsiString(date);
            
            // Find the most recent Saturday (week start in Persian calendar)
            // DayOfWeek: Sunday = 0, Monday = 1, ..., Saturday = 6
            var dayOfWeek = (int)currentDate.DayOfWeek;
            // Calculate days to subtract to get to Saturday (6)
            // If today is Saturday, subtract 0; if Sunday, subtract 1; if Monday, subtract 2; etc.
            var daysToSubtract = (dayOfWeek + 1) % 7;
            var weekStart = currentDate.AddDays(-daysToSubtract);
            
            return Shared.Utils.ShamsiDateHelper.ToShamsiString(weekStart);
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


        public string GetEmployeeImagesFolder()
        {
            var imagesDir = Path.Combine(_dataDir, "Images");
            var staffDir = Path.Combine(imagesDir, "Staff");
            
            if (!Directory.Exists(imagesDir))
            {
                Directory.CreateDirectory(imagesDir);
            }
            
            if (!Directory.Exists(staffDir))
            {
                Directory.CreateDirectory(staffDir);
            }
            
            return staffDir;
        }

        public (string? FirstName, string? LastName) DetectNameFromFolder(string filePath)
        {
            try
            {
                // Parse from the filename itself: FirstName_LastName_...
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    var parts = fileName.Split('_');
                    if (parts.Length >= 2)
                    {
                        var first = parts[0].Trim();
                        var last = parts[1].Trim();
                        if (first.Length > 0 && last.Length > 0)
                        {
                            return (first, last);
                        }
                    }
                }

                // Fallback to parent folder check (for backward compatibility)
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    var folderName = Path.GetFileName(directory);
                    var parts = folderName.Split('_');
                    if (parts.Length >= 2)
                    {
                        return (parts[0], parts[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting name from file path: {FilePath}", filePath);
            }

            return (null, null);
        }

        public string? DetectPersonnelIdFromFilename(string filePath)
        {
            try
            {
                // Parse filename format: FirstName_LastName_PersonnelId.ext
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(fileName))
                    return null;

                var parts = fileName.Split('_');
                // Need at least 3 parts: FirstName_LastName_PersonnelId
                if (parts.Length >= 3)
                {
                    // The last part should be the personnel ID (or timestamp if PersonnelId was empty)
                    var personnelId = parts[parts.Length - 1];
                    // Validate that it's a numeric value (personnel ID or timestamp)
                    // If it's all digits, treat it as PersonnelId (could also be timestamp, but we'll treat it as PersonnelId)
                    if (!string.IsNullOrEmpty(personnelId) && personnelId.All(char.IsDigit))
                    {
                        return personnelId;
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
                if (string.IsNullOrEmpty(groupId) || groupId == "all")
                {
                    foreach (var group in ShiftGroupManager.GetActiveShiftGroups())
                    {
                        group.SwapShifts();
                        _logger.LogInformation("Rotated shifts for group {GroupId}", group.GroupId);
                    }
                }
                else
                {
                    var group = ShiftGroupManager.GetShiftGroup(groupId);
                    if (group == null)
                        return false;
                    group.SwapShifts();
                    _logger.LogInformation("Rotated shifts for group {GroupId}", groupId);
                }

                ShiftsUpdated?.Invoke();
                ShiftGroupsUpdated?.Invoke();
                SaveData();
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
                    ShowErrorDialog("Error", $"ID card template not found: {templatePath}");
                    return null;
                }

                // Determine output path
                if (string.IsNullOrEmpty(outputPath))
                {
                    var badgesDir = Path.Combine(_dataDir, "Badges");
                    if (!Directory.Exists(badgesDir))
                    {
                        Directory.CreateDirectory(badgesDir);
                    }
                    var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                    outputPath = Path.Combine(badgesDir, $"{employee.FirstName}_{employee.LastName}_badge_{timestamp}.png");
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
                    ShowErrorDialog("Error", "Error generating ID card");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating badge for employee {EmployeeId}", employeeId);
                ShowErrorDialog("Error", $"Error generating ID card: {ex.Message}");
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
                
                // Stop current image watcher
                _imageWatcher?.Dispose();
                _imageWatcher = null;
                
                // Create new sync manager with new path
                _syncManager = new SyncManager(_dataDir);
                SetupSync();
                
                // Restart image watcher with new path
                SetupImageWatcher();
                
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
