using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace DisplayApp.Utils
{
    public class ValidationHelper
    {
        private readonly ILogger<ValidationHelper> _logger;

        public ValidationHelper()
        {
            _logger = LoggingService.CreateLogger<ValidationHelper>();
        }

        public bool ValidateReportData(Dictionary<string, object> reportData)
        {
            try
            {
                if (reportData == null)
                {
                    _logger.LogWarning("Report data is null");
                    return false;
                }

                // Check required fields
                var requiredFields = new[] { "date", "employees", "managers", "shifts", "absences", "tasks", "settings" };
                foreach (var field in requiredFields)
                {
                    if (!reportData.ContainsKey(field))
                    {
                        _logger.LogWarning("Required field missing: {Field}", field);
                        return false;
                    }
                }

                // Validate shifts structure
                if (!ValidateShiftsStructure(reportData))
                {
                    return false;
                }

                // Validate absences structure
                if (!ValidateAbsencesStructure(reportData))
                {
                    return false;
                }

                // Validate tasks structure
                if (!ValidateTasksStructure(reportData))
                {
                    return false;
                }

                _logger.LogInformation("Report data validation successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating report data");
                return false;
            }
        }

        private bool ValidateShiftsStructure(Dictionary<string, object> reportData)
        {
            try
            {
                if (reportData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
                {
                    var requiredShifts = new[] { "morning", "evening" };
                    foreach (var shiftType in requiredShifts)
                    {
                        if (!shifts.ContainsKey(shiftType))
                        {
                            _logger.LogWarning("Required shift type missing: {ShiftType}", shiftType);
                            return false;
                        }

                        if (shifts[shiftType] is Dictionary<string, object> shift)
                        {
                            var requiredShiftFields = new[] { "shift_type", "capacity", "assigned_employees" };
                            foreach (var field in requiredShiftFields)
                            {
                                if (!shift.ContainsKey(field))
                                {
                                    _logger.LogWarning("Required shift field missing: {ShiftType}.{Field}", shiftType, field);
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Invalid shift structure for: {ShiftType}", shiftType);
                            return false;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid shifts structure");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating shifts structure");
                return false;
            }
        }

        private bool ValidateAbsencesStructure(Dictionary<string, object> reportData)
        {
            try
            {
                if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
                {
                    var requiredAbsenceTypes = new[] { "Leave", "Sick", "Absent" };
                    foreach (var absenceType in requiredAbsenceTypes)
                    {
                        if (!absences.ContainsKey(absenceType))
                        {
                            _logger.LogWarning("Required absence type missing: {AbsenceType}", absenceType);
                            return false;
                        }

                        if (!(absences[absenceType] is List<object>))
                        {
                            _logger.LogWarning("Invalid absence structure for: {AbsenceType}", absenceType);
                            return false;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid absences structure");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating absences structure");
                return false;
            }
        }

        private bool ValidateTasksStructure(Dictionary<string, object> reportData)
        {
            try
            {
                if (reportData.TryGetValue("tasks", out var tasksObj) && tasksObj is Dictionary<string, object> tasks)
                {
                    var requiredTaskFields = new[] { "tasks", "next_task_id" };
                    foreach (var field in requiredTaskFields)
                    {
                        if (!tasks.ContainsKey(field))
                        {
                            _logger.LogWarning("Required task field missing: {Field}", field);
                            return false;
                        }
                    }

                    if (!(tasks["tasks"] is Dictionary<string, object>))
                    {
                        _logger.LogWarning("Invalid tasks structure");
                        return false;
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid tasks structure");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating tasks structure");
                return false;
            }
        }

        public bool ValidateEmployeeData(Dictionary<string, object> employeeData)
        {
            try
            {
                if (employeeData == null)
                {
                    return false;
                }

                var requiredFields = new[] { "id", "first_name", "last_name", "position" };
                foreach (var field in requiredFields)
                {
                    if (!employeeData.ContainsKey(field) || string.IsNullOrEmpty(employeeData[field]?.ToString()))
                    {
                        _logger.LogWarning("Required employee field missing or empty: {Field}", field);
                        return false;
                    }
                }

                if (employeeData.TryGetValue("phone", out var phoneObj))
                {
                    var phone = phoneObj?.ToString() ?? string.Empty;
                    if (!IsValidPhone(phone))
                    {
                        _logger.LogWarning("Invalid phone format for employee {Employee}", employeeData.GetValueOrDefault("id", "unknown"));
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating employee data");
                return false;
            }
        }

        private bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return true; // Optional field
            }

            var trimmed = phone.Trim();
            var phonePattern = @"^[0-9+\-\s()]{6,20}$";
            return Regex.IsMatch(trimmed, phonePattern);
        }

        public bool ValidateManagerData(Dictionary<string, object> managerData)
        {
            try
            {
                if (managerData == null)
                {
                    return false;
                }

                var requiredFields = new[] { "id", "first_name", "last_name", "role" };
                foreach (var field in requiredFields)
                {
                    if (!managerData.ContainsKey(field) || string.IsNullOrEmpty(managerData[field]?.ToString()))
                    {
                        _logger.LogWarning("Required manager field missing or empty: {Field}", field);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating manager data");
                return false;
            }
        }

        public bool ValidateTaskData(Dictionary<string, object> taskData)
        {
            try
            {
                if (taskData == null)
                {
                    return false;
                }

                var requiredFields = new[] { "id", "title", "description", "status", "priority" };
                foreach (var field in requiredFields)
                {
                    if (!taskData.ContainsKey(field) || string.IsNullOrEmpty(taskData[field]?.ToString()))
                    {
                        _logger.LogWarning("Required task field missing or empty: {Field}", field);
                        return false;
                    }
                }

                // Validate status values
                var validStatuses = new[] { "Pending", "InProgress", "Completed", "Cancelled" };
                var status = taskData["status"].ToString();
                if (!validStatuses.Contains(status))
                {
                    _logger.LogWarning("Invalid task status: {Status}", status);
                    return false;
                }

                // Validate priority values
                var validPriorities = new[] { "Low", "Medium", "High", "Urgent" };
                var priority = taskData["priority"].ToString();
                if (!validPriorities.Contains(priority))
                {
                    _logger.LogWarning("Invalid task priority: {Priority}", priority);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating task data");
                return false;
            }
        }

        public bool ValidateFilePath(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return false;
                }

                // Check if file exists
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File does not exist: {FilePath}", filePath);
                    return false;
                }

                // Check file extension
                var extension = Path.GetExtension(filePath).ToLower();
                var validExtensions = new[] { ".json", ".png", ".jpg", ".jpeg" };
                if (!validExtensions.Contains(extension))
                {
                    _logger.LogWarning("Invalid file extension: {Extension}", extension);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating file path: {FilePath}", filePath);
                return false;
            }
        }

        public bool ValidateDirectoryPath(string directoryPath)
        {
            try
            {
                if (string.IsNullOrEmpty(directoryPath))
                {
                    return false;
                }

                // Check if directory exists
                if (!Directory.Exists(directoryPath))
                {
                    _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating directory path: {DirectoryPath}", directoryPath);
                return false;
            }
        }

        public string SanitizeString(string input)
        {
            try
            {
                if (string.IsNullOrEmpty(input))
                {
                    return string.Empty;
                }

                // Remove potentially dangerous characters
                var dangerousChars = new[] { '<', '>', '"', '\'', '&', '\0' };
                var sanitized = input;
                
                foreach (var c in dangerousChars)
                {
                    sanitized = sanitized.Replace(c.ToString(), string.Empty);
                }

                // Trim whitespace
                sanitized = sanitized.Trim();

                return sanitized;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sanitizing string: {Input}", input);
                return string.Empty;
            }
        }

        public int SanitizeInteger(object value, int defaultValue = 0)
        {
            try
            {
                if (value == null)
                {
                    return defaultValue;
                }

                if (int.TryParse(value.ToString(), out var result))
                {
                    return result;
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sanitizing integer: {Value}", value);
                return defaultValue;
            }
        }

        public bool SanitizeBoolean(object value, bool defaultValue = false)
        {
            try
            {
                if (value == null)
                {
                    return defaultValue;
                }

                if (bool.TryParse(value.ToString(), out var result))
                {
                    return result;
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sanitizing boolean: {Value}", value);
                return defaultValue;
            }
        }
    }
}
