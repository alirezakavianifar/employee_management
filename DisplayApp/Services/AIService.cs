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
                // Analyze shift capacity
                var shiftRecommendation = AnalyzeShiftCapacity(reportData);
                if (!string.IsNullOrEmpty(shiftRecommendation))
                {
                    return shiftRecommendation;
                }

                // Analyze task workload (higher priority than absence patterns)
                var taskRecommendation = AnalyzeTaskWorkload(reportData);
                if (!string.IsNullOrEmpty(taskRecommendation))
                {
                    return taskRecommendation;
                }

                // Analyze absence patterns
                var absenceRecommendation = AnalyzeAbsencePatterns(reportData);
                if (!string.IsNullOrEmpty(absenceRecommendation))
                {
                    return absenceRecommendation;
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

        private string AnalyzeShiftCapacity(Dictionary<string, object> reportData)
        {
            try
            {
                if (reportData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
                {
                    var morningCapacity = GetShiftCapacity(shifts, "morning");
                    var eveningCapacity = GetShiftCapacity(shifts, "evening");
                    var morningAssigned = GetAssignedCount(shifts, "morning");
                    var eveningAssigned = GetAssignedCount(shifts, "evening");

                    var totalCapacity = morningCapacity + eveningCapacity;
                    var totalAssigned = morningAssigned + eveningAssigned;

                    // Check if capacity is underutilized
                    if (totalAssigned < totalCapacity * 0.7) // Less than 70% capacity
                    {
                        var availableSlots = totalCapacity - totalAssigned;
                        if (availableSlots >= 3)
                        {
                            return "تا ۳ نفر می‌توانند به مرخصی بروند";
                        }
                    }

                    // Check if capacity is overutilized
                    if (totalAssigned > totalCapacity * 1.15) // More than 115% capacity
                    {
                        return "اضافه کاری باید برنامه ریزی شود";
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
                if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
                {
                    var leaveCount = GetAbsenceCount(absences, "مرخصی");
                    var sickCount = GetAbsenceCount(absences, "بیمار");
                    var absentCount = GetAbsenceCount(absences, "غایب");
                    var totalAbsences = leaveCount + sickCount + absentCount;

                    // High absence rate
                    if (totalAbsences > 10)
                    {
                        if (sickCount > leaveCount && sickCount > absentCount)
                        {
                            return "نرخ بیماری بالا است. بررسی شرایط محیط کار توصیه می‌شود";
                        }
                        else if (absentCount > leaveCount && absentCount > sickCount)
                        {
                            return "نرخ غیبت غیرمجاز بالا است. بررسی انضباط کارکنان ضروری است";
                        }
                    }

                    // Low absence rate
                    if (totalAbsences < 3)
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

                    _logger.LogInformation("Task analysis: Total={TotalTasks}, Completed={CompletedTasks}, InProgress={InProgressTasks}, Pending={PendingTasks}", 
                        totalTasks, completedTasks, inProgressTasks, pendingTasks);

                    // High workload
                    if (pendingTasks > completedTasks)
                    {
                        return "بار کاری بالا است. استخدام نیروی اضافی توصیه می‌شود";
                    }

                    // Good progress
                    if (completedTasks > pendingTasks && completedTasks > inProgressTasks)
                    {
                        return "پیشرفت کارها عالی است";
                    }

                    // Many in-progress tasks
                    if (inProgressTasks > totalTasks * 0.6)
                    {
                        return "تعداد زیادی کار در حال انجام است. اولویت‌بندی مجدد ضروری است";
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
