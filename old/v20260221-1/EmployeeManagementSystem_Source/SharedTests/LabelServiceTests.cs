using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared.Models;
using Shared.Services;
using Xunit;

namespace SharedTests
{
    public class LabelServiceTests : IDisposable
    {
        private readonly string _testDir;
        private readonly LabelService _labelService;

        public LabelServiceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "LabelServiceTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
            _labelService = new LabelService(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                try 
                {
                    Directory.Delete(_testDir, true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        [Fact]
        public void CreateLabel_ShouldAddLabelToArchive()
        {
            // Act
            var label = _labelService.CreateLabel("Test Label");

            // Assert
            Assert.NotNull(label);
            Assert.Equal("Test Label", label.Text);
            
            var allLabels = _labelService.GetAllLabels();
            Assert.Single(allLabels);
            Assert.Equal(label.LabelId, allLabels[0].LabelId);
        }

        [Fact]
        public void LoadLabelArchive_ShouldLoadSavedLabels()
        {
            // Arrange
            var label1 = _labelService.CreateLabel("Label 1");
            var label2 = _labelService.CreateLabel("Label 2");

            // Act
            var newService = new LabelService(_testDir);
            var loadedLabels = newService.LoadLabelArchive();

            // Assert
            Assert.Equal(2, loadedLabels.Count);
            Assert.Contains(loadedLabels.Values, l => l.Text == "Label 1");
            Assert.Contains(loadedLabels.Values, l => l.Text == "Label 2");
        }

        [Fact]
        public void AssignLabelToEmployee_ShouldAddLabelCopy()
        {
            // Arrange
            var label = _labelService.CreateLabel("Test Label");
            var employee = new Employee { EmployeeId = "emp1", FirstName = "John" };

            // Act
            var result = _labelService.AssignLabelToEmployee(employee, label.LabelId);

            // Assert
            Assert.True(result);
            Assert.Single(employee.Labels);
            Assert.Equal("Test Label", employee.Labels[0].Text);
            // EmployeeLabel.CreateCopy generates NEW ID.
            Assert.NotEqual(label.LabelId, employee.Labels[0].LabelId);
        }

        [Fact]
        public void RemoveLabelFromEmployee_ShouldRemoveLabel()
        {
            // Arrange
            var label = _labelService.CreateLabel("Test Label");
            var employee = new Employee { EmployeeId = "emp1", FirstName = "John" };
            _labelService.AssignLabelToEmployee(employee, label.LabelId);
            var assignedLabelId = employee.Labels[0].LabelId;

            // Act
            var result = _labelService.RemoveLabelFromEmployee(employee, assignedLabelId);

            // Assert
            Assert.True(result);
            Assert.Empty(employee.Labels);
        }

        [Fact]
        public void UpdateLabel_ShouldUpdateText()
        {
            // Arrange
            var label = _labelService.CreateLabel("Old Name");

            // Act
            _labelService.UpdateLabel(label.LabelId, "New Name");

            // Assert
            var updatedLabel = _labelService.GetLabel(label.LabelId);
            Assert.Equal("New Name", updatedLabel?.Text);
        }

        [Fact]
        public void DeleteLabelFromArchive_ShouldRemoveFromArchive()
        {
            // Arrange
            var label = _labelService.CreateLabel("To Delete");

            // Act
            var result = _labelService.DeleteLabelFromArchive(label.LabelId);

            // Assert
            Assert.True(result);
            Assert.Empty(_labelService.GetAllLabels());
        }
    }
}
