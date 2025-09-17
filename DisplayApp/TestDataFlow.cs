using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shared.Services;
using Shared.Utils;

namespace DisplayApp
{
    public class TestDataFlow
    {
        private readonly ILogger<TestDataFlow> _logger;
        private readonly JsonHandler _jsonHandler;

        public TestDataFlow()
        {
            _logger = LoggingService.CreateLogger<TestDataFlow>();
            _jsonHandler = new JsonHandler("../ManagementApp/Data");
        }

        public async Task TestDataTransformation()
        {
            try
            {
                _logger.LogInformation("=== STARTING DATA FLOW TEST ===");

                // Step 1: Read the latest report file
                var reportsDir = Path.Combine("../ManagementApp/Data", "Reports");
                _logger.LogInformation("Looking for reports in: {ReportsDir}", reportsDir);

                if (!Directory.Exists(reportsDir))
                {
                    _logger.LogError("Reports directory does not exist: {ReportsDir}", reportsDir);
                    return;
                }

                var reportFiles = Directory.GetFiles(reportsDir, "report_*.json")
                    .Where(f => !Path.GetFileName(f).Contains("_backup_"))
                    .OrderByDescending(f => f)
                    .ToList();

                if (!reportFiles.Any())
                {
                    _logger.LogError("No report files found");
                    return;
                }

                var latestReportFile = reportFiles[0];
                _logger.LogInformation("Testing with latest report: {ReportFile}", Path.GetFileName(latestReportFile));

                // Step 2: Read and parse the raw data
                var rawData = _jsonHandler.ReadJson(latestReportFile);
                if (rawData == null)
                {
                    _logger.LogError("Failed to read report data");
                    return;
                }

                _logger.LogInformation("Raw data keys: {Keys}", string.Join(", ", rawData.Keys));

                // Step 3: Test data transformation
                var transformedData = TransformReportData(rawData);
                if (transformedData == null)
                {
                    _logger.LogError("Data transformation failed");
                    return;
                }

                _logger.LogInformation("Transformed data keys: {Keys}", string.Join(", ", transformedData.Keys));

                // Step 4: Test shift data specifically
                if (transformedData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
                {
                    _logger.LogInformation("Found shifts data with {Count} shift types", shifts.Count);

                    foreach (var shift in shifts)
                    {
                        _logger.LogInformation("Shift: {ShiftType}", shift.Key);
                        if (shift.Value is Dictionary<string, object> shiftData)
                        {
                            if (shiftData.TryGetValue("assigned_employees", out var employeesObj) && employeesObj is List<object> employees)
                            {
                                _logger.LogInformation("  - Assigned employees count: {Count}", employees.Count);
                                foreach (var emp in employees)
                                {
                                    if (emp is Dictionary<string, object> empData)
                                    {
                                        _logger.LogInformation("    - Employee: {EmployeeId} ({FirstName} {LastName})", 
                                            empData.GetValueOrDefault("EmployeeId", "Unknown"),
                                            empData.GetValueOrDefault("FirstName", "Unknown"),
                                            empData.GetValueOrDefault("LastName", "Unknown"));
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogWarning("  - No assigned employees found");
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogError("No shifts data found in transformed data");
                }

                _logger.LogInformation("=== DATA FLOW TEST COMPLETED ===");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data flow test");
            }
        }

        private Dictionary<string, object> TransformReportData(Dictionary<string, object> rawData)
        {
            try
            {
                var transformedData = new Dictionary<string, object>();

                // Copy basic fields
                foreach (var kvp in rawData)
                {
                    if (kvp.Key != "employees" && kvp.Key != "shifts")
                    {
                        transformedData[kvp.Key] = kvp.Value;
                    }
                }

                // Transform employees
                if (rawData.TryGetValue("employees", out var employeesObj) && employeesObj is List<object> employeesList)
                {
                    var transformedEmployees = new List<Dictionary<string, object>>();
                    
                    foreach (var employeeJson in employeesList)
                    {
                        if (employeeJson is string employeeJsonString)
                        {
                            // Clean the JSON string
                            var cleanJson = employeeJsonString.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
                            
                            try
                            {
                                var employeeData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(cleanJson);
                                if (employeeData != null)
                                {
                                    transformedEmployees.Add(employeeData);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to parse employee JSON: {Error}", ex.Message);
                            }
                        }
                    }
                    
                    transformedData["employees"] = transformedEmployees;
                    _logger.LogInformation("Transformed {Count} employees", transformedEmployees.Count);
                }

                // Transform shifts
                if (rawData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
                {
                    var transformedShifts = new Dictionary<string, object>();

                    foreach (var shift in shifts)
                    {
                        if (shift.Key == "Capacity") continue; // Skip capacity field

                        var shiftKey = shift.Key switch
                        {
                            "MorningShift" => "morning",
                            "EveningShift" => "evening",
                            _ => shift.Key.ToLower()
                        };

                        if (shift.Value is string shiftJsonString)
                        {
                            // Clean the JSON string
                            var cleanJson = shiftJsonString.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
                            
                            try
                            {
                                var shiftData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(cleanJson);
                                if (shiftData != null)
                                {
                                    // Get assigned employees
                                    var assignedEmployees = GetAssignedEmployees(shiftData, transformedData);
                                    shiftData["assigned_employees"] = assignedEmployees;
                                    
                                    transformedShifts[shiftKey] = shiftData;
                                    _logger.LogInformation("Transformed {ShiftType} shift with {Count} assigned employees", 
                                        shiftKey, assignedEmployees.Count);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Failed to parse shift JSON for {ShiftType}: {Error}", shiftKey, ex.Message);
                            }
                        }
                    }

                    transformedData["shifts"] = transformedShifts;
                }

                return transformedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transforming report data");
                return null;
            }
        }

        private List<object> GetAssignedEmployees(Dictionary<string, object> shiftData, Dictionary<string, object> transformedData)
        {
            var assignedEmployees = new List<object>();

            if (shiftData.TryGetValue("AssignedEmployeeIds", out var assignedIdsObj) && assignedIdsObj is List<object> assignedIds)
            {
                if (transformedData.TryGetValue("employees", out var employeesObj) && employeesObj is List<object> employees)
                {
                    foreach (var assignedId in assignedIds)
                    {
                        if (assignedId != null && !string.IsNullOrEmpty(assignedId.ToString()))
                        {
                            var employee = employees.FirstOrDefault(emp => 
                                emp is Dictionary<string, object> empData && 
                                empData.GetValueOrDefault("EmployeeId", "").ToString() == assignedId.ToString());

                            if (employee != null)
                            {
                                assignedEmployees.Add(employee);
                                _logger.LogInformation("Found assigned employee: {EmployeeId}", assignedId);
                            }
                            else
                            {
                                _logger.LogWarning("Assigned employee not found: {EmployeeId}", assignedId);
                            }
                        }
                    }
                }
            }

            return assignedEmployees;
        }
    }
}
