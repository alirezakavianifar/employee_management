using System;
using System.IO;
using Newtonsoft.Json;

namespace Shared.Models
{
    public class Employee
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string RoleId { get; set; } = "employee"; // Default to employee role
        public string ShiftGroupId { get; set; } = "default"; // Default to default group
        public string PhotoPath { get; set; } = string.Empty;
        public bool IsManager { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Backward compatibility property
        [JsonIgnore]
        public string Role => RoleId;

        public Employee()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public Employee(string employeeId, string firstName, string lastName, string roleId = "employee", string shiftGroupId = "default", string photoPath = "", bool isManager = false)
        {
            EmployeeId = employeeId;
            FirstName = firstName;
            LastName = lastName;
            RoleId = roleId;
            ShiftGroupId = shiftGroupId;
            PhotoPath = photoPath;
            IsManager = isManager;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        [JsonIgnore]
        public string FullName => $"{FirstName} {LastName}".Trim();

        [JsonIgnore]
        public string DisplayName => FirstName;

        public bool HasPhoto()
        {
            if (string.IsNullOrEmpty(PhotoPath))
                return false;

            // Try the path as-is first
            if (File.Exists(PhotoPath))
                return true;

            // Try relative to data directory
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SharedData");
            var fullPath = Path.Combine(dataDir, "Images", "Staff", Path.GetFileName(PhotoPath));
            
            if (File.Exists(fullPath))
            {
                PhotoPath = fullPath;
                return true;
            }

            // Try current directory
            var currentPath = Path.Combine(Directory.GetCurrentDirectory(), PhotoPath);
            if (File.Exists(currentPath))
            {
                PhotoPath = currentPath;
                return true;
            }

            return false;
        }

        public string GetPhotoPath()
        {
            if (HasPhoto())
            {
                return PhotoPath;
            }
            return string.Empty;
        }

        public void Update(string? firstName = null, string? lastName = null, string? roleId = null, string? shiftGroupId = null, string? photoPath = null, bool? isManager = null)
        {
            if (!string.IsNullOrEmpty(firstName))
                FirstName = firstName;
            if (!string.IsNullOrEmpty(lastName))
                LastName = lastName;
            if (!string.IsNullOrEmpty(roleId))
                RoleId = roleId;
            if (!string.IsNullOrEmpty(shiftGroupId))
                ShiftGroupId = shiftGroupId;
            if (!string.IsNullOrEmpty(photoPath))
                PhotoPath = photoPath;
            if (isManager.HasValue)
                IsManager = isManager.Value;
            
            UpdatedAt = DateTime.Now;
        }

        public override bool Equals(object? obj)
        {
            return obj is Employee employee && EmployeeId == employee.EmployeeId;
        }

        public override int GetHashCode()
        {
            return EmployeeId.GetHashCode();
        }

        public override string ToString()
        {
            return $"Employee({EmployeeId}: {FullName})";
        }

        public static Employee FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Employee>(json) ?? new Employee();
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "employee_id", EmployeeId },
                { "first_name", FirstName },
                { "last_name", LastName },
                { "role", RoleId }, // Keep backward compatibility
                { "role_id", RoleId },
                { "shift_group_id", ShiftGroupId },
                { "photo_path", PhotoPath },
                { "is_manager", IsManager },
                { "created_at", CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) },
                { "updated_at", UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) }
            };
        }
    }
}
