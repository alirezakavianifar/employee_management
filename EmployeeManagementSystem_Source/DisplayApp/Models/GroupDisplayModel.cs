using System.Collections.Generic;

namespace DisplayApp.Models
{
    public class GroupDisplayModel
    {
        public string GroupName { get; set; } = "";
        public string GroupDescription { get; set; } = "";
        public List<EmployeeDisplayModel> MorningShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        public List<EmployeeDisplayModel> EveningShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
    }

    public class CombinedDisplayModel
    {
        public List<EmployeeDisplayModel> AllMorningShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        public List<EmployeeDisplayModel> AllEveningShiftEmployees { get; set; } = new List<EmployeeDisplayModel>();
        public int TotalGroups { get; set; } = 0;
        public string DisplayTitle { get; set; } = "همه کارکنان";
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
    }
}
