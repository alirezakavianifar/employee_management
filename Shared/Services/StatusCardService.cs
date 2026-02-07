using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared.Models;

namespace Shared.Services
{
    /// <summary>
    /// Service for managing status cards - CRUD operations and persistence.
    /// Status cards are stored in Data/status_cards.json
    /// </summary>
    public class StatusCardService
    {
        private readonly string _dataDir;
        private readonly string _statusCardsFilePath;
        private readonly ILogger<StatusCardService> _logger;
        private Dictionary<string, StatusCard> _statusCards = new();

        public StatusCardService(string dataDir = "Data")
        {
            _dataDir = dataDir;
            _statusCardsFilePath = Path.Combine(dataDir, "status_cards.json");
            _logger = LoggingService.CreateLogger<StatusCardService>();
            
            EnsureDataDirectoryExists();
        }

        private void EnsureDataDirectoryExists()
        {
            if (!Directory.Exists(_dataDir))
            {
                Directory.CreateDirectory(_dataDir);
            }
        }

        /// <summary>
        /// Loads status cards from the JSON file. Creates default cards if file doesn't exist.
        /// </summary>
        public Dictionary<string, StatusCard> LoadStatusCards()
        {
            try
            {
                if (File.Exists(_statusCardsFilePath))
                {
                    var json = File.ReadAllText(_statusCardsFilePath);
                    var cardsList = JsonConvert.DeserializeObject<List<StatusCard>>(json);
                    
                    if (cardsList != null)
                    {
                        _statusCards = cardsList.ToDictionary(c => c.StatusCardId, c => c);
                        _logger.LogInformation("Loaded {Count} status cards from {Path}", _statusCards.Count, _statusCardsFilePath);
                        return _statusCards;
                    }
                }
                
                // File doesn't exist or is empty - create default cards
                _logger.LogInformation("No status cards file found, creating defaults");
                EnsureDefaultStatusCards();
                return _statusCards;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading status cards from {Path}", _statusCardsFilePath);
                EnsureDefaultStatusCards();
                return _statusCards;
            }
        }

        /// <summary>
        /// Saves all status cards to the JSON file.
        /// </summary>
        public void SaveStatusCards()
        {
            try
            {
                EnsureDataDirectoryExists();
                
                var cardsList = _statusCards.Values.ToList();
                var json = JsonConvert.SerializeObject(cardsList, Formatting.Indented);
                File.WriteAllText(_statusCardsFilePath, json);
                
                _logger.LogInformation("Saved {Count} status cards to {Path}", cardsList.Count, _statusCardsFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving status cards to {Path}", _statusCardsFilePath);
                throw;
            }
        }

        /// <summary>
        /// Gets a status card by ID.
        /// </summary>
        public StatusCard? GetStatusCard(string statusCardId)
        {
            return _statusCards.TryGetValue(statusCardId, out var card) ? card : null;
        }

        /// <summary>
        /// Gets all status cards.
        /// </summary>
        public List<StatusCard> GetAllStatusCards()
        {
            return _statusCards.Values.ToList();
        }

        /// <summary>
        /// Gets only active status cards.
        /// </summary>
        public List<StatusCard> GetActiveStatusCards()
        {
            return _statusCards.Values.Where(c => c.IsActive).ToList();
        }

        /// <summary>
        /// Adds a new status card.
        /// </summary>
        public bool AddStatusCard(StatusCard statusCard)
        {
            if (string.IsNullOrEmpty(statusCard.StatusCardId))
            {
                _logger.LogWarning("Cannot add status card with empty ID");
                return false;
            }

            if (_statusCards.ContainsKey(statusCard.StatusCardId))
            {
                _logger.LogWarning("Status card with ID {Id} already exists", statusCard.StatusCardId);
                return false;
            }

            _statusCards[statusCard.StatusCardId] = statusCard;
            SaveStatusCards();
            _logger.LogInformation("Added status card: {Card}", statusCard);
            return true;
        }

        /// <summary>
        /// Adds a new status card with specified parameters.
        /// </summary>
        public bool AddStatusCard(string statusCardId, string name, string color = "#FF5722", string textColor = "#FFFFFF")
        {
            var card = new StatusCard(statusCardId, name, color, textColor);
            return AddStatusCard(card);
        }

        /// <summary>
        /// Updates an existing status card.
        /// </summary>
        public bool UpdateStatusCard(string statusCardId, string? name = null, string? color = null, string? textColor = null, bool? isActive = null)
        {
            if (!_statusCards.TryGetValue(statusCardId, out var card))
            {
                _logger.LogWarning("Cannot update: Status card with ID {Id} not found", statusCardId);
                return false;
            }

            card.Update(name, color, textColor, isActive);
            SaveStatusCards();
            _logger.LogInformation("Updated status card: {Card}", card);
            return true;
        }

        /// <summary>
        /// Deletes a status card by ID.
        /// </summary>
        public bool DeleteStatusCard(string statusCardId)
        {
            if (!_statusCards.ContainsKey(statusCardId))
            {
                _logger.LogWarning("Cannot delete: Status card with ID {Id} not found", statusCardId);
                return false;
            }

            _statusCards.Remove(statusCardId);
            SaveStatusCards();
            _logger.LogInformation("Deleted status card with ID: {Id}", statusCardId);
            return true;
        }

        /// <summary>
        /// Creates default status cards if none exist.
        /// Called on first load or when status_cards.json is missing.
        /// </summary>
        public void EnsureDefaultStatusCards()
        {
            if (_statusCards.Count > 0)
                return;

            var defaults = new List<StatusCard>
            {
                new StatusCard("out_of_order", "Out of Order", "#F44336", "#FFFFFF"),  // Red
                new StatusCard("empty", "Empty", "#9E9E9E", "#FFFFFF"),                  // Gray
                new StatusCard("available", "Available", "#4CAF50", "#FFFFFF")          // Green
            };

            foreach (var card in defaults)
            {
                _statusCards[card.StatusCardId] = card;
            }

            SaveStatusCards();
            _logger.LogInformation("Created {Count} default status cards", defaults.Count);
        }

        /// <summary>
        /// Sets the status cards dictionary directly (used when loading from main data).
        /// </summary>
        public void SetStatusCards(Dictionary<string, StatusCard> statusCards)
        {
            _statusCards = statusCards ?? new Dictionary<string, StatusCard>();
        }

        /// <summary>
        /// Gets the status cards dictionary (used for MainController integration).
        /// </summary>
        public Dictionary<string, StatusCard> GetStatusCardsDictionary()
        {
            return _statusCards;
        }
    }
}
