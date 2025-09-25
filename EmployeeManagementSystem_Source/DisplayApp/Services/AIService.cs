using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shared.Services;

namespace DisplayApp.Services
{
    public class AIService
    {
        private readonly ILogger<AIService> _logger;

        public AIService()
        {
            _logger = LoggingService.CreateLogger<AIService>();
            _logger.LogInformation("AIService initialized");
        }

        public string GetRecommendation(Dictionary<string, object> reportData)
        {
            try
            {
                // First, check for critical edge cases and data inconsistencies
                var criticalRecommendation = AnalyzeCriticalEdgeCases(reportData);
                if (!string.IsNullOrEmpty(criticalRecommendation))
                {
                    return criticalRecommendation;
                }

                // Analyze task workload (highest priority for normal operations)
                var taskRecommendation = AnalyzeTaskWorkload(reportData);
                if (!string.IsNullOrEmpty(taskRecommendation))
                {
                    return taskRecommendation;
                }

                // Analyze absence patterns (second priority)
                var absenceRecommendation = AnalyzeAbsencePatterns(reportData);
                if (!string.IsNullOrEmpty(absenceRecommendation))
                {
                    return absenceRecommendation;
                }

                // Analyze shift capacity (lowest priority)
                var shiftRecommendation = AnalyzeShiftCapacity(reportData);
                if (!string.IsNullOrEmpty(shiftRecommendation))
                {
                    return shiftRecommendation;
                }

                // Default recommendation
                return GetDefaultRecommendation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI recommendation");
                return "خطا در تحلیل داده‌ها";
            }
        }

        private string AnalyzeCriticalEdgeCases(Dictionary<string, object> reportData)
        {
            try
            {
                // Get basic counts
                var employeeCount = GetEmployeeCount(reportData);
                var taskCount = GetTaskCount(reportData);
                var totalAbsences = GetTotalAbsenceCount(reportData);

                _logger.LogInformation("Critical edge case analysis: Employees={EmployeeCount}, Tasks={TaskCount}, Absences={TotalAbsences}", 
                    employeeCount, taskCount, totalAbsences);

                // Edge Case 1: No employees but has absences (data inconsistency)
                if (employeeCount == 0 && totalAbsences > 0)
                {
                    _logger.LogWarning("Data inconsistency detected: No employees but {AbsenceCount} absences exist", totalAbsences);
                    return "خطا در داده‌ها: وجود غیبت بدون کارمند";
                }

                // Edge Case 2: No employees but has tasks
                if (employeeCount == 0 && taskCount > 0)
                {
                    _logger.LogInformation("No employees but {TaskCount} tasks exist - recommending hiring", taskCount);
                    return "هیچ کارمندی در سیستم وجود ندارد. برای انجام کارها استخدام نیرو ضروری است";
                }

                // Edge Case 3: No employees and no tasks (empty system)
                if (employeeCount == 0 && taskCount == 0)
                {
                    _logger.LogInformation("Empty system detected - no employees or tasks");
                    return "سیستم خالی است. افزودن کارمند و تعریف کار توصیه می‌شود";
                }

                // Edge Case 4: Has employees but no tasks
                if (employeeCount > 0 && taskCount == 0)
                {
                    _logger.LogInformation("Has {EmployeeCount} employees but no tasks", employeeCount);
                    return "کارمندان موجود هستند اما کاری تعریف نشده. ایجاد کار یا بررسی بهره‌وری توصیه می‌شود";
                }

                // Edge Case 5: Critical absence rate (all or most employees absent)
                if (employeeCount > 0 && totalAbsences > 0)
                {
                    var absenceRate = (double)totalAbsences / employeeCount;
                    if (absenceRate >= 0.8) // 80% or more absent
                    {
                        _logger.LogWarning("Critical absence rate: {AbsenceRate:P} of employees absent", absenceRate);
                        return "وضعیت بحرانی: بیش از ۸۰٪ کارمندان غایب هستند";
                    }
                }

                // Edge Case 6: Data corruption - assigned employees in shifts but no employees in employee list
                var assignedEmployeeCount = GetAssignedEmployeeCount(reportData);
                if (assignedEmployeeCount > 0 && employeeCount == 0)
                {
                    _logger.LogWarning("Data corruption detected: {AssignedCount} employees assigned to shifts but no employees in system", assignedEmployeeCount);
                    return "خطا در داده‌ها: کارمندان در شیفت‌ها تعریف شده‌اند اما در سیستم وجود ندارند";
                }

                return string.Empty; // No critical edge cases detected
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing critical edge cases");
                return string.Empty;
            }
        }

        private int GetEmployeeCount(Dictionary<string, object> reportData)
        {
            if (reportData.TryGetValue("employees", out var employeesObj) && employeesObj is List<object> employees)
            {
                return employees.Count;
            }
            return 0;
        }

        private int GetTaskCount(Dictionary<string, object> reportData)
        {
            if (reportData.TryGetValue("tasks", out var tasksObj))
            {
                if (tasksObj is Dictionary<string, object> tasks)
                {
                    return tasks.Count(kvp => kvp.Key != "NextTaskId");
                }
                else if (tasksObj is Newtonsoft.Json.Linq.JObject jTasks)
                {
                    return jTasks.Properties().Count(prop => prop.Name != "NextTaskId");
                }
            }
            return 0;
        }

        private int GetTotalAbsenceCount(Dictionary<string, object> reportData)
        {
            if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
            {
                var leaveCount = GetAbsenceCount(absences, "مرخصی");
                var sickCount = GetAbsenceCount(absences, "بیمار");
                var absentCount = GetAbsenceCount(absences, "غایب");
                return leaveCount + sickCount + absentCount;
            }
            return 0;
        }

        private int GetAssignedEmployeeCount(Dictionary<string, object> reportData)
        {
            if (reportData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
            {
                var morningAssigned = GetAssignedCount(shifts, "morning");
                var eveningAssigned = GetAssignedCount(shifts, "evening");
                return morningAssigned + eveningAssigned;
            }
            return 0;
        }

        private string AnalyzeShiftCapacity(Dictionary<string, object> reportData)
        {
            try
            {
                var employeeCount = GetEmployeeCount(reportData);
                
                // Don't analyze shift capacity if there are no employees
                if (employeeCount == 0)
                {
                    return string.Empty;
                }

                if (reportData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
                {
                    var morningCapacity = GetShiftCapacity(shifts, "morning");
                    var eveningCapacity = GetShiftCapacity(shifts, "evening");
                    var morningAssigned = GetAssignedCount(shifts, "morning");
                    var eveningAssigned = GetAssignedCount(shifts, "evening");

                    var totalCapacity = morningCapacity + eveningCapacity;
                    var totalAssigned = morningAssigned + eveningAssigned;

                    _logger.LogInformation("Shift capacity analysis: TotalCapacity={TotalCapacity}, TotalAssigned={TotalAssigned}, Employees={EmployeeCount}", 
                        totalCapacity, totalAssigned, employeeCount);

                    // Check for data inconsistency
                    if (totalAssigned > employeeCount)
                    {
                        _logger.LogWarning("Data inconsistency: {AssignedCount} employees assigned to shifts but only {EmployeeCount} employees exist", 
                            totalAssigned, employeeCount);
                        return "خطا در داده‌ها: تعداد کارمندان تعریف شده در شیفت‌ها بیشتر از کل کارمندان است";
                    }

                    // Check if capacity is overutilized (more than 115% capacity)
                    if (totalAssigned > totalCapacity * 1.15)
                    {
                        return "اضافه کاری باید برنامه ریزی شود";
                    }

                    // Check if capacity is underutilized (less than 70% capacity)
                    if (totalAssigned < totalCapacity * 0.7)
                    {
                        var availableSlots = totalCapacity - totalAssigned;
                        var unassignedEmployees = employeeCount - totalAssigned;
                        
                        if (availableSlots >= 3 && unassignedEmployees > 0)
                        {
                            return "تا ۳ نفر می‌توانند به مرخصی بروند";
                        }
                        else if (unassignedEmployees > 0)
                        {
                            return "کارمندان تعریف نشده در شیفت‌ها وجود دارند";
                        }
                    }

                    // Check if all employees are assigned but capacity is available
                    if (totalAssigned == employeeCount && totalAssigned < totalCapacity)
                    {
                        var availableSlots = totalCapacity - totalAssigned;
                        if (availableSlots >= 2)
                        {
                            return "ظرفیت شیفت‌ها برای استخدام نیروی اضافی موجود است";
                        }
                    }

                    // Optimal capacity utilization
                    if (totalAssigned >= totalCapacity * 0.7 && totalAssigned <= totalCapacity * 1.0)
                    {
                        return "ظرفیت شیفت‌ها بهینه استفاده می‌شود";
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing shift capacity");
                return string.Empty;
            }
        }

        private string AnalyzeAbsencePatterns(Dictionary<string, object> reportData)
        {
            try
            {
                var employeeCount = GetEmployeeCount(reportData);
                
                // Don't analyze absences if there are no employees
                if (employeeCount == 0)
                {
                    return string.Empty;
                }

                if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
                {
                    var leaveCount = GetAbsenceCount(absences, "مرخصی");
                    var sickCount = GetAbsenceCount(absences, "بیمار");
                    var absentCount = GetAbsenceCount(absences, "غایب");
                    var totalAbsences = leaveCount + sickCount + absentCount;

                    // Calculate absence rate relative to employee count
                    var absenceRate = employeeCount > 0 ? (double)totalAbsences / employeeCount : 0;

                    _logger.LogInformation("Absence analysis: Total={TotalAbsences}, Employees={EmployeeCount}, Rate={AbsenceRate:P}", 
                        totalAbsences, employeeCount, absenceRate);

                    // High absence rate (more than 50% of employees absent)
                    if (absenceRate > 0.5)
                    {
                        if (sickCount > leaveCount && sickCount > absentCount)
                        {
                            return "نرخ بیماری بالا است. بررسی شرایط محیط کار توصیه می‌شود";
                        }
                        else if (absentCount > leaveCount && absentCount > sickCount)
                        {
                            return "نرخ غیبت غیرمجاز بالا است. بررسی انضباط کارکنان ضروری است";
                        }
                        else
                        {
                            return "نرخ غیبت بالا است. بررسی علل و راه‌حل‌ها ضروری است";
                        }
                    }

                    // Moderate absence rate (20-50% of employees absent)
                    if (absenceRate > 0.2)
                    {
                        return "نرخ غیبت متوسط است. نظارت بیشتر توصیه می‌شود";
                    }

                    // Low absence rate (less than 20% of employees absent)
                    if (totalAbsences > 0 && absenceRate <= 0.2)
                    {
                        return "وضعیت حضور کارکنان مطلوب است";
                    }

                    // Very low or no absences
                    if (totalAbsences == 0)
                    {
                        return "وضعیت حضور کارکنان عالی است";
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing absence patterns");
                return string.Empty;
            }
        }

        private string AnalyzeTaskWorkload(Dictionary<string, object> reportData)
        {
            try
            {
                var employeeCount = GetEmployeeCount(reportData);
                
                if (reportData.TryGetValue("tasks", out var tasksObj))
                {
                    var totalTasks = 0;
                    var completedTasks = 0;
                    var inProgressTasks = 0;
                    var pendingTasks = 0;

                    // Handle both Dictionary and JObject
                    if (tasksObj is Dictionary<string, object> tasks)
                    {
                        foreach (var kvp in tasks)
                        {
                            // Skip NextTaskId
                            if (kvp.Key == "NextTaskId") continue;
                            
                            totalTasks++;
                            
                            // Parse the task JSON string
                            if (kvp.Value is string taskJson)
                            {
                                try
                                {
                                    var taskData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(taskJson);
                                    if (taskData != null && taskData.TryGetValue("Status", out var statusObj))
                                    {
                                        var status = statusObj.ToString();
                                        switch (status)
                                        {
                                            case "تکمیل شده":
                                                completedTasks++;
                                                break;
                                            case "در حال انجام":
                                                inProgressTasks++;
                                                break;
                                            case "در انتظار":
                                                pendingTasks++;
                                                break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error parsing task JSON: {TaskJson}", taskJson);
                                }
                            }
                        }
                    }
                    else if (tasksObj is Newtonsoft.Json.Linq.JObject jTasks)
                    {
                        foreach (var prop in jTasks.Properties())
                        {
                            // Skip NextTaskId
                            if (prop.Name == "NextTaskId") continue;
                            
                            totalTasks++;
                            
                            // Parse the task JSON string
                            if (prop.Value is Newtonsoft.Json.Linq.JValue jValue && jValue.Value is string taskJson)
                            {
                                try
                                {
                                    var taskData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(taskJson);
                                    if (taskData != null && taskData.TryGetValue("Status", out var statusObj))
                                    {
                                        var status = statusObj.ToString();
                                        switch (status)
                                        {
                                            case "تکمیل شده":
                                                completedTasks++;
                                                break;
                                            case "در حال انجام":
                                                inProgressTasks++;
                                                break;
                                            case "در انتظار":
                                                pendingTasks++;
                                                break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error parsing task JSON: {TaskJson}", taskJson);
                                }
                            }
                        }
                    }

                    _logger.LogInformation("Task analysis: Total={TotalTasks}, Completed={CompletedTasks}, InProgress={InProgressTasks}, Pending={PendingTasks}, Employees={EmployeeCount}", 
                        totalTasks, completedTasks, inProgressTasks, pendingTasks, employeeCount);

                    // Calculate task-to-employee ratio for better context
                    var taskToEmployeeRatio = employeeCount > 0 ? (double)totalTasks / employeeCount : totalTasks;

                    // High workload scenarios
                    if (pendingTasks > completedTasks && pendingTasks > inProgressTasks)
                    {
                        if (employeeCount == 0)
                        {
                            return "بار کاری بالا است. استخدام نیروی اضافی توصیه می‌شود";
                        }
                        else if (taskToEmployeeRatio > 5) // More than 5 tasks per employee
                        {
                            return "بار کاری بسیار بالا است. استخدام نیروی اضافی یا کاهش کارها ضروری است";
                        }
                        else
                        {
                            return "بار کاری بالا است. استخدام نیروی اضافی توصیه می‌شود";
                        }
                    }

                    // Good progress scenarios
                    if (completedTasks > pendingTasks && completedTasks > inProgressTasks)
                    {
                        if (completedTasks > totalTasks * 0.7) // More than 70% completed
                        {
                            return "پیشرفت کارها عالی است";
                        }
                        else
                        {
                            return "پیشرفت کارها مطلوب است";
                        }
                    }

                    // Many in-progress tasks
                    if (inProgressTasks > totalTasks * 0.6)
                    {
                        return "تعداد زیادی کار در حال انجام است. اولویت‌بندی مجدد ضروری است";
                    }

                    // Balanced workload
                    if (totalTasks > 0 && employeeCount > 0 && taskToEmployeeRatio <= 3)
                    {
                        return "بار کاری متعادل است";
                    }

                    // Low task count relative to employees
                    if (totalTasks > 0 && employeeCount > 0 && taskToEmployeeRatio < 1)
                    {
                        return "کارمندان آماده دریافت کارهای بیشتر هستند";
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing task workload");
                return string.Empty;
            }
        }

        private string GetDefaultRecommendation()
        {
            var recommendations = new[]
            {
                "وضعیت کلی سیستم مطلوب است",
                "همه چیز طبق برنامه پیش می‌رود",
                "عملکرد تیم در سطح قابل قبولی است",
                "نیاز به بهبود خاصی مشاهده نمی‌شود"
            };

            var random = new Random();
            return recommendations[random.Next(recommendations.Length)];
        }

        private int GetShiftCapacity(Dictionary<string, object> shifts, string shiftType)
        {
            if (shifts.TryGetValue(shiftType, out var shiftObj) && shiftObj is Dictionary<string, object> shift)
            {
                if (shift.TryGetValue("capacity", out var capacityObj))
                {
                    if (int.TryParse(capacityObj.ToString(), out var capacity))
                    {
                        return capacity;
                    }
                }
            }
            return 15; // Default capacity
        }

        private int GetAssignedCount(Dictionary<string, object> shifts, string shiftType)
        {
            if (shifts.TryGetValue(shiftType, out var shiftObj) && shiftObj is Dictionary<string, object> shift)
            {
                if (shift.TryGetValue("assigned_employees", out var employeesObj) && employeesObj is List<object> employees)
                {
                    return employees.Count;
                }
            }
            return 0;
        }

        private int GetAbsenceCount(Dictionary<string, object> absences, string category)
        {
            if (absences.TryGetValue(category, out var categoryObj) && categoryObj is List<object> categoryList)
            {
                return categoryList.Count;
            }
            return 0;
        }

        public Dictionary<string, object> GetInsights(Dictionary<string, object> reportData)
        {
            try
            {
                var insights = new Dictionary<string, object>();

                // Shift insights
                if (reportData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
                {
                    var morningCapacity = GetShiftCapacity(shifts, "morning");
                    var eveningCapacity = GetShiftCapacity(shifts, "evening");
                    var morningAssigned = GetAssignedCount(shifts, "morning");
                    var eveningAssigned = GetAssignedCount(shifts, "evening");

                    insights["morning_utilization"] = morningCapacity > 0 ? (double)morningAssigned / morningCapacity * 100 : 0;
                    insights["evening_utilization"] = eveningCapacity > 0 ? (double)eveningAssigned / eveningCapacity * 100 : 0;
                    insights["total_utilization"] = (morningCapacity + eveningCapacity) > 0 ? 
                        (double)(morningAssigned + eveningAssigned) / (morningCapacity + eveningCapacity) * 100 : 0;
                }

                // Absence insights
                if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
                {
                    var leaveCount = GetAbsenceCount(absences, "مرخصی");
                    var sickCount = GetAbsenceCount(absences, "بیمار");
                    var absentCount = GetAbsenceCount(absences, "غایب");
                    var totalAbsences = leaveCount + sickCount + absentCount;

                    insights["total_absences"] = totalAbsences;
                    insights["absence_rate"] = totalAbsences > 0 ? (double)absentCount / totalAbsences * 100 : 0;
                }

                // Task insights
                if (reportData.TryGetValue("tasks", out var tasksObj))
                {
                    var totalTasks = 0;
                    var completedTasks = 0;
                    var inProgressTasks = 0;
                    var pendingTasks = 0;

                    // Handle both Dictionary and JObject
                    if (tasksObj is Dictionary<string, object> tasks)
                    {
                        foreach (var kvp in tasks)
                        {
                            // Skip NextTaskId
                            if (kvp.Key == "NextTaskId") continue;
                            
                            totalTasks++;
                            
                            // Parse the task JSON string
                            if (kvp.Value is string taskJson)
                            {
                                try
                                {
                                    var taskData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(taskJson);
                                    if (taskData != null && taskData.TryGetValue("Status", out var statusObj))
                                    {
                                        var status = statusObj.ToString();
                                        switch (status)
                                        {
                                            case "تکمیل شده":
                                                completedTasks++;
                                                break;
                                            case "در حال انجام":
                                                inProgressTasks++;
                                                break;
                                            case "در انتظار":
                                                pendingTasks++;
                                                break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error parsing task JSON in insights: {TaskJson}", taskJson);
                                }
                            }
                        }
                    }
                    else if (tasksObj is Newtonsoft.Json.Linq.JObject jTasks)
                    {
                        foreach (var prop in jTasks.Properties())
                        {
                            // Skip NextTaskId
                            if (prop.Name == "NextTaskId") continue;
                            
                            totalTasks++;
                            
                            // Parse the task JSON string
                            if (prop.Value is Newtonsoft.Json.Linq.JValue jValue && jValue.Value is string taskJson)
                            {
                                try
                                {
                                    var taskData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(taskJson);
                                    if (taskData != null && taskData.TryGetValue("Status", out var statusObj))
                                    {
                                        var status = statusObj.ToString();
                                        switch (status)
                                        {
                                            case "تکمیل شده":
                                                completedTasks++;
                                                break;
                                            case "در حال انجام":
                                                inProgressTasks++;
                                                break;
                                            case "در انتظار":
                                                pendingTasks++;
                                                break;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error parsing task JSON in insights: {TaskJson}", taskJson);
                                }
                            }
                        }
                    }

                    insights["total_tasks"] = totalTasks;
                    insights["completion_rate"] = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;
                    insights["pending_tasks"] = pendingTasks;
                }

                _logger.LogInformation("Generated insights for report data");
                return insights;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating insights");
                return new Dictionary<string, object>();
            }
        }
    }
}