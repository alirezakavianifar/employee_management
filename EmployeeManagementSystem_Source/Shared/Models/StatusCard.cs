using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Shared.Models
{
    /// <summary>
    /// Represents a status card that can be assigned to shift cells instead of employees.
    /// Status cards indicate cell states like "Out of Order", "Empty", or "Available".
    /// A cell can have EITHER an employee OR a status card, not both.
    /// </summary>
    public class StatusCard
    {
        public string StatusCardId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FF5722";        // Background color (default: deep orange)
        public string TextColor { get; set; } = "#FFFFFF";    // Text color for contrast (default: white)
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public StatusCard()
        {
            StatusCardId = Guid.NewGuid().ToString("N")[..8]; // Short unique ID
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public StatusCard(string statusCardId, string name, string color = "#FF5722", string textColor = "#FFFFFF")
        {
            StatusCardId = statusCardId;
            Name = name;
            Color = color;
            TextColor = textColor;
            IsActive = true;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public void Update(string? name = null, string? color = null, string? textColor = null, bool? isActive = null)
        {
            if (!string.IsNullOrEmpty(name))
                Name = name;
            if (!string.IsNullOrEmpty(color))
                Color = color;
            if (!string.IsNullOrEmpty(textColor))
                TextColor = textColor;
            if (isActive.HasValue)
                IsActive = isActive.Value;
            
            UpdatedAt = DateTime.Now;
        }

        public override bool Equals(object? obj)
        {
            return obj is StatusCard card && StatusCardId == card.StatusCardId;
        }

        public override int GetHashCode()
        {
            return StatusCardId.GetHashCode();
        }

        public override string ToString()
        {
            return $"StatusCard({StatusCardId}: {Name})";
        }

        public static StatusCard FromJson(string json)
        {
            return JsonConvert.DeserializeObject<StatusCard>(json) ?? new StatusCard();
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "status_card_id", StatusCardId },
                { "name", Name },
                { "color", Color },
                { "text_color", TextColor },
                { "is_active", IsActive },
                { "created_at", CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) },
                { "updated_at", UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) }
            };
        }

        /// <summary>
        /// Creates a StatusCard from a dictionary (used when loading from JSON)
        /// </summary>
        public static StatusCard FromDictionary(Dictionary<string, object> dict)
        {
            var card = new StatusCard();
            
            if (dict.TryGetValue("status_card_id", out var id))
                card.StatusCardId = id?.ToString() ?? card.StatusCardId;
            if (dict.TryGetValue("name", out var name))
                card.Name = name?.ToString() ?? string.Empty;
            if (dict.TryGetValue("color", out var color))
                card.Color = color?.ToString() ?? "#FF5722";
            if (dict.TryGetValue("text_color", out var textColor))
                card.TextColor = textColor?.ToString() ?? "#FFFFFF";
            if (dict.TryGetValue("is_active", out var isActive))
                card.IsActive = isActive is bool b ? b : bool.TryParse(isActive?.ToString(), out var parsed) && parsed;
            if (dict.TryGetValue("created_at", out var createdAt) && DateTime.TryParse(createdAt?.ToString(), out var parsedCreated))
                card.CreatedAt = parsedCreated;
            if (dict.TryGetValue("updated_at", out var updatedAt) && DateTime.TryParse(updatedAt?.ToString(), out var parsedUpdated))
                card.UpdatedAt = parsedUpdated;
            
            return card;
        }
    }
}
