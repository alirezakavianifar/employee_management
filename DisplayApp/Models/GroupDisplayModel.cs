using System.Collections.Generic;

namespace DisplayApp.Models
{
    public class GroupDisplayModel
    {
        public string GroupName { get; set; } = "";
        public string GroupDescription { get; set; } = "";
        public string Color { get; set; } = "#4CAF50"; // Default green color
        public string SupervisorId { get; set; } = "";
        public string SupervisorName { get; set; } = "";
        public string SupervisorPhotoPath { get; set; } = "";
        public string MorningForemanName { get; set; } = "";
        public string AfternoonForemanName { get; set; } = "";
        public string NightForemanName { get; set; } = "";
        public List<EmployeeDisplayModel> MorningShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        public List<EmployeeDisplayModel> AfternoonShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        public List<EmployeeDisplayModel> NightShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        // Status cards assigned to shift slots (Phase 2)
        public List<StatusCardDisplayModel> MorningShiftStatusCards { get; set; } = new List<StatusCardDisplayModel>();
        public List<StatusCardDisplayModel> AfternoonShiftStatusCards { get; set; } = new List<StatusCardDisplayModel>();
        public List<StatusCardDisplayModel> NightShiftStatusCards { get; set; } = new List<StatusCardDisplayModel>();
    }

    public class CombinedDisplayModel
    {
        public List<EmployeeDisplayModel> AllMorningShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        public List<EmployeeDisplayModel> AllAfternoonShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        public List<EmployeeDisplayModel> AllNightShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        public int TotalGroups { get; set; } = 0;
        public string DisplayTitle { get; set; } = "All Staff";
    }

    public class EmployeeDisplayModel
    {
        public string EmployeeId { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName => $"{FirstName} {LastName}".Trim();
        public string PhotoPath { get; set; } = "";
        public string Role { get; set; } = "";
        public string GroupName { get; set; } = ""; // Add group name to identify which group the employee belongs to
        public string ShieldColor { get; set; } = "Blue";
        public bool ShowShield { get; set; } = true;
        public List<string> StickerPaths { get; set; } = new List<string>();
        public string MedalBadgePath { get; set; } = "";
        public string PersonnelId { get; set; } = "";
        public string Phone { get; set; } = "";
        public bool ShowPhone { get; set; } = true;
        public List<Shared.Models.EmployeeLabel> Labels { get; set; } = new List<Shared.Models.EmployeeLabel>();
    }

    /// <summary>
    /// Display model for status cards that can be assigned to shift slots.
    /// </summary>
    public class StatusCardDisplayModel
    {
        public string StatusCardId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#FF5722"; // Default orange
        public string TextColor { get; set; } = "#FFFFFF"; // Default white
        public int SlotIndex { get; set; } = -1; // Which slot this card is assigned to
    }
}

