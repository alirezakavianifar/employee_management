using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Utils;

namespace Shared.Services
{
    public class JsonHandler
    {
        private readonly string _dataDir;
        private readonly string _reportsDir;
        private readonly string _imagesDir;
        private readonly string _logsDir;
        private readonly ILogger<JsonHandler> _logger;

        public JsonHandler(string dataDir = "Data")
        {
            _dataDir = dataDir;
            _reportsDir = Path.Combine(dataDir, "Reports");
            _imagesDir = Path.Combine(dataDir, "Images", "Staff");
            _logsDir = Path.Combine(dataDir, "Logs");

            // Ensure directories exist
            Directory.CreateDirectory(_reportsDir);
            Directory.CreateDirectory(_imagesDir);
            Directory.CreateDirectory(_logsDir);

            _logger = LoggingService.CreateLogger<JsonHandler>();
            _logger.LogInformation("JsonHandler initialized with data directory: {DataDir}", _dataDir);
        }

        public string GetTodayFilename()
        {
            // Use Persian calendar date for consistency with existing files
            // Format: yyyy-MM-dd (e.g., 1404-06-22)
            var persianDate = ShamsiDateHelper.GetCurrentShamsiDate().Replace("/", "-");
            return $"report_{persianDate}.json";
        }

        public string GetTodayFilepath()
        {
            return Path.Combine(_reportsDir, GetTodayFilename());
        }

        public string CreateBackup(string filepath)
        {
            if (!File.Exists(filepath))
                return string.Empty;

            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFilename = $"{Path.GetFileNameWithoutExtension(filepath)}_backup_{timestamp}.json";
                var backupPath = Path.Combine(_reportsDir, backupFilename);

                File.Copy(filepath, backupPath, true);
                _logger.LogInformation("Backup created: {BackupPath}", backupPath);
                return backupPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create backup for {FilePath}", filepath);
                return string.Empty;
            }
        }

        public Dictionary<string, object>? ReadJson(string filepath)
        {
            try
            {
                if (!File.Exists(filepath))
                {
                    _logger.LogWarning("File does not exist: {FilePath}", filepath);
                    return null;
                }

                var json = File.ReadAllText(filepath, System.Text.Encoding.UTF8);
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                
                _logger.LogInformation("Successfully read JSON file: {FilePath}", filepath);
                return data;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON decode error in {FilePath}", filepath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading {FilePath}", filepath);
                return null;
            }
        }

        public bool WriteJson(string filepath, Dictionary<string, object> data)
        {
            try
            {
                // Create backup if file exists
                if (File.Exists(filepath))
                {
                    CreateBackup(filepath);
                }

                // Write new data
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(filepath, json, System.Text.Encoding.UTF8);

                _logger.LogInformation("Successfully wrote JSON file: {FilePath}", filepath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing {FilePath}", filepath);
                return false;
            }
        }

        public Dictionary<string, object>? ReadTodayReport()
        {
            return ReadJson(GetTodayFilepath());
        }

        public bool WriteTodayReport(Dictionary<string, object> data)
        {
            return WriteJson(GetTodayFilepath(), data);
        }

        public Dictionary<string, object> GetDefaultReportStructure()
        {
            try
            {
                // Try to load sample employees from CSV
                var sampleEmployees = LoadSampleEmployees();

                // Separate employees and managers
                var employees = new List<object>();
                var managers = new List<object>();

                foreach (var emp in sampleEmployees)
                {
                    var empDict = emp as Dictionary<string, object>;
                    var role = empDict?.GetValueOrDefault("role", "").ToString() ?? "";
                    
                    if (role.ToLower().StartsWith("مدیر") || role.ToLower().StartsWith("manager"))
                    {
                        managers.Add(emp);
                    }
                    else
                    {
                        employees.Add(emp);
                    }
                }

                _logger.LogInformation("Loaded {EmployeeCount} employees and {ManagerCount} managers from sample data", 
                    employees.Count, managers.Count);

                return new Dictionary<string, object>
                {
                    { "date", ShamsiDateHelper.GetCurrentShamsiDate().Replace("/", "-") },
                    { "employees", employees },
                    { "managers", managers },
                    { "shifts", new Dictionary<string, object>
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
                        }
                    },
                    { "absences", new Dictionary<string, object>
                        {
                            { "مرخصی", new List<object>() },
                            { "بیمار", new List<object>() },
                            { "غایب", new List<object>() }
                        }
                    },
                    { "tasks", new Dictionary<string, object>
                        {
                            { "tasks", new Dictionary<string, object>() },
                            { "next_task_id", 1 }
                        }
                    },
                    { "settings", new Dictionary<string, object>
                        {
                            { "shift_capacity", 5 },
                            { "morning_capacity", 5 },
                            { "evening_capacity", 5 },
                            { "shared_folder_path", _dataDir }
                        }
                    },
                    { "last_modified", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sample data");
                return GetEmptyReportStructure();
            }
        }

        private Dictionary<string, object> GetEmptyReportStructure()
        {
            return new Dictionary<string, object>
            {
                { "date", ShamsiDateHelper.GetCurrentShamsiDate().Replace("/", "-") },
                { "employees", new List<object>() },
                { "managers", new List<object>() },
                { "shifts", new Dictionary<string, object>
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
                    }
                },
                { "absences", new Dictionary<string, object>
                    {
                        { "مرخصی", new List<object>() },
                        { "بیمار", new List<object>() },
                        { "غایب", new List<object>() }
                    }
                },
                { "tasks", new Dictionary<string, object>
                    {
                        { "tasks", new Dictionary<string, object>() },
                        { "next_task_id", 1 }
                    }
                },
                { "settings", new Dictionary<string, object>
                    {
                        { "shift_capacity", 5 },
                        { "morning_capacity", 5 },
                        { "evening_capacity", 5 },
                        { "shared_folder_path", _dataDir }
                    }
                },
                { "last_modified", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
            };
        }

        private List<object> LoadSampleEmployees()
        {
            try
            {
                // Look for sample_employees.csv in multiple possible locations
                var possiblePaths = new[]
                {
                    Path.Combine(_dataDir, "sample_employees.csv"),
                    Path.Combine(Path.GetDirectoryName(_dataDir) ?? "", "sample_employees.csv"),
                    "sample_employees.csv",
                    Path.Combine(Directory.GetCurrentDirectory(), "sample_employees.csv")
                };

                string? csvPath = null;
                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        csvPath = path;
                        break;
                    }
                }

                if (csvPath == null)
                {
                    _logger.LogWarning("Sample employees CSV not found. Tried paths: {Paths}", 
                        string.Join(", ", possiblePaths));
                    return new List<object>();
                }

                _logger.LogInformation("Loading sample employees from {CsvPath}", csvPath);

                var employees = new List<object>();
                var lines = File.ReadAllLines(csvPath, System.Text.Encoding.UTF8);
                
                if (lines.Length < 2) // Need at least header + one data row
                    return employees;

                var headers = lines[0].Split(',');
                
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',');
                    var employee = new Dictionary<string, object>();

                    for (int j = 0; j < headers.Length && j < values.Length; j++)
                    {
                        var header = headers[j].Trim();
                        var value = values[j].Trim();
                        
                        switch (header.ToLower())
                        {
                            case "employee_id":
                                employee["employee_id"] = value;
                                break;
                            case "first_name":
                                employee["first_name"] = value;
                                break;
                            case "last_name":
                                employee["last_name"] = value;
                                break;
                            case "role":
                                employee["role"] = string.IsNullOrEmpty(value) ? "کارگر" : value;
                                break;
                            case "photo_path":
                                employee["photo_path"] = value;
                                break;
                        }
                    }

                    // Set timestamps
                    employee["created_at"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                    employee["updated_at"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

                    employees.Add(employee);
                }

                _logger.LogInformation("Successfully loaded {Count} sample employees", employees.Count);
                return employees;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sample employees");
                return new List<object>();
            }
        }

        public Dictionary<string, object> EnsureTodayReportExists()
        {
            var filepath = GetTodayFilepath();
            var todayFilename = GetTodayFilename();
            _logger.LogInformation("Ensuring today's report exists: {FilePath}", filepath);
            _logger.LogInformation("Today's filename: {TodayFilename}", todayFilename);

            if (File.Exists(filepath))
            {
                var data = ReadJson(filepath);
                if (data != null)
                {
                    var employeesData = data.GetValueOrDefault("employees");
                    var managersData = data.GetValueOrDefault("managers");
                    
                    _logger.LogInformation("Raw employees data type: {Type}, value: {Value}", 
                        employeesData?.GetType().Name ?? "null", 
                        employeesData?.ToString()?.Substring(0, Math.Min(100, employeesData.ToString()?.Length ?? 0)) ?? "null");
                    
                    var empCount = 0;
                    var mgrCount = 0;
                    
                    if (employeesData is List<object> empList)
                    {
                        empCount = empList.Count;
                    }
                    else if (employeesData is Newtonsoft.Json.Linq.JArray empJArray)
                    {
                        empCount = empJArray.Count;
                    }
                    
                    if (managersData is List<object> mgrList)
                    {
                        mgrCount = mgrList.Count;
                    }
                    else if (managersData is Newtonsoft.Json.Linq.JArray mgrJArray)
                    {
                        mgrCount = mgrJArray.Count;
                    }
                    
                    _logger.LogInformation("Existing file has {EmpCount} employees and {MgrCount} managers", 
                        empCount, mgrCount);

                    // If no employees, try to carry forward from previous day
                    if (empCount == 0 && mgrCount == 0)
                    {
                        _logger.LogWarning("No employees found in existing file, attempting to carry forward from previous day");
                        var previousData = GetPreviousDayData();
                        if (previousData != null)
                        {
                            data["employees"] = previousData.GetValueOrDefault("employees", new List<object>());
                            data["managers"] = previousData.GetValueOrDefault("managers", new List<object>());
                            
                            var settings = data.GetValueOrDefault("settings", new Dictionary<string, object>()) as Dictionary<string, object>;
                            var prevSettings = previousData.GetValueOrDefault("settings", new Dictionary<string, object>()) as Dictionary<string, object>;
                            if (settings != null && prevSettings != null)
                            {
                                settings["managers"] = prevSettings.GetValueOrDefault("managers", new List<object>());
                            }
                            
                            WriteJson(filepath, data);
                            _logger.LogInformation("Carried forward employees from previous day");
                            return data;
                        }
                        else
                        {
                            _logger.LogWarning("No previous day data found, but keeping existing file to preserve any shift assignments");
                            // Don't delete the file - it might contain shift assignments even without employees
                            // Just return the existing data
                            return data;
                        }
                    }
                    else
                    {
                        return data;
                    }
                }
            }

            // Create default report with sample data or previous day data
            _logger.LogInformation("Creating new report");
            var prevData = GetPreviousDayData();
            Dictionary<string, object> defaultData;

            if (prevData != null && 
                ((prevData.GetValueOrDefault("employees") as List<object>)?.Count > 0 ||
                 (prevData.GetValueOrDefault("managers") as List<object>)?.Count > 0))
            {
                _logger.LogInformation("Using previous day data as base");
                defaultData = CreateReportFromPrevious(prevData);
            }
            else
            {
                _logger.LogInformation("Using sample data as base");
                defaultData = GetDefaultReportStructure();
            }

            var empCountFinal = (defaultData.GetValueOrDefault("employees") as List<object>)?.Count ?? 0;
            var mgrCountFinal = (defaultData.GetValueOrDefault("managers") as List<object>)?.Count ?? 0;
            _logger.LogInformation("Report created with {EmpCount} employees and {MgrCount} managers", 
                empCountFinal, mgrCountFinal);

            var success = WriteJson(filepath, defaultData);
            _logger.LogInformation("Report file write success: {Success}", success);

            return defaultData;
        }

        private Dictionary<string, object>? GetPreviousDayData()
        {
            try
            {
                var reports = GetAllReports();
                var todayFilename = GetTodayFilename();

                // Find the most recent report that's not today
                foreach (var reportFile in reports)
                {
                    if (reportFile != todayFilename)
                    {
                        var filepath = Path.Combine(_reportsDir, reportFile);
                        var data = ReadJson(filepath);
                        if (data != null)
                        {
                            var empData = data.GetValueOrDefault("employees");
                            var mgrData = data.GetValueOrDefault("managers");
                            
                            var empCount = 0;
                            var mgrCount = 0;
                            
                            if (empData is List<object> empList)
                                empCount = empList.Count;
                            else if (empData is Newtonsoft.Json.Linq.JArray empJArray)
                                empCount = empJArray.Count;
                                
                            if (mgrData is List<object> mgrList)
                                mgrCount = mgrList.Count;
                            else if (mgrData is Newtonsoft.Json.Linq.JArray mgrJArray)
                                mgrCount = mgrJArray.Count;
                            
                            if (empCount > 0 || mgrCount > 0)
                            {
                                _logger.LogInformation("Found previous day data in {ReportFile}", reportFile);
                                return data;
                            }
                        }
                    }
                }

                _logger.LogInformation("No previous day data found");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting previous day data");
                return null;
            }
        }

        private Dictionary<string, object> CreateReportFromPrevious(Dictionary<string, object> previousData)
        {
            try
            {
                // Start with default structure
                var newData = GetDefaultReportStructure();

                // Carry forward employees and managers
                newData["employees"] = previousData.GetValueOrDefault("employees", new List<object>());
                newData["managers"] = previousData.GetValueOrDefault("managers", new List<object>());

                // Carry forward settings but update date
                var settings = newData.GetValueOrDefault("settings", new Dictionary<string, object>()) as Dictionary<string, object>;
                var prevSettings = previousData.GetValueOrDefault("settings", new Dictionary<string, object>()) as Dictionary<string, object>;
                
                if (settings != null && prevSettings != null)
                {
                    foreach (var kvp in prevSettings)
                    {
                        settings[kvp.Key] = kvp.Value;
                    }
                    settings["shared_folder_path"] = _dataDir; // Update path
                }

                // Reset shifts (clear assignments but keep structure)
                var shifts = newData.GetValueOrDefault("shifts", new Dictionary<string, object>()) as Dictionary<string, object>;
                var prevShifts = previousData.GetValueOrDefault("shifts", new Dictionary<string, object>()) as Dictionary<string, object>;
                
                if (shifts != null && prevShifts != null)
                {
                    foreach (var shiftType in new[] { "morning", "evening" })
                    {
                        if (prevShifts.ContainsKey(shiftType))
                        {
                            var shift = shifts.GetValueOrDefault(shiftType, new Dictionary<string, object>()) as Dictionary<string, object>;
                            var prevShift = prevShifts.GetValueOrDefault(shiftType, new Dictionary<string, object>()) as Dictionary<string, object>;
                            
                            if (shift != null && prevShift != null)
                            {
                                shift["capacity"] = prevShift.GetValueOrDefault("capacity", 15);
                                shift["assigned_employees"] = new List<object>(); // Clear assignments
                            }
                        }
                    }
                }

                // Clear absences for new day
                newData["absences"] = new Dictionary<string, object>
                {
                    { "مرخصی", new List<object>() },
                    { "بیمار", new List<object>() },
                    { "غایب", new List<object>() }
                };

                // Reset tasks for new day
                newData["tasks"] = new Dictionary<string, object>
                {
                    { "tasks", new Dictionary<string, object>() },
                    { "next_task_id", 1 }
                };

                return newData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating report from previous data");
                return GetDefaultReportStructure();
            }
        }

        public List<string> GetAllReports()
        {
            try
            {
                var files = Directory.GetFiles(_reportsDir, "report_*.json")
                    .Where(f => !Path.GetFileName(f).Contains("_backup_"))
                    .Select(Path.GetFileName)
                    .Cast<string>()
                    .OrderByDescending(f => f)
                    .ToList();
                
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing reports");
                return new List<string>();
            }
        }

        public string GetEmployeePhotoPath(string employeeId)
        {
            return Path.Combine(_imagesDir, $"{employeeId}.jpg");
        }

        public bool SaveEmployeePhoto(string employeeId, byte[] photoData)
        {
            try
            {
                var photoPath = GetEmployeePhotoPath(employeeId);
                File.WriteAllBytes(photoPath, photoData);
                _logger.LogInformation("Photo saved for employee {EmployeeId}", employeeId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save photo for employee {EmployeeId}", employeeId);
                return false;
            }
        }
    }
}
