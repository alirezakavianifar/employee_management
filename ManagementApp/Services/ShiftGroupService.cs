using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Services;

namespace ManagementApp.Services
{
    public class ShiftGroupService
    {
        private readonly ILogger<ShiftGroupService> _logger;

        public ShiftGroupService()
        {
            _logger = LoggingService.CreateLogger<ShiftGroupService>();
        }

        /// <summary>
        /// Validates shift group data before creation or update
        /// </summary>
        public bool ValidateShiftGroup(string groupId, string name, int morningCapacity, int eveningCapacity, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(groupId))
            {
                errorMessage = "شناسه گروه نمی‌تواند خالی باشد";
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errorMessage = "نام گروه نمی‌تواند خالی باشد";
                return false;
            }

            if (morningCapacity < 1)
            {
                errorMessage = "ظرفیت شیفت صبح باید حداقل 1 باشد";
                return false;
            }

            if (eveningCapacity < 1)
            {
                errorMessage = "ظرفیت شیفت عصر باید حداقل 1 باشد";
                return false;
            }

            if (groupId == "default")
            {
                errorMessage = "شناسه 'default' برای گروه پیش‌فرض محفوظ است";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Generates a unique group ID based on the group name
        /// </summary>
        public string GenerateGroupId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return $"group_{DateTimeOffset.Now.ToUnixTimeSeconds()}";

            // Convert Persian/Arabic characters to English equivalents for ID generation
            var englishName = ConvertToEnglish(name);
            var cleanName = new string(englishName.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            if (string.IsNullOrEmpty(cleanName))
                return $"group_{DateTimeOffset.Now.ToUnixTimeSeconds()}";

            return cleanName.ToLower();
        }

        /// <summary>
        /// Converts Persian/Arabic characters to English equivalents
        /// </summary>
        private string ConvertToEnglish(string persianText)
        {
            var persianToEnglish = new Dictionary<char, string>
            {
                {'ا', "a"}, {'ب', "b"}, {'پ', "p"}, {'ت', "t"}, {'ث', "s"}, {'ج', "j"}, {'چ', "ch"},
                {'ح', "h"}, {'خ', "kh"}, {'د', "d"}, {'ذ', "z"}, {'ر', "r"}, {'ز', "z"}, {'ژ', "zh"},
                {'س', "s"}, {'ش', "sh"}, {'ص', "s"}, {'ض', "z"}, {'ط', "t"}, {'ظ', "z"}, {'ع', "a"},
                {'غ', "gh"}, {'ف', "f"}, {'ق', "q"}, {'ک', "k"}, {'گ', "g"}, {'ل', "l"}, {'م', "m"},
                {'ن', "n"}, {'و', "v"}, {'ه', "h"}, {'ی', "y"}, {'ء', "a"}, {'آ', "a"}, {'أ', "a"},
                {'إ', "a"}, {'ؤ', "v"}, {'ئ', "y"}, {'ة', "h"}
            };

            var result = new System.Text.StringBuilder();
            foreach (char c in persianText)
            {
                if (persianToEnglish.ContainsKey(c))
                {
                    result.Append(persianToEnglish[c]);
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets statistics for a shift group
        /// </summary>
        public ShiftGroupStatistics GetGroupStatistics(ShiftGroup group)
        {
            try
            {
                var morningAssigned = group.MorningShift.AssignedEmployees.Count(emp => emp != null);
                var eveningAssigned = group.EveningShift.AssignedEmployees.Count(emp => emp != null);
                var totalAssigned = morningAssigned + eveningAssigned;
                var totalCapacity = group.MorningCapacity + group.EveningCapacity;
                var utilizationRate = totalCapacity > 0 ? (double)totalAssigned / totalCapacity * 100 : 0;

                return new ShiftGroupStatistics
                {
                    GroupId = group.GroupId,
                    GroupName = group.Name,
                    MorningAssigned = morningAssigned,
                    MorningCapacity = group.MorningCapacity,
                    EveningAssigned = eveningAssigned,
                    EveningCapacity = group.EveningCapacity,
                    TotalAssigned = totalAssigned,
                    TotalCapacity = totalCapacity,
                    UtilizationRate = utilizationRate,
                    IsActive = group.IsActive
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating group statistics for group {GroupId}", group.GroupId);
                return new ShiftGroupStatistics { GroupId = group.GroupId, GroupName = group.Name };
            }
        }

        /// <summary>
        /// Gets overall statistics for all shift groups
        /// </summary>
        public OverallShiftGroupStatistics GetOverallStatistics(ShiftGroupManager manager)
        {
            try
            {
                var groups = manager.GetAllShiftGroups();
                var activeGroups = manager.GetActiveShiftGroups();
                
                var totalGroups = groups.Count;
                var totalActiveGroups = activeGroups.Count;
                var totalAssigned = manager.GetTotalAssignedEmployees();
                var totalCapacity = manager.GetTotalCapacity();
                var overallUtilization = totalCapacity > 0 ? (double)totalAssigned / totalCapacity * 100 : 0;

                var groupStatistics = groups.Select(GetGroupStatistics).ToList();

                return new OverallShiftGroupStatistics
                {
                    TotalGroups = totalGroups,
                    ActiveGroups = totalActiveGroups,
                    TotalAssigned = totalAssigned,
                    TotalCapacity = totalCapacity,
                    OverallUtilizationRate = overallUtilization,
                    GroupStatistics = groupStatistics
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating overall shift group statistics");
                return new OverallShiftGroupStatistics();
            }
        }

        /// <summary>
        /// Suggests optimal capacity based on employee count and historical data
        /// </summary>
        public CapacitySuggestion SuggestCapacity(int employeeCount, int currentMorningCapacity, int currentEveningCapacity)
        {
            try
            {
                // Simple algorithm: suggest 60% morning, 40% evening split
                var suggestedMorning = (int)Math.Ceiling(employeeCount * 0.6);
                var suggestedEvening = (int)Math.Ceiling(employeeCount * 0.4);

                // Ensure minimum capacity of 1
                suggestedMorning = Math.Max(1, suggestedMorning);
                suggestedEvening = Math.Max(1, suggestedEvening);

                return new CapacitySuggestion
                {
                    SuggestedMorningCapacity = suggestedMorning,
                    SuggestedEveningCapacity = suggestedEvening,
                    Reason = $"پیشنهاد بر اساس {employeeCount} کارمند: 60% صبح، 40% عصر"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suggesting capacity");
                return new CapacitySuggestion
                {
                    SuggestedMorningCapacity = currentMorningCapacity,
                    SuggestedEveningCapacity = currentEveningCapacity,
                    Reason = "خطا در محاسبه پیشنهاد"
                };
            }
        }
    }

    public class ShiftGroupStatistics
    {
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public int MorningAssigned { get; set; }
        public int MorningCapacity { get; set; }
        public int EveningAssigned { get; set; }
        public int EveningCapacity { get; set; }
        public int TotalAssigned { get; set; }
        public int TotalCapacity { get; set; }
        public double UtilizationRate { get; set; }
        public bool IsActive { get; set; }
    }

    public class OverallShiftGroupStatistics
    {
        public int TotalGroups { get; set; }
        public int ActiveGroups { get; set; }
        public int TotalAssigned { get; set; }
        public int TotalCapacity { get; set; }
        public double OverallUtilizationRate { get; set; }
        public List<ShiftGroupStatistics> GroupStatistics { get; set; } = new();
    }

    public class CapacitySuggestion
    {
        public int SuggestedMorningCapacity { get; set; }
        public int SuggestedEveningCapacity { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
