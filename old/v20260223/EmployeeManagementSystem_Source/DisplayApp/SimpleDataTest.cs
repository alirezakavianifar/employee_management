using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace DisplayApp
{
    public class SimpleDataTest
    {
        public static void TestDataPath()
        {
            var logger = LoggingService.CreateLogger<SimpleDataTest>();
            
            try
            {
                logger.LogInformation("=== SIMPLE DATA PATH TEST ===");
                
                // Test 1: Check current working directory
                var currentDir = Directory.GetCurrentDirectory();
                logger.LogInformation("Current working directory: {CurrentDir}", currentDir);
                
                // Test 2: Check if ManagementApp/Data exists
                var managementDataPath = Path.Combine(currentDir, "..", "ManagementApp", "Data");
                var fullManagementDataPath = Path.GetFullPath(managementDataPath);
                logger.LogInformation("ManagementApp data path: {DataPath}", fullManagementDataPath);
                logger.LogInformation("ManagementApp data path exists: {Exists}", Directory.Exists(fullManagementDataPath));
                
                // Test 3: Check if Reports directory exists
                var reportsPath = Path.Combine(fullManagementDataPath, "Reports");
                logger.LogInformation("Reports path: {ReportsPath}", reportsPath);
                logger.LogInformation("Reports path exists: {Exists}", Directory.Exists(reportsPath));
                
                if (Directory.Exists(reportsPath))
                {
                    // Test 4: List report files
                    var reportFiles = Directory.GetFiles(reportsPath, "report_*.json")
                        .Where(f => !Path.GetFileName(f).Contains("_backup_"))
                        .OrderByDescending(f => f)
                        .ToList();
                    
                    logger.LogInformation("Found {Count} report files", reportFiles.Count);
                    
                    foreach (var file in reportFiles.Take(3)) // Show first 3 files
                    {
                        logger.LogInformation("Report file: {FileName}", Path.GetFileName(file));
                    }
                    
                    // Test 5: Try to read the latest report
                    if (reportFiles.Any())
                    {
                        var latestReport = reportFiles[0];
                        logger.LogInformation("Reading latest report: {FileName}", Path.GetFileName(latestReport));
                        
                        var jsonHandler = new JsonHandler(fullManagementDataPath);
                        var data = jsonHandler.ReadJson(latestReport);
                        
                        if (data != null)
                        {
                            logger.LogInformation("Successfully read report data. Keys: {Keys}", string.Join(", ", data.Keys));
                            
                            // Check employees
                            if (data.TryGetValue("employees", out var employeesObj))
                            {
                                logger.LogInformation("Employees data type: {Type}", employeesObj.GetType().Name);
                                if (employeesObj is System.Collections.IList employeesList)
                                {
                                    logger.LogInformation("Employees count: {Count}", employeesList.Count);
                                }
                            }
                            
                            // Check shifts
                            if (data.TryGetValue("shifts", out var shiftsObj))
                            {
                                logger.LogInformation("Shifts data type: {Type}", shiftsObj.GetType().Name);
                                if (shiftsObj is System.Collections.IDictionary shiftsDict)
                                {
                                    logger.LogInformation("Shifts keys: {Keys}", string.Join(", ", shiftsDict.Keys.Cast<string>()));
                                }
                            }
                        }
                        else
                        {
                            logger.LogError("Failed to read report data");
                        }
                    }
                }
                
                logger.LogInformation("=== SIMPLE DATA PATH TEST COMPLETED ===");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during simple data path test");
            }
        }
    }
}
