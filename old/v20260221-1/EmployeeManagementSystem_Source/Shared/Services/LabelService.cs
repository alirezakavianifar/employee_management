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
    /// Service for managing employee labels - CRUD operations and persistence.
    /// Labels are stored in Data/label_archive.json
    /// </summary>
    public class LabelService
    {
        private readonly string _dataDir;
        private readonly string _labelArchiveFilePath;
        private readonly ILogger<LabelService> _logger;
        private Dictionary<string, EmployeeLabel> _labelArchive = new();

        public LabelService(string dataDir = "Data")
        {
            _dataDir = dataDir;
            _labelArchiveFilePath = Path.Combine(dataDir, "label_archive.json");
            _logger = LoggingService.CreateLogger<LabelService>();
            
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
        /// Loads labels from the JSON archive file.
        /// </summary>
        public Dictionary<string, EmployeeLabel> LoadLabelArchive()
        {
            try
            {
                if (File.Exists(_labelArchiveFilePath))
                {
                    var json = File.ReadAllText(_labelArchiveFilePath);
                    var labelsList = JsonConvert.DeserializeObject<List<EmployeeLabel>>(json);
                    
                    if (labelsList != null)
                    {
                        _labelArchive = labelsList.ToDictionary(l => l.LabelId, l => l);
                        _logger.LogInformation("Loaded {Count} labels from {Path}", _labelArchive.Count, _labelArchiveFilePath);
                        return _labelArchive;
                    }
                }
                
                // File doesn't exist or is empty
                _logger.LogInformation("No label archive file found at {Path}", _labelArchiveFilePath);
                _labelArchive = new Dictionary<string, EmployeeLabel>();
                return _labelArchive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading label archive from {Path}", _labelArchiveFilePath);
                _labelArchive = new Dictionary<string, EmployeeLabel>();
                return _labelArchive;
            }
        }

        /// <summary>
        /// Saves all labels to the JSON archive file.
        /// </summary>
        public void SaveLabelArchive()
        {
            try
            {
                EnsureDataDirectoryExists();
                
                var labelsList = _labelArchive.Values.ToList();
                var json = JsonConvert.SerializeObject(labelsList, Formatting.Indented);
                File.WriteAllText(_labelArchiveFilePath, json);
                
                _logger.LogInformation("Saved {Count} labels to {Path}", labelsList.Count, _labelArchiveFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving label archive to {Path}", _labelArchiveFilePath);
                throw;
            }
        }

        /// <summary>
        /// Gets a label by ID from the archive.
        /// </summary>
        public EmployeeLabel? GetLabel(string labelId)
        {
            return _labelArchive.TryGetValue(labelId, out var label) ? label : null;
        }

        /// <summary>
        /// Gets all labels from the archive.
        /// </summary>
        public List<EmployeeLabel> GetAllLabels()
        {
            return _labelArchive.Values.ToList();
        }

        /// <summary>
        /// Creates a new label and adds it to the archive.
        /// </summary>
        public EmployeeLabel CreateLabel(string text)
        {
            var label = EmployeeLabel.Create(text);
            _labelArchive[label.LabelId] = label;
            SaveLabelArchive();
            _logger.LogInformation("Created label: {Label}", label);
            return label;
        }

        /// <summary>
        /// Adds an existing label to the archive.
        /// </summary>
        public bool AddLabel(EmployeeLabel label)
        {
            if (string.IsNullOrEmpty(label.LabelId))
            {
                _logger.LogWarning("Cannot add label with empty ID");
                return false;
            }

            if (_labelArchive.ContainsKey(label.LabelId))
            {
                _logger.LogWarning("Label with ID {Id} already exists", label.LabelId);
                return false;
            }

            _labelArchive[label.LabelId] = label;
            SaveLabelArchive();
            _logger.LogInformation("Added label: {Label}", label);
            return true;
        }

        /// <summary>
        /// Updates an existing label in the archive.
        /// </summary>
        public bool UpdateLabel(string labelId, string? text = null)
        {
            if (!_labelArchive.TryGetValue(labelId, out var label))
            {
                _logger.LogWarning("Cannot update: Label with ID {Id} not found", labelId);
                return false;
            }

            label.Update(text);
            SaveLabelArchive();
            _logger.LogInformation("Updated label: {Label}", label);
            return true;
        }

        /// <summary>
        /// Deletes a label from the archive permanently.
        /// Note: This does not remove the label from employees who already have it assigned.
        /// </summary>
        public bool DeleteLabelFromArchive(string labelId)
        {
            if (!_labelArchive.ContainsKey(labelId))
            {
                _logger.LogWarning("Cannot delete: Label with ID {Id} not found in archive", labelId);
                return false;
            }

            _labelArchive.Remove(labelId);
            SaveLabelArchive();
            _logger.LogInformation("Deleted label with ID: {Id} from archive", labelId);
            return true;
        }

        /// <summary>
        /// Assigns a copy of an archive label to an employee.
        /// </summary>
        public bool AssignLabelToEmployee(Employee employee, string archiveLabelId)
        {
            var archiveLabel = GetLabel(archiveLabelId);
            if (archiveLabel == null)
            {
                _logger.LogWarning("Cannot assign: Label with ID {Id} not found in archive", archiveLabelId);
                return false;
            }

            // Create a copy of the label for the employee
            var labelCopy = archiveLabel.CreateCopy();
            employee.Labels.Add(labelCopy);
            _logger.LogInformation("Assigned label '{Text}' to employee {EmployeeId}", archiveLabel.Text, employee.EmployeeId);
            return true;
        }

        /// <summary>
        /// Assigns a label with specific text to an employee (creates new instance).
        /// </summary>
        public EmployeeLabel AssignNewLabelToEmployee(Employee employee, string text)
        {
            var label = EmployeeLabel.Create(text);
            employee.Labels.Add(label);
            _logger.LogInformation("Assigned new label '{Text}' to employee {EmployeeId}", text, employee.EmployeeId);
            return label;
        }

        /// <summary>
        /// Removes a label from an employee by label ID.
        /// </summary>
        public bool RemoveLabelFromEmployee(Employee employee, string labelId)
        {
            var label = employee.Labels.FirstOrDefault(l => l.LabelId == labelId);
            if (label == null)
            {
                _logger.LogWarning("Cannot remove: Label with ID {Id} not found on employee {EmployeeId}", labelId, employee.EmployeeId);
                return false;
            }

            employee.Labels.Remove(label);
            _logger.LogInformation("Removed label '{Text}' from employee {EmployeeId}", label.Text, employee.EmployeeId);
            return true;
        }

        /// <summary>
        /// Clears all labels from an employee.
        /// </summary>
        public void ClearEmployeeLabels(Employee employee)
        {
            var count = employee.Labels.Count;
            employee.Labels.Clear();
            _logger.LogInformation("Cleared {Count} labels from employee {EmployeeId}", count, employee.EmployeeId);
        }

        /// <summary>
        /// Sets the label archive dictionary directly (used when loading from main data).
        /// </summary>
        public void SetLabelArchive(Dictionary<string, EmployeeLabel> labels)
        {
            _labelArchive = labels ?? new Dictionary<string, EmployeeLabel>();
        }

        /// <summary>
        /// Gets the label archive dictionary (used for MainController integration).
        /// </summary>
        public Dictionary<string, EmployeeLabel> GetLabelArchiveDictionary()
        {
            return _labelArchive;
        }
    }
}
