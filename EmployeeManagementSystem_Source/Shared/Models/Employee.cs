using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Shared.Utils;

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
        
        // Extended properties
        public string ShieldColor { get; set; } = "Blue";
        public bool ShowShield { get; set; } = true;
        public List<string> StickerPaths { get; set; } = new List<string>();
        public string MedalBadgePath { get; set; } = string.Empty;
        public string PersonnelId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool ShowPhone { get; set; } = true;
        
        // Employee labels (text labels displayed below photo)
        public List<EmployeeLabel> Labels { get; set; } = new List<EmployeeLabel>();

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

        /// <summary>
        /// Resolves a photo path (absolute from another machine or relative) to a local file path.
        /// Use this when loading photos from report/JSON data so images work on any system.
        /// </summary>
        public static string? ResolvePhotoPath(string? photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
                return null;

            if (File.Exists(photoPath))
                return photoPath;

            var fileName = Path.GetFileName(photoPath);
            if (string.IsNullOrEmpty(fileName))
                return null;

            // Prefer the app's configured Images directory (works when config points to correct SharedData)
            try
            {
                var config = AppConfigHelper.Config;
                if (!string.IsNullOrEmpty(config.ImagesDirectory))
                {
                    var configuredPath = Path.Combine(config.ImagesDirectory, "Staff", fileName);
                    if (File.Exists(configuredPath))
                        return configuredPath;
                }
            }
            catch { /* config not ready or wrong path on another PC */ }

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var sharedDataRoots = new[]
            {
                Path.Combine(baseDir, "SharedData"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "SharedData")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "SharedData")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "SharedData"))
            };

            foreach (var dataDir in sharedDataRoots)
            {
                var imagesPath = Path.Combine(dataDir, "Images", "Staff", fileName);
                if (File.Exists(imagesPath))
                    return imagesPath;
            }

            var pathTrimmed = photoPath.Trim();
            if (pathTrimmed.Length > 0 && pathTrimmed[0] != Path.DirectorySeparatorChar && pathTrimmed[0] != '/' && !Path.IsPathRooted(pathTrimmed))
            {
                var currentPath = Path.Combine(Directory.GetCurrentDirectory(), photoPath);
                if (File.Exists(currentPath))
                    return currentPath;
            }

            return null;
        }

        public bool HasPhoto()
        {
            if (string.IsNullOrEmpty(PhotoPath))
                return false;

            var resolved = ResolvePhotoPath(PhotoPath);
            if (resolved != null)
            {
                PhotoPath = resolved;
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

        public void Update(string? firstName = null, string? lastName = null, string? roleId = null, string? shiftGroupId = null, string? photoPath = null, bool? isManager = null, 
                          string? shieldColor = null, bool? showShield = null, List<string>? stickerPaths = null, string? medalBadgePath = null, string? personnelId = null,
                          List<EmployeeLabel>? labels = null, string? phone = null, bool? showPhone = null)
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
            if (!string.IsNullOrEmpty(shieldColor))
                ShieldColor = shieldColor;
            if (showShield.HasValue)
                ShowShield = showShield.Value;
            if (stickerPaths != null)
                StickerPaths = stickerPaths;
            if (medalBadgePath != null)
                MedalBadgePath = medalBadgePath;
            if (!string.IsNullOrEmpty(personnelId))
                PersonnelId = personnelId;
            if (labels != null)
                Labels = labels;
            if (phone != null)
                Phone = phone;
            if (showPhone.HasValue)
                ShowPhone = showPhone.Value;
            
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
                { "updated_at", UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) },
                { "shield_color", ShieldColor },
                { "show_shield", ShowShield },
                { "sticker_paths", StickerPaths ?? new List<string>() },
                { "medal_badge_path", MedalBadgePath },
                { "personnel_id", PersonnelId },
                { "labels", Labels?.Select(l => l.ToDictionary()).ToList() ?? new List<Dictionary<string, object>>() },
                { "phone", Phone },
                { "show_phone", ShowPhone }
            };
        }
    }
}
