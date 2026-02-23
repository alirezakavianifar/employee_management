using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Shared.Models
{
    public class DailyTaskProgress
    {
        public string ProgressId { get; set; } = string.Empty; // Format: "{groupId}_{shiftType}_{date}"
        public string GroupId { get; set; } = string.Empty;
        public string ShiftType { get; set; } = string.Empty; // "morning" or "evening"
        public string Date { get; set; } = string.Empty; // Shamsi date in yyyy/MM/dd format
        public int CompletedBoxes { get; set; } = 0;
        public int DailyTarget { get; set; } = 100;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public DailyTaskProgress()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public DailyTaskProgress(string groupId, string shiftType, string date, int completedBoxes, int dailyTarget = 100)
        {
            GroupId = groupId;
            ShiftType = shiftType;
            Date = date;
            CompletedBoxes = completedBoxes;
            DailyTarget = dailyTarget;
            ProgressId = $"{groupId}_{shiftType}_{date}";
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public static DailyTaskProgress FromJson(string json)
        {
            var progressData = JsonConvert.DeserializeObject<DailyTaskProgressData>(json);
            if (progressData == null)
                return new DailyTaskProgress();

            return new DailyTaskProgress
            {
                ProgressId = progressData.ProgressId ?? string.Empty,
                GroupId = progressData.GroupId ?? string.Empty,
                ShiftType = progressData.ShiftType ?? string.Empty,
                Date = progressData.Date ?? string.Empty,
                CompletedBoxes = progressData.CompletedBoxes,
                DailyTarget = progressData.DailyTarget,
                CreatedAt = progressData.CreatedAt,
                UpdatedAt = progressData.UpdatedAt
            };
        }

        public string ToJson()
        {
            var progressData = new DailyTaskProgressData
            {
                ProgressId = ProgressId,
                GroupId = GroupId,
                ShiftType = ShiftType,
                Date = Date,
                CompletedBoxes = CompletedBoxes,
                DailyTarget = DailyTarget,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
            return JsonConvert.SerializeObject(progressData, Formatting.Indented);
        }

        private class DailyTaskProgressData
        {
            public string ProgressId { get; set; } = string.Empty;
            public string GroupId { get; set; } = string.Empty;
            public string ShiftType { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
            public int CompletedBoxes { get; set; } = 0;
            public int DailyTarget { get; set; } = 100;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }

    public class ProgressStatus
    {
        public int Completed { get; set; }
        public int Target { get; set; }
        public double Percentage { get; set; }
        public int Difference { get; set; }
        public string StatusText { get; set; } = string.Empty; // e.g. "In progress", "Behind", "On track"
        public bool IsAhead { get; set; }
        public bool IsBehind { get; set; }
        public bool IsOnTrack { get; set; }
    }

    public class WeeklyProgressStatus
    {
        public string GroupId { get; set; } = string.Empty;
        public string ShiftType { get; set; } = string.Empty;
        public string WeekStartDate { get; set; } = string.Empty;
        public int TotalCompleted { get; set; }
        public int WeeklyTarget { get; set; } = 1000;
        public double Percentage { get; set; }
        public int Difference { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public bool IsAhead { get; set; }
        public bool IsBehind { get; set; }
        public bool IsOnTrack { get; set; }
        public List<DailyTaskProgress> DailyProgress { get; set; } = new();
    }

    public class DailyTaskProgressManager
    {
        public Dictionary<string, DailyTaskProgress> ProgressRecords { get; set; } = new();

        public void AddProgress(DailyTaskProgress progress)
        {
            progress.UpdatedAt = DateTime.Now;
            if (string.IsNullOrEmpty(progress.ProgressId))
            {
                progress.ProgressId = $"{progress.GroupId}_{progress.ShiftType}_{progress.Date}";
            }
            ProgressRecords[progress.ProgressId] = progress;
        }

        public void UpdateProgress(string progressId, int completedBoxes)
        {
            if (ProgressRecords.ContainsKey(progressId))
            {
                ProgressRecords[progressId].CompletedBoxes = completedBoxes;
                ProgressRecords[progressId].UpdatedAt = DateTime.Now;
            }
            else
            {
                var parts = progressId.Split('_');
                if (parts.Length >= 3)
                {
                    var groupId = parts[0];
                    var shiftType = parts[1];
                    var date = string.Join("_", parts.Skip(2));
                    var progress = new DailyTaskProgress(groupId, shiftType, date, completedBoxes);
                    ProgressRecords[progressId] = progress;
                }
            }
        }

        public DailyTaskProgress? GetProgress(string progressId)
        {
            return ProgressRecords.GetValueOrDefault(progressId);
        }

        public DailyTaskProgress? GetProgress(string groupId, string shiftType, string date)
        {
            var progressId = $"{groupId}_{shiftType}_{date}";
            return GetProgress(progressId);
        }

        public List<DailyTaskProgress> GetProgressForGroupAndShift(string groupId, string shiftType)
        {
            return ProgressRecords.Values
                .Where(p => p.GroupId == groupId && p.ShiftType == shiftType)
                .OrderBy(p => p.Date)
                .ToList();
        }

        public List<DailyTaskProgress> GetWeeklyProgress(string groupId, string shiftType, string weekStartDate)
        {
            var weekStart = Shared.Utils.ShamsiDateHelper.FromShamsiString(weekStartDate);
            var weekEnd = weekStart.AddDays(6);
            
            return ProgressRecords.Values
                .Where(p => p.GroupId == groupId && p.ShiftType == shiftType)
                .Where(p =>
                {
                    var progressDate = Shared.Utils.ShamsiDateHelper.FromShamsiString(p.Date);
                    return progressDate >= weekStart && progressDate <= weekEnd;
                })
                .OrderBy(p => p.Date)
                .ToList();
        }

        public static DailyTaskProgressManager FromJson(string json)
        {
            var managerData = JsonConvert.DeserializeObject<DailyTaskProgressManagerData>(json);
            if (managerData == null)
                return new DailyTaskProgressManager();

            var manager = new DailyTaskProgressManager();

            foreach (var kvp in managerData.ProgressRecords)
            {
                try
                {
                    var progress = DailyTaskProgress.FromJson(kvp.Value);
                    manager.ProgressRecords[kvp.Key] = progress;
                }
                catch (Exception)
                {
                    // Skip invalid records
                    continue;
                }
            }

            return manager;
        }

        public string ToJson()
        {
            var managerData = new DailyTaskProgressManagerData
            {
                ProgressRecords = ProgressRecords.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToJson()
                )
            };
            return JsonConvert.SerializeObject(managerData, Formatting.Indented);
        }

        private class DailyTaskProgressManagerData
        {
            public Dictionary<string, string> ProgressRecords { get; set; } = new();
        }
    }
}
