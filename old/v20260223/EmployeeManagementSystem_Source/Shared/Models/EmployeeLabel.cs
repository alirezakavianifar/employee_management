using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Shared.Models
{
    /// <summary>
    /// Represents a text label that can be attached to employees.
    /// Labels appear below the employee photo in the UI.
    /// </summary>
    public class EmployeeLabel
    {
        public string LabelId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public EmployeeLabel()
        {
            LabelId = Guid.NewGuid().ToString("N")[..8]; // Short unique ID
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public EmployeeLabel(string labelId, string text)
        {
            LabelId = labelId;
            Text = text;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        /// <summary>
        /// Creates a new label with auto-generated ID.
        /// </summary>
        public static EmployeeLabel Create(string text)
        {
            return new EmployeeLabel
            {
                LabelId = Guid.NewGuid().ToString("N")[..8],
                Text = text,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        /// <summary>
        /// Creates a copy of this label with a new ID (used when assigning to employees).
        /// </summary>
        public EmployeeLabel CreateCopy()
        {
            return new EmployeeLabel
            {
                LabelId = Guid.NewGuid().ToString("N")[..8],
                Text = this.Text,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        public void Update(string? text = null)
        {
            if (!string.IsNullOrEmpty(text))
                Text = text;
            
            UpdatedAt = DateTime.Now;
        }

        public override bool Equals(object? obj)
        {
            return obj is EmployeeLabel label && LabelId == label.LabelId;
        }

        public override int GetHashCode()
        {
            return LabelId.GetHashCode();
        }

        public override string ToString()
        {
            return $"EmployeeLabel({LabelId}: {Text})";
        }

        public static EmployeeLabel FromJson(string json)
        {
            return JsonConvert.DeserializeObject<EmployeeLabel>(json) ?? new EmployeeLabel();
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "label_id", LabelId },
                { "text", Text },
                { "created_at", CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) },
                { "updated_at", UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) }
            };
        }

        /// <summary>
        /// Creates an EmployeeLabel from a dictionary (used when loading from JSON).
        /// </summary>
        public static EmployeeLabel FromDictionary(Dictionary<string, object> dict)
        {
            var label = new EmployeeLabel();
            
            if (dict.TryGetValue("label_id", out var id))
                label.LabelId = id?.ToString() ?? label.LabelId;
            if (dict.TryGetValue("text", out var text))
                label.Text = text?.ToString() ?? string.Empty;
            if (dict.TryGetValue("created_at", out var createdAt) && DateTime.TryParse(createdAt?.ToString(), out var parsedCreated))
                label.CreatedAt = parsedCreated;
            if (dict.TryGetValue("updated_at", out var updatedAt) && DateTime.TryParse(updatedAt?.ToString(), out var parsedUpdated))
                label.UpdatedAt = parsedUpdated;
            
            return label;
        }
    }
}
