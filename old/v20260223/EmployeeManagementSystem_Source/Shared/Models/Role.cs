using System;
using Newtonsoft.Json;

namespace Shared.Models
{
    public class Role
    {
        public string RoleId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = "#4CAF50"; // Default green color
        public int Priority { get; set; } = 0; // Higher number = higher priority
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Role()
        {
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public Role(string roleId, string name, string description = "", string color = "#4CAF50", int priority = 0)
        {
            RoleId = roleId;
            Name = name;
            Description = description;
            Color = color;
            Priority = priority;
            IsActive = true;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public void Update(string? name = null, string? description = null, string? color = null, int? priority = null, bool? isActive = null)
        {
            if (!string.IsNullOrEmpty(name))
                Name = name;
            if (!string.IsNullOrEmpty(description))
                Description = description;
            if (!string.IsNullOrEmpty(color))
                Color = color;
            if (priority.HasValue)
                Priority = priority.Value;
            if (isActive.HasValue)
                IsActive = isActive.Value;
            
            UpdatedAt = DateTime.Now;
        }

        public override bool Equals(object? obj)
        {
            return obj is Role role && RoleId == role.RoleId;
        }

        public override int GetHashCode()
        {
            return RoleId.GetHashCode();
        }

        public override string ToString()
        {
            return $"Role({RoleId}: {Name})";
        }

        public static Role FromJson(string json)
        {
            return JsonConvert.DeserializeObject<Role>(json) ?? new Role();
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "role_id", RoleId },
                { "name", Name },
                { "description", Description },
                { "color", Color },
                { "priority", Priority },
                { "is_active", IsActive },
                { "created_at", CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) },
                { "updated_at", UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) }
            };
        }
    }
}
