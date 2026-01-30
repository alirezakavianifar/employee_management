
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Utils;
using Newtonsoft.Json.Linq;

namespace DisplayApp.Services
{
    public class DataService
    
    {
        private readonly ILogger<DataService> _logger;
        private JsonHandler _jsonHandler;
        private string _dataPath;

        public DataService(string dataPath = "../ManagementApp/Data")
        {
            // Use configuration system to get data path
            var config = Shared.Utils.AppConfigHelper.Config;
            _dataPath = config.DataDirectory;
            
            _logger = LoggingService.CreateLogger<DataService>();
            _jsonHandler = new JsonHandler(_dataPath);
            
            _logger.LogInformation("DataService initialized with data path: {DataPath}", _dataPath);
            _logger.LogInformation("Current working directory: {WorkingDir}", Directory.GetCurrentDirectory());
            _logger.LogInformation("Base directory: {BaseDir}", AppDomain.CurrentDomain.BaseDirectory);
            
            // Subscribe to configuration changes
            Shared.Utils.AppConfigHelper.ConfigurationChanged += OnConfigurationChanged;
        }

        public async Task<Dictionary<string, object>?> GetLatestReportAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    var reportsDir = Path.Combine(_dataPath, "Reports");
                    _logger.LogInformation("Looking for reports in: {ReportsDir}", reportsDir);
                    
                    if (!Directory.Exists(reportsDir))
                    {
                        _logger.LogWarning("Reports directory does not exist: {ReportsDir}", reportsDir);
                        return null;
                    }

                    // Get all report files and sort by last write time instead of filename
                    var reportFiles = Directory.GetFiles(reportsDir, "report_*.json")
                        .Where(f => !Path.GetFileName(f).Contains("_backup_"))
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .ToList();

                    if (!reportFiles.Any())
                    {
                        _logger.LogWarning("No report files found in {ReportsDir}", reportsDir);
                        return null;
                    }

                    _logger.LogInformation("Found {Count} report files, trying to read latest", reportFiles.Count);

                    // Try to read the latest report
                    foreach (var reportFile in reportFiles)
                    {
                        try
                        {
                            _logger.LogInformation("Attempting to read report: {ReportFile}", Path.GetFileName(reportFile));
                            var data = _jsonHandler.ReadJson(reportFile);
                            if (data != null)
                            {
                                _logger.LogInformation("Successfully loaded report: {ReportFile}", Path.GetFileName(reportFile));
                                
                                // Transform the data to parse JSON strings into objects
                                var transformedData = TransformReportData(data);
                                return transformedData;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error reading report file: {ReportFile}", reportFile);
                        }
                    }

                    _logger.LogError("Failed to read any report files");
                    return null;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest report");
                return null;
            }
        }

        private Dictionary<string, object> TransformReportData(Dictionary<string, object> originalData)
        {
            try
            {
                var transformedData = new Dictionary<string, object>(originalData);

                // Transform employees from JSON strings to objects or use existing structure
                if (originalData.TryGetValue("employees", out var employeesObj))
                {
                    _logger.LogInformation("Found employees data: {Type}", employeesObj?.GetType().Name ?? "null");
                    var transformedEmployees = new List<object>();
                    
                    if (employeesObj is List<object> employeesList)
                    {
                        _logger.LogInformation("Found {Count} employees in list format", employeesList.Count);
                        
                        foreach (var employeeItem in employeesList)
                        {
                            if (employeeItem is string employeeJson)
                            {
                                // Old format - employees are JSON strings
                            try
                            {
                                // Clean up the JSON string by removing \r\n characters
                                var cleanJson = employeeJson.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
                                var employeeObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(cleanJson);
                                if (employeeObj != null)
                                {
                                    // Map field names to match DisplayApp expectations
                                    var mappedEmployee = new Dictionary<string, object>
                                    {
                                        { "employee_id", employeeObj.GetValueOrDefault("EmployeeId", "") },
                                        { "first_name", employeeObj.GetValueOrDefault("FirstName", "") },
                                        { "last_name", employeeObj.GetValueOrDefault("LastName", "") },
                                        { "role", employeeObj.GetValueOrDefault("Role", "") },
                                        { "photo_path", employeeObj.GetValueOrDefault("PhotoPath", "") },
                                        { "is_manager", employeeObj.GetValueOrDefault("IsManager", false) },
                                        { "created_at", employeeObj.GetValueOrDefault("CreatedAt", "") },
                                        { "updated_at", employeeObj.GetValueOrDefault("UpdatedAt", "") }
                                    };
                                    transformedEmployees.Add(mappedEmployee);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to parse employee JSON: {EmployeeJson}", employeeJson);
                            }
                        }
                            else if (employeeItem is Dictionary<string, object> employeeDict)
                            {
                                // New format - employees are already dictionaries
                                _logger.LogInformation("Employee already in dictionary format: {EmployeeId}", 
                                    employeeDict.GetValueOrDefault("employee_id", "Unknown"));
                                transformedEmployees.Add(employeeDict);
                            }
                            else if (employeeItem is Newtonsoft.Json.Linq.JObject employeeJObject)
                            {
                                // JObject format - convert to dictionary
                                var convertedEmployeeDict = ConvertJObjectToDictionary(employeeJObject);
                                _logger.LogInformation("Converted JObject employee: {EmployeeId}", 
                                    convertedEmployeeDict.GetValueOrDefault("employee_id", "Unknown"));
                                transformedEmployees.Add(convertedEmployeeDict);
                            }
                            else
                            {
                                _logger.LogWarning("Unknown employee format: {Type}", employeeItem?.GetType().Name ?? "null");
                            }
                        }
                    }
                    else if (employeesObj is Newtonsoft.Json.Linq.JArray employeesJArray)
                    {
                        _logger.LogInformation("Found {Count} employees in JArray format", employeesJArray.Count);
                        
                        foreach (var employeeItem in employeesJArray)
                        {
                            if (employeeItem is Newtonsoft.Json.Linq.JObject employeeJObject)
                            {
                                // JObject format - convert to dictionary
                                var convertedEmployeeDict = ConvertJObjectToDictionary(employeeJObject);
                                _logger.LogInformation("Converted JObject employee: {EmployeeId}", 
                                    convertedEmployeeDict.GetValueOrDefault("employee_id", "Unknown"));
                                transformedEmployees.Add(convertedEmployeeDict);
                            }
                            else
                            {
                                _logger.LogWarning("Unknown employee format in JArray: {Type}", employeeItem?.GetType().Name ?? "null");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Employees data is not a List<object> or JArray: {Type}", employeesObj?.GetType().Name ?? "null");
                    }
                    
                    transformedData["employees"] = transformedEmployees;
                    _logger.LogInformation("Transformed {Count} employees", transformedEmployees.Count);
                }
                else
                {
                    _logger.LogWarning("No employees data found in original data");
                }

                // Transform shifts from JSON strings to objects or use existing structure
                if (originalData.TryGetValue("shifts", out var shiftsObj))
                {
                    Dictionary<string, object> shiftsDict = null;
                    
                    if (shiftsObj is Dictionary<string, object> dict)
                    {
                        shiftsDict = dict;
                    }
                    else if (shiftsObj is Newtonsoft.Json.Linq.JObject jObject)
                    {
                        // Convert JObject to Dictionary
                        shiftsDict = ConvertJObjectToDictionary(jObject);
                    }
                    
                    if (shiftsDict != null)
                {
                    _logger.LogInformation("Found shifts data as Dictionary with {Count} keys: {Keys}", 
                        shiftsDict.Count, string.Join(", ", shiftsDict.Keys));
                    
                    var transformedShifts = new Dictionary<string, object>();

                    // Check if shifts are already in the correct format (new format)
                    if (shiftsDict.ContainsKey("morning") && shiftsDict.ContainsKey("evening"))
                    {
                        _logger.LogInformation("Processing shifts in new format (morning/evening keys found)");
                        
                        // Check if we have selected_group data (newest format)
                        if (shiftsDict.ContainsKey("selected_group") && shiftsDict["selected_group"] is Dictionary<string, object> selectedGroup)
                        {
                            _logger.LogInformation("Found selected_group data, using it for shift display");
                            
                            // Preserve the selected_group data for UI display
                            transformedShifts["selected_group"] = selectedGroup;
                            
                            // Use selected_group data for shifts
                            if (selectedGroup.TryGetValue("morning_shift", out var morningShiftObj) && morningShiftObj is Dictionary<string, object> morningShift)
                            {
                                _logger.LogInformation("Processing morning shift from selected_group");
                                transformedShifts["morning"] = morningShift;
                            }
                            
                            if (selectedGroup.TryGetValue("evening_shift", out var eveningShiftObj) && eveningShiftObj is Dictionary<string, object> eveningShift)
                            {
                                _logger.LogInformation("Processing evening shift from selected_group");
                                transformedShifts["evening"] = eveningShift;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No selected_group found, using direct morning/evening data");
                            // Fallback to direct morning/evening data
                            foreach (var shiftType in new[] { "morning", "evening" })
                            {
                                if (shiftsDict.TryGetValue(shiftType, out var shiftObj) && shiftObj is Dictionary<string, object> shift)
                                {
                                    _logger.LogInformation("Processing {ShiftType} shift with {Count} keys", shiftType, shift.Count);
                                    
                                    // Check if assigned_employees already contains employee objects
                                    if (shift.TryGetValue("assigned_employees", out var assignedEmployeesObj))
                                    {
                                        _logger.LogInformation("Found assigned_employees in {ShiftType} shift: {Type}", shiftType, assignedEmployeesObj?.GetType().Name ?? "null");
                                        
                                        if (assignedEmployeesObj is List<object> assignedEmployees)
                                        {
                                            _logger.LogInformation("Found {Count} assigned employees in {ShiftType} shift", assignedEmployees.Count, shiftType);
                                            
                                            // Check if these are already employee objects or just IDs
                                            if (assignedEmployees.Count > 0)
                                            {
                                                var firstEmployee = assignedEmployees[0];
                                                if (firstEmployee is Dictionary<string, object> employeeObj && employeeObj.ContainsKey("employee_id"))
                                                {
                                                    _logger.LogInformation("Assigned employees are already employee objects - no transformation needed");
                                                    // Already employee objects, no transformation needed
                                                }
                                                else
                                                {
                                                    _logger.LogInformation("Assigned employees are IDs - transforming to employee objects");
                                                    // These are IDs, need to convert to employee objects
                                                    var transformedEmployees = new List<object>();
                                                    
                                            if (transformedData.TryGetValue("employees", out var employeesListObj) && employeesListObj is List<object> employees)
                                            {
                                                        foreach (var employeeId in assignedEmployees)
                                                {
                                                    var employee = employees.FirstOrDefault(e => 
                                                        e is Dictionary<string, object> emp && 
                                                        emp.GetValueOrDefault("employee_id", "").ToString() == employeeId.ToString());
                                                    if (employee != null)
                                                    {
                                                                transformedEmployees.Add(employee);
                                                            }
                                                        }
                                                    }
                                                    
                                                    shift["assigned_employees"] = transformedEmployees;
                                                    _logger.LogInformation("Transformed {Count} employee IDs to objects for {ShiftType} shift", transformedEmployees.Count, shiftType);
                                                }
                                            }
                                        }
                                    }
                                    
                                    transformedShifts[shiftType] = shift;
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Processing shifts in old format (MorningShift/EveningShift keys expected)");
                        // Old format - transform from JSON strings
                    // Transform MorningShift
                    if (shiftsDict.TryGetValue("MorningShift", out var morningShiftStr) && morningShiftStr is string morningJson)
                    {
                        try
                        {
                            var cleanMorningJson = morningJson.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
                            var morningShiftObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(cleanMorningJson);
                            if (morningShiftObj != null)
                            {
                                var assignedEmployees = GetAssignedEmployees(morningShiftObj, transformedData);
                                var morningShift = new Dictionary<string, object>
                                {
                                    { "shift_type", "morning" },
                                    { "capacity", morningShiftObj.GetValueOrDefault("Capacity", 15) },
                                    { "assigned_employees", assignedEmployees }
                                };
                                transformedShifts["morning"] = morningShift;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse morning shift JSON: {MorningJson}", morningJson);
                        }
                    }

                    // Transform EveningShift
                    if (shiftsDict.TryGetValue("EveningShift", out var eveningShiftStr) && eveningShiftStr is string eveningJson)
                    {
                        try
                        {
                            var cleanEveningJson = eveningJson.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
                            var eveningShiftObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(cleanEveningJson);
                            if (eveningShiftObj != null)
                            {
                                var assignedEmployees = GetAssignedEmployees(eveningShiftObj, transformedData);
                                var eveningShift = new Dictionary<string, object>
                                {
                                    { "shift_type", "evening" },
                                    { "capacity", eveningShiftObj.GetValueOrDefault("Capacity", 15) },
                                    { "assigned_employees", assignedEmployees }
                                };
                                transformedShifts["evening"] = eveningShift;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse evening shift JSON: {EveningJson}", eveningJson);
                            }
                        }
                    }

                    transformedData["shifts"] = transformedShifts;
                    }
                }

                // Transform absences from JSON strings to objects
                if (originalData.TryGetValue("absences", out var absencesObj))
                {
                    _logger.LogInformation("Found absences data: {Type}", absencesObj?.GetType().Name ?? "null");
                    
                    Dictionary<string, object> absencesDict = null;
                    
                    if (absencesObj is Dictionary<string, object> dict)
                    {
                        absencesDict = dict;
                    }
                    else if (absencesObj is Newtonsoft.Json.Linq.JObject jObject)
                    {
                        // Convert JObject to Dictionary
                        absencesDict = ConvertJObjectToDictionary(jObject);
                    }
                    
                    if (absencesDict != null)
                    {
                        var transformedAbsences = new Dictionary<string, object>();
                        
                        foreach (var category in new[] { "مرخصی", "بیمار", "غایب" })
                        {
                            if (absencesDict.TryGetValue(category, out var categoryObj) && categoryObj is List<object> categoryList)
                            {
                                _logger.LogInformation("Found {Count} {Category} absences", categoryList.Count, category);
                                
                                var transformedCategoryList = new List<object>();
                                
                                foreach (var absenceItem in categoryList)
                                {
                                    if (absenceItem is string absenceJson)
                                    {
                                        try
                                        {
                                            // Clean up the JSON string
                                            var cleanJson = absenceJson.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
                                            var absenceObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(cleanJson);
                                            if (absenceObj != null)
                                            {
                                                // Map field names to match DisplayApp expectations
                                                var mappedAbsence = new Dictionary<string, object>
                                                {
                                                    { "employee_id", absenceObj.GetValueOrDefault("EmployeeId", "") },
                                                    { "first_name", absenceObj.GetValueOrDefault("FirstName", "") },
                                                    { "last_name", absenceObj.GetValueOrDefault("LastName", "") },
                                                    { "employee_name", absenceObj.GetValueOrDefault("EmployeeName", "") },
                                                    { "category", absenceObj.GetValueOrDefault("Category", "") },
                                                    { "date", absenceObj.GetValueOrDefault("Date", "") },
                                                    { "notes", absenceObj.GetValueOrDefault("Notes", "") },
                                                    { "photo_path", absenceObj.GetValueOrDefault("PhotoPath", "") },
                                                    { "created_at", absenceObj.GetValueOrDefault("CreatedAt", "") },
                                                    { "updated_at", absenceObj.GetValueOrDefault("UpdatedAt", "") }
                                                };
                                                transformedCategoryList.Add(mappedAbsence);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Failed to parse absence JSON: {AbsenceJson}", absenceJson);
                                        }
                                    }
                                    else if (absenceItem is Dictionary<string, object> absenceDict)
                                    {
                                        // Already in dictionary format
                                        transformedCategoryList.Add(absenceDict);
                                    }
                                    else if (absenceItem is Newtonsoft.Json.Linq.JObject absenceJObject)
                                    {
                                        // JObject format - convert to dictionary
                                        var convertedAbsenceDict = ConvertJObjectToDictionary(absenceJObject);
                                        transformedCategoryList.Add(convertedAbsenceDict);
                                    }
                                }
                                
                                transformedAbsences[category] = transformedCategoryList;
                                _logger.LogInformation("Transformed {Count} {Category} absences", transformedCategoryList.Count, category);
                            }
                            else
                            {
                                // No absences for this category
                                transformedAbsences[category] = new List<object>();
                            }
                        }
                        
                        transformedData["absences"] = transformedAbsences;
                        _logger.LogInformation("Successfully transformed absences data");
                    }
                    else
                    {
                        _logger.LogWarning("Absences data is not a Dictionary: {Type}", absencesObj?.GetType().Name ?? "null");
                    }
                }
                else
                {
                    _logger.LogWarning("No absences data found in original data");
                }

                // Add fallback managers from employees if managers array is empty
                _logger.LogInformation("Checking managers array for fallback logic...");
                if (transformedData.TryGetValue("managers", out var managersObj))
                {
                    _logger.LogInformation("Managers object found: {Type}", managersObj?.GetType().Name ?? "null");
                    
                    var managersCount = 0;
                    var isEmpty = false;
                    
                    if (managersObj is List<object> managers)
                    {
                        managersCount = managers.Count;
                        isEmpty = managersCount == 0;
                        _logger.LogInformation("Managers array (List<object>) has {Count} items", managersCount);
                    }
                    else if (managersObj is Newtonsoft.Json.Linq.JArray managersJArray)
                    {
                        managersCount = managersJArray.Count;
                        isEmpty = managersCount == 0;
                        _logger.LogInformation("Managers array (JArray) has {Count} items", managersCount);
                        
                        // Convert JArray to List<object> with proper Dictionary objects
                        var convertedManagers = new List<object>();
                        foreach (var managerItem in managersJArray)
                        {
                            if (managerItem is Newtonsoft.Json.Linq.JObject managerJObject)
                            {
                                var convertedManager = ConvertJObjectToDictionary(managerJObject);
                                convertedManagers.Add(convertedManager);
                                _logger.LogInformation("Converted JObject manager: {EmployeeId}", 
                                    convertedManager.GetValueOrDefault("employee_id", "Unknown"));
                            }
                            else
                            {
                                _logger.LogWarning("Manager item is not a JObject: {Type}", managerItem?.GetType().Name ?? "null");
                            }
                        }
                        
                        if (convertedManagers.Count > 0)
                        {
                            transformedData["managers"] = convertedManagers;
                            _logger.LogInformation("Converted {Count} managers from JArray to List<object>", convertedManagers.Count);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Managers object is not a List<object> or JArray: {Type}", managersObj?.GetType().Name ?? "null");
                    }
                    
                    if (isEmpty)
                    {
                        _logger.LogInformation("Managers array is empty, checking employees for managers");
                        var fallbackManagers = ExtractManagersFromEmployees(transformedData);
                        if (fallbackManagers.Count > 0)
                        {
                            transformedData["managers"] = fallbackManagers;
                            _logger.LogInformation("Found {Count} managers from employees fallback", fallbackManagers.Count);
                        }
                        else
                        {
                            _logger.LogInformation("No managers found in employees fallback");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Managers array has {Count} items, skipping fallback logic", managersCount);
                    }
                }
                else
                {
                    _logger.LogWarning("No managers key found in transformed data");
                }

                _logger.LogInformation("Successfully transformed report data");
                return transformedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transforming report data");
                return originalData;
            }
        }

        private Dictionary<string, object> ConvertJObjectToDictionary(JObject jObject)
        {
            var dictionary = new Dictionary<string, object>();
            
            foreach (var property in jObject.Properties())
            {
                if (property.Value is JObject nestedJObject)
                {
                    dictionary[property.Name] = ConvertJObjectToDictionary(nestedJObject);
                }
                else if (property.Value is JArray jArray)
                {
                    var list = new List<object>();
                    foreach (var item in jArray)
                    {
                        if (item is JObject itemJObject)
                        {
                            list.Add(ConvertJObjectToDictionary(itemJObject));
                        }
                        else
                        {
                            list.Add(item.ToObject<object>());
                        }
                    }
                    dictionary[property.Name] = list;
                }
                else
                {
                    dictionary[property.Name] = property.Value?.ToObject<object>();
                }
            }
            
            return dictionary;
        }

        private List<object> ExtractManagersFromEmployees(Dictionary<string, object> reportData)
        {
            try
            {
                var managers = new List<object>();
                
                _logger.LogInformation("ExtractManagersFromEmployees called");
                if (reportData.TryGetValue("employees", out var employeesObj))
                {
                    _logger.LogInformation("Employees object found: {Type}", employeesObj?.GetType().Name ?? "null");
                    if (employeesObj is List<object> employees)
                    {
                        _logger.LogInformation("Checking {Count} employees for manager status", employees.Count);
                    
                    foreach (var employeeItem in employees)
                    {
                        if (employeeItem is Dictionary<string, object> employeeDict)
                        {
                            var employeeId = employeeDict.GetValueOrDefault("employee_id", "")?.ToString() ?? "";
                            var firstName = employeeDict.GetValueOrDefault("first_name", "")?.ToString() ?? "";
                            var lastName = employeeDict.GetValueOrDefault("last_name", "")?.ToString() ?? "";
                            var role = employeeDict.GetValueOrDefault("role", "")?.ToString()?.ToLower() ?? "";
                            
                            _logger.LogInformation("Checking employee: {EmployeeId} - {FirstName} {LastName} (Role: {Role})", 
                                employeeId, firstName, lastName, role);
                            
                            var isManager = false;
                            
                            // Check is_manager field
                            if (employeeDict.TryGetValue("is_manager", out var isManagerObj))
                            {
                                _logger.LogInformation("Employee {EmployeeId} has is_manager field: {IsManager} (Type: {Type})", 
                                    employeeId, isManagerObj, isManagerObj?.GetType().Name ?? "null");
                                
                                if (isManagerObj is bool isManagerBool)
                                {
                                    isManager = isManagerBool;
                                    _logger.LogInformation("Employee {EmployeeId} is_manager (bool): {IsManager}", employeeId, isManager);
                                }
                                else if (isManagerObj != null && bool.TryParse(isManagerObj.ToString(), out var isManagerParsed))
                                {
                                    isManager = isManagerParsed;
                                    _logger.LogInformation("Employee {EmployeeId} is_manager (parsed): {IsManager}", employeeId, isManager);
                                }
                            }
                            else
                            {
                                _logger.LogInformation("Employee {EmployeeId} has no is_manager field", employeeId);
                            }
                            
                            // Check role-based manager detection
                            if (!isManager && (role.StartsWith("مدیر") || role.StartsWith("manager")))
                            {
                                isManager = true;
                                _logger.LogInformation("Employee {EmployeeId} identified as manager by role: {Role}", employeeId, role);
                            }
                            
                            if (isManager)
                            {
                                _logger.LogInformation("Found manager: {EmployeeId} - {FirstName} {LastName} (Role: {Role}, IsManager: {IsManager})", 
                                    employeeId, firstName, lastName, role, employeeDict.GetValueOrDefault("is_manager", false));
                                managers.Add(employeeDict);
                            }
                            else
                            {
                                _logger.LogInformation("Employee {EmployeeId} is not a manager", employeeId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Employee item is not a Dictionary: {Type}", employeeItem?.GetType().Name ?? "null");
                        }
                    }
                    }
                    else
                    {
                        _logger.LogWarning("Employees object is not a List<object>: {Type}", employeesObj?.GetType().Name ?? "null");
                    }
                }
                else
                {
                    _logger.LogWarning("No employees key found in report data");
                }
                
                _logger.LogInformation("Extracted {Count} managers from employees", managers.Count);
                return managers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting managers from employees");
                return new List<object>();
            }
        }

        private List<object> GetAssignedEmployees(Dictionary<string, object> shiftObj, Dictionary<string, object> reportData)
        {
            try
            {
                var assignedEmployees = new List<object>();

                if (shiftObj.TryGetValue("AssignedEmployeeIds", out var assignedIdsObj) && assignedIdsObj is List<object> assignedIds)
                {
                    _logger.LogInformation("Found {Count} assigned employee IDs", assignedIds.Count);
                    
                    if (reportData.TryGetValue("employees", out var employeesObj) && employeesObj is List<object> employees)
                    {
                        _logger.LogInformation("Found {Count} total employees", employees.Count);
                        
                        foreach (var employeeId in assignedIds)
                        {
                            if (employeeId != null && !string.IsNullOrEmpty(employeeId.ToString()))
                            {
                                _logger.LogInformation("Looking for employee ID: {EmployeeId}", employeeId);
                                
                                // Find the employee with this ID
                                foreach (var employee in employees)
                                {
                                    if (employee is Dictionary<string, object> employeeDict &&
                                        employeeDict.TryGetValue("employee_id", out var empId))
                                    {
                                        if (empId.ToString() == employeeId.ToString())
                                        {
                                            _logger.LogInformation("Found matching employee: {EmployeeId}", employeeId);
                                            assignedEmployees.Add(employeeDict);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Returning {Count} assigned employees", assignedEmployees.Count);
                return assignedEmployees;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting assigned employees");
                return new List<object>();
            }
        }

        public async Task<Dictionary<string, object>?> GetReportByDateAsync(DateTime date)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var filename = $"report_{date:yyyy-MM-dd}.json";
                    var filepath = Path.Combine(_dataPath, "Reports", filename);
                    
                    if (!File.Exists(filepath))
                    {
                        _logger.LogWarning("Report file does not exist: {Filepath}", filepath);
                        return null;
                    }

                    var data = _jsonHandler.ReadJson(filepath);
                    if (data != null)
                    {
                        _logger.LogInformation("Successfully loaded report for date {Date}: {Filename}", date, filename);
                    }
                    
                    return data;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report for date {Date}", date);
                return null;
            }
        }

        public async Task<List<string>> GetAvailableReportDatesAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    var reportsDir = Path.Combine(_dataPath, "Reports");
                    if (!Directory.Exists(reportsDir))
                    {
                        return new List<string>();
                    }

                    var reportFiles = Directory.GetFiles(reportsDir, "report_*.json")
                        .Where(f => !Path.GetFileName(f).Contains("_backup_"))
                        .Select(f => Path.GetFileNameWithoutExtension(f).Replace("report_", ""))
                        .OrderByDescending(d => d)
                        .ToList();

                    _logger.LogInformation("Found {Count} available report dates", reportFiles.Count);
                    return reportFiles;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available report dates");
                return new List<string>();
            }
        }

        public async Task<bool> IsDataAvailableAsync()
        {
            try
            {
                var latestReport = await GetLatestReportAsync();
                return latestReport != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking data availability");
                return false;
            }
        }

        public async Task<DateTime?> GetLastUpdateTimeAsync()
        {
            try
            {
                var latestReport = await GetLatestReportAsync();
                if (latestReport != null && latestReport.TryGetValue("last_modified", out var lastModifiedObj))
                {
                    if (DateTime.TryParse(lastModifiedObj.ToString(), out var lastModified))
                    {
                        return lastModified;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last update time");
                return null;
            }
        }

        public async Task<Dictionary<string, object>> GetDefaultDataAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    _logger.LogInformation("Returning default empty data structure");
                    
                    return new Dictionary<string, object>
                    {
                        { "date", DateTime.Now.ToString("yyyy-MM-dd") },
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
                                { "shift_capacity", 15 },
                                { "morning_capacity", 15 },
                                { "evening_capacity", 15 },
                                { "shared_folder_path", _dataPath }
                            }
                        },
                        { "last_modified", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
                    };
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default data");
                return new Dictionary<string, object>();
            }
        }

        private void OnConfigurationChanged(Shared.Utils.AppConfig newConfig)
        {
            try
            {
                _logger.LogInformation("Configuration changed, updating data path from {OldPath} to {NewPath}", 
                    _dataPath, newConfig.DataDirectory);
                
                _dataPath = newConfig.DataDirectory;
                _jsonHandler = new JsonHandler(_dataPath);
                
                _logger.LogInformation("DataService updated with new data path: {DataPath}", _dataPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DataService configuration");
            }
        }
    }
}
