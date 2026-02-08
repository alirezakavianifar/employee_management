using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Shared.Utils;

namespace SharedTests
{
    /// <summary>
    /// Unit tests for CustomOverrideManager to verify text override functionality.
    /// </summary>
    public class CustomOverrideManagerTests : IDisposable
    {
        public CustomOverrideManagerTests()
        {
            // Clear all overrides before each test
            CustomOverrideManager.ClearAllOverrides();
        }

        public void Dispose()
        {
            // Clean up after tests
            CustomOverrideManager.ClearAllOverrides();
        }

        [Fact]
        public void SetOverride_StoresValue()
        {
            // Arrange
            const string key = "test_key";
            const string value = "custom_value";

            // Act
            CustomOverrideManager.SetOverride(key, value);

            // Assert
            Assert.Equal(value, CustomOverrideManager.GetOverride(key));
        }

        [Fact]
        public void GetOverride_ReturnsNull_WhenKeyNotSet()
        {
            // Act
            var result = CustomOverrideManager.GetOverride("nonexistent_key");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void RemoveOverride_RemovesExistingKey()
        {
            // Arrange
            const string key = "test_key";
            CustomOverrideManager.SetOverride(key, "test_value");

            // Act
            var removed = CustomOverrideManager.RemoveOverride(key);

            // Assert
            Assert.True(removed);
            Assert.Null(CustomOverrideManager.GetOverride(key));
        }

        [Fact]
        public void RemoveOverride_ReturnsFalse_WhenKeyNotExists()
        {
            // Act
            var removed = CustomOverrideManager.RemoveOverride("nonexistent_key");

            // Assert
            Assert.False(removed);
        }

        [Fact]
        public void ClearAllOverrides_RemovesAll()
        {
            // Arrange
            CustomOverrideManager.SetOverride("key1", "value1");
            CustomOverrideManager.SetOverride("key2", "value2");
            CustomOverrideManager.SetOverride("key3", "value3");

            // Act
            CustomOverrideManager.ClearAllOverrides();

            // Assert
            Assert.Equal(0, CustomOverrideManager.OverrideCount);
        }

        [Fact]
        public void GetAllOverrides_ReturnsCopyOfDictionary()
        {
            // Arrange
            CustomOverrideManager.SetOverride("key1", "value1");
            CustomOverrideManager.SetOverride("key2", "value2");

            // Act
            var overrides = CustomOverrideManager.GetAllOverrides();
            overrides["key1"] = "modified"; // Modify the returned copy

            // Assert - original should be unchanged
            Assert.Equal("value1", CustomOverrideManager.GetOverride("key1"));
        }

        [Fact]
        public void HasOverride_ReturnsCorrectly()
        {
            // Arrange
            CustomOverrideManager.SetOverride("existing_key", "value");

            // Assert
            Assert.True(CustomOverrideManager.HasOverride("existing_key"));
            Assert.False(CustomOverrideManager.HasOverride("missing_key"));
        }

        [Fact]
        public void ApplySupervisorToForemanPreset_SetsMultipleOverrides()
        {
            // Act
            CustomOverrideManager.ApplySupervisorToForemanPreset();

            // Assert
            Assert.Equal("Foreman: {0}", CustomOverrideManager.GetOverride("display_supervisor"));
            Assert.Equal("No Foreman", CustomOverrideManager.GetOverride("display_no_supervisor"));
            Assert.True(CustomOverrideManager.OverrideCount >= 8);
        }

        [Fact]
        public void SaveAndLoad_PersistsOverrides()
        {
            // Arrange
            var tempPath = Path.GetTempFileName();
            try
            {
                CustomOverrideManager.SetOverride("display_supervisor", "Foreman: {0}");
                CustomOverrideManager.SetOverride("test_key", "test_value");

                // Act - Save
                CustomOverrideManager.SaveOverrides(tempPath);

                // Clear and reload
                CustomOverrideManager.ClearAllOverrides();
                Assert.Equal(0, CustomOverrideManager.OverrideCount);

                CustomOverrideManager.LoadOverrides(tempPath);

                // Assert
                Assert.Equal("Foreman: {0}", CustomOverrideManager.GetOverride("display_supervisor"));
                Assert.Equal("test_value", CustomOverrideManager.GetOverride("test_key"));
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void ResourceManager_ReturnsOverride_WhenSet()
        {
            // Arrange - Load base resources first
            var path = GetResourcesPath();
            if (!string.IsNullOrEmpty(path))
            {
                ResourceManager.LoadResources(path);
            }

            // Set override
            CustomOverrideManager.SetOverride("display_supervisor", "Foreman: {0}");

            // Act
            var result = ResourceManager.GetString("display_supervisor");

            // Assert
            Assert.Equal("Foreman: {0}", result);
        }

        [Fact]
        public void ResourceManager_ReturnsFallback_WhenOverrideCleared()
        {
            // Arrange - Load base resources first
            var path = GetResourcesPath();
            if (string.IsNullOrEmpty(path))
            {
                return; // Skip if resources not found
            }

            ResourceManager.LoadResources(path);
            CustomOverrideManager.SetOverride("display_supervisor", "Foreman: {0}");

            // Act - Clear override
            CustomOverrideManager.RemoveOverride("display_supervisor");
            var result = ResourceManager.GetString("display_supervisor");

            // Assert - Should return base value (contains "{0}" for format)
            Assert.Contains("{0}", result);
            Assert.DoesNotContain("Foreman", result);
        }

        private string? GetResourcesPath()
        {
            // Go up from test output to find SharedData
            var current = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10; i++)
            {
                var sharedData = Path.Combine(current, "SharedData", "resources.xml");
                if (File.Exists(sharedData))
                    return sharedData;
                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }
            return null;
        }
    }
}
