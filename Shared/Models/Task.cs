using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shared.Utils;

namespace Shared.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TaskStatus
    {
        [JsonProperty("در انتظار")]
        Pending,
        [JsonProperty("در حال انجام")]
        InProgress,
        [JsonProperty("تکمیل شده")]
        Completed,
        [JsonProperty("لغو شده")]
        Cancelled
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum TaskPriority
    {
        [JsonProperty("کم")]
        Low,
        [JsonProperty("متوسط")]
        Medium,
        [JsonProperty("زیاد")]
        High,
        [JsonProperty("فوری")]
        Urgent
    }

    public class Task
    {
        public string TaskId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public double EstimatedHours { get; set; } = 8.0;
        public string TargetDate { get; set; } = string.Empty; // Shamsi date in yyyy/MM/dd format
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public List<string> AssignedEmployees { get; set; } = new();
        public string? StartDate { get; set; } // Shamsi date in yyyy/MM/dd format
        public string? CompletionDate { get; set; } // Shamsi date in yyyy/MM/dd format
        public double ActualHours { get; set; } = 0.0;
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Task()
        {
            TargetDate = GeorgianDateHelper.GetCurrentGeorgianDate();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public Task(string taskId, string title, string description = "", TaskPriority priority = TaskPriority.Medium,
                   double estimatedHours = 8.0, string? targetDate = null)
        {
            TaskId = taskId;
            Title = title;
            Description = description;
            Priority = priority;
            EstimatedHours = estimatedHours;
            TargetDate = targetDate ?? GeorgianDateHelper.GetCurrentGeorgianDate();
            Status = TaskStatus.Pending;
            AssignedEmployees = new List<string>();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public bool StartTask()
        {
            if (Status == TaskStatus.Pending)
            {
                Status = TaskStatus.InProgress;
                StartDate = GeorgianDateHelper.GetCurrentGeorgianDate();
                UpdatedAt = DateTime.Now;
                return true;
            }
            return false;
        }

        public bool CompleteTask(double? actualHours = null, string notes = "")
        {
            if (Status == TaskStatus.InProgress)
            {
                Status = TaskStatus.Completed;
                CompletionDate = GeorgianDateHelper.GetCurrentGeorgianDate();
                if (actualHours.HasValue)
                    ActualHours = actualHours.Value;
                if (!string.IsNullOrEmpty(notes))
                    Notes = notes;
                UpdatedAt = DateTime.Now;
                return true;
            }
            return false;
        }

        public bool AssignEmployee(string employeeId)
        {
            if (!AssignedEmployees.Contains(employeeId))
            {
                AssignedEmployees.Add(employeeId);
                UpdatedAt = DateTime.Now;
                return true;
            }
            return false;
        }

        public bool RemoveEmployee(string employeeId)
        {
            if (AssignedEmployees.Contains(employeeId))
            {
                AssignedEmployees.Remove(employeeId);
                UpdatedAt = DateTime.Now;
                return true;
            }
            return false;
        }

        public int GetWeekNumber()
        {
            try
            {
                if (DateTime.TryParseExact(TargetDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var targetDate))
                {
                    var calendar = CultureInfo.CurrentCulture.Calendar;
                    return calendar.GetWeekOfYear(targetDate, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
                }
            }
            catch
            {
                // Fall back to current week
            }
            return CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(DateTime.Now, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
        }

        [JsonIgnore]
        public string TargetDateDisplay => GeorgianDateHelper.FormatForDisplay(TargetDate);

        [JsonIgnore]
        public string StartDateDisplay => string.IsNullOrEmpty(StartDate) ? "" : GeorgianDateHelper.FormatForDisplay(StartDate);

        [JsonIgnore]
        public string CompletionDateDisplay => string.IsNullOrEmpty(CompletionDate) ? "" : GeorgianDateHelper.FormatForDisplay(CompletionDate);

        [JsonIgnore]
        public string TargetDateGregorian => GeorgianDateHelper.GeorgianToGregorian(TargetDate);

        [JsonIgnore]
        public string AssignedEmployeesDisplay => AssignedEmployees.Count == 0 ? "تخصیص داده نشده" : string.Join(", ", AssignedEmployees);

        public override string ToString()
        {
            return $"Task({TaskId}: {Title})";
        }

        public static Task FromJson(string json)
        {
            var taskData = JsonConvert.DeserializeObject<TaskData>(json);
            if (taskData == null)
                return new Task();

            var task = new Task(
                taskData.TaskId,
                taskData.Title,
                taskData.Description,
                taskData.Priority,
                taskData.EstimatedHours,
                taskData.TargetDate);

            task.Status = taskData.Status;
            task.AssignedEmployees = taskData.AssignedEmployees;
            task.StartDate = taskData.StartDate;
            task.CompletionDate = taskData.CompletionDate;
            task.ActualHours = taskData.ActualHours;
            task.Notes = taskData.Notes;
            task.CreatedAt = taskData.CreatedAt;
            task.UpdatedAt = taskData.UpdatedAt;

            return task;
        }

        public string ToJson()
        {
            var taskData = new TaskData
            {
                TaskId = TaskId,
                Title = Title,
                Description = Description,
                Priority = Priority,
                EstimatedHours = EstimatedHours,
                TargetDate = TargetDate,
                Status = Status,
                AssignedEmployees = AssignedEmployees,
                StartDate = StartDate,
                CompletionDate = CompletionDate,
                ActualHours = ActualHours,
                Notes = Notes,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
            return JsonConvert.SerializeObject(taskData, Formatting.Indented);
        }

        private class TaskData
        {
            public string TaskId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            
            [JsonConverter(typeof(PersianEnumConverter<TaskPriority>))]
            public TaskPriority Priority { get; set; } = TaskPriority.Medium;
            
            public double EstimatedHours { get; set; } = 8.0;
            public string TargetDate { get; set; } = string.Empty;
            
            [JsonConverter(typeof(PersianEnumConverter<TaskStatus>))]
            public TaskStatus Status { get; set; } = TaskStatus.Pending;
            
            public List<string> AssignedEmployees { get; set; } = new();
            public string? StartDate { get; set; }
            public string? CompletionDate { get; set; }
            public double ActualHours { get; set; } = 0.0;
            public string Notes { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private class PersianEnumConverter<T> : JsonConverter<T> where T : struct, Enum
        {
            public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
            {
                var field = typeof(T).GetField(value.ToString());
                var attribute = field?.GetCustomAttributes(typeof(JsonPropertyAttribute), false)
                    .FirstOrDefault() as JsonPropertyAttribute;
                
                writer.WriteValue(attribute?.PropertyName ?? value.ToString());
            }

            public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                    return default(T);

                var value = reader.Value?.ToString();
                if (string.IsNullOrEmpty(value))
                    return default(T);

                // Try to find enum value by JsonProperty attribute
                foreach (var field in typeof(T).GetFields())
                {
                    var attribute = field.GetCustomAttributes(typeof(JsonPropertyAttribute), false)
                        .FirstOrDefault() as JsonPropertyAttribute;
                    
                    if (attribute?.PropertyName == value)
                    {
                        return (T)field.GetValue(null);
                    }
                }

                // Fallback to direct enum parsing
                if (Enum.TryParse<T>(value, out T result))
                    return result;

                return default(T);
            }
        }
    }

    public class TaskManager
    {
        public Dictionary<string, Task> Tasks { get; set; } = new();
        public int NextTaskId { get; set; } = 1;

        public string AddTask(string title, string description = "", TaskPriority priority = TaskPriority.Medium,
                            double estimatedHours = 1.0, string? targetDate = null)
        {
            var taskId = $"TASK_{NextTaskId:0000}";
            NextTaskId++;

            var task = new Task(taskId, title, description, priority, estimatedHours, targetDate);
            Tasks[taskId] = task;
            return taskId;
        }

        public string AddExistingTask(Task task)
        {
            if (string.IsNullOrEmpty(task.TaskId))
            {
                var taskId = $"TASK_{NextTaskId:0000}";
                NextTaskId++;
                task.TaskId = taskId;
            }
            
            Tasks[task.TaskId] = task;
            return task.TaskId;
        }

        public Task? GetTask(string taskId)
        {
            return Tasks.GetValueOrDefault(taskId);
        }

        public bool UpdateTask(string taskId, string? title = null, string? description = null,
                             TaskPriority? priority = null, double? estimatedHours = null,
                             string? targetDate = null, TaskStatus? status = null,
                             double? actualHours = null, string? notes = null)
        {
            if (!Tasks.ContainsKey(taskId))
                return false;

            var task = Tasks[taskId];
            
            if (!string.IsNullOrEmpty(title))
                task.Title = title;
            if (!string.IsNullOrEmpty(description))
                task.Description = description;
            if (priority.HasValue)
                task.Priority = priority.Value;
            if (estimatedHours.HasValue)
                task.EstimatedHours = estimatedHours.Value;
            if (!string.IsNullOrEmpty(targetDate))
                task.TargetDate = targetDate;
            if (status.HasValue)
                task.Status = status.Value;
            if (actualHours.HasValue)
                task.ActualHours = actualHours.Value;
            if (!string.IsNullOrEmpty(notes))
                task.Notes = notes;

            task.UpdatedAt = DateTime.Now;
            return true;
        }

        public bool DeleteTask(string taskId)
        {
            return Tasks.Remove(taskId);
        }

        public List<Task> GetTasksByStatus(TaskStatus status)
        {
            return Tasks.Values.Where(task => task.Status == status).ToList();
        }

        public List<Task> GetTasksByWeek(int weekNumber)
        {
            return Tasks.Values.Where(task => task.GetWeekNumber() == weekNumber).ToList();
        }

        public List<Task> GetTasksByDate(string targetDate)
        {
            return Tasks.Values.Where(task => task.TargetDate == targetDate).ToList();
        }

        public List<Task> GetInProgressTasks()
        {
            return GetTasksByStatus(TaskStatus.InProgress);
        }

        public List<Task> GetCompletedTasks(string? date = null)
        {
            var completed = GetTasksByStatus(TaskStatus.Completed);
            if (!string.IsNullOrEmpty(date))
            {
                completed = completed.Where(task => 
                    !string.IsNullOrEmpty(task.CompletionDate) && 
                    task.CompletionDate.StartsWith(date)).ToList();
            }
            return completed;
        }

        public List<Task> GetPendingTasks()
        {
            return GetTasksByStatus(TaskStatus.Pending);
        }

        public double GetTotalEstimatedHours(TaskStatus? status = null)
        {
            var tasks = Tasks.Values.AsEnumerable();
            if (status.HasValue)
                tasks = tasks.Where(task => task.Status == status.Value);
            
            return tasks.Sum(task => task.EstimatedHours);
        }

        public double GetTotalActualHours(TaskStatus? status = null)
        {
            var tasks = Tasks.Values.AsEnumerable();
            if (status.HasValue)
                tasks = tasks.Where(task => task.Status == status.Value);
            
            return tasks.Sum(task => task.ActualHours);
        }

        public Dictionary<string, object> GetTaskProgress()
        {
            var totalTasks = Tasks.Count;
            var completedTasks = GetCompletedTasks().Count;
            var inProgressTasks = GetInProgressTasks().Count;
            var pendingTasks = GetPendingTasks().Count;
            
            var progressPercentage = totalTasks > 0 ? (completedTasks / (double)totalTasks * 100) : 0;

            return new Dictionary<string, object>
            {
                { "total_tasks", totalTasks },
                { "completed_tasks", completedTasks },
                { "in_progress_tasks", inProgressTasks },
                { "pending_tasks", pendingTasks },
                { "progress_percentage", Math.Round(progressPercentage, 1) },
                { "total_estimated_hours", GetTotalEstimatedHours() },
                { "total_actual_hours", GetTotalActualHours() },
                { "completed_estimated_hours", GetTotalEstimatedHours(TaskStatus.Completed) },
                { "completed_actual_hours", GetTotalActualHours(TaskStatus.Completed) }
            };
        }

        public static TaskManager FromJson(string json)
        {
            var managerData = JsonConvert.DeserializeObject<TaskManagerData>(json);
            if (managerData == null)
                return new TaskManager();

            var manager = new TaskManager
            {
                NextTaskId = managerData.NextTaskId
            };

            foreach (var kvp in managerData.Tasks)
            {
                try
                {
                    var task = Task.FromJson(kvp.Value);
                    manager.Tasks[kvp.Key] = task;
                }
                catch (Exception ex)
                {
                    // Log error and continue
                    System.Diagnostics.Debug.WriteLine($"Error loading task {kvp.Key}: {ex.Message}");
                }
            }

            return manager;
        }

        public string ToJson()
        {
            var managerData = new TaskManagerData
            {
                Tasks = Tasks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToJson()),
                NextTaskId = NextTaskId
            };
            return JsonConvert.SerializeObject(managerData, Formatting.Indented);
        }

        private class TaskManagerData
        {
            public Dictionary<string, string> Tasks { get; set; } = new();
            public int NextTaskId { get; set; } = 1;
        }
    }
}
