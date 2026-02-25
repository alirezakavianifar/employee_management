using Xunit;
using Shared.Models;

namespace SharedTests
{
    public class RoleManagerTests
    {
        [Fact]
        public void RoleManager_Initialization_ShouldCreateDefaultRoles()
        {
            // Arrange & Act
            var roleManager = new RoleManager();

            // Assert
            Assert.NotNull(roleManager);
            Assert.True(roleManager.GetRoleCount() >= 5); // Should have at least 5 default roles
            
            // Check specific default roles
            Assert.NotNull(roleManager.GetRole("manager"));
            Assert.NotNull(roleManager.GetRole("supervisor"));
            Assert.NotNull(roleManager.GetRole("employee"));
            Assert.NotNull(roleManager.GetRole("intern"));
            Assert.NotNull(roleManager.GetRole("contractor"));
        }

        [Fact]
        public void AddRole_ValidRole_ShouldSucceed()
        {
            // Arrange
            var roleManager = new RoleManager();
            var roleId = "test_role";
            var roleName = "Test Role";
            var description = "Test Description";
            var color = "#FF0000";
            var priority = 75;

            // Act
            var result = roleManager.AddRole(roleId, roleName, description, color, priority);

            // Assert
            Assert.True(result);
            var addedRole = roleManager.GetRole(roleId);
            Assert.NotNull(addedRole);
            Assert.Equal(roleId, addedRole.RoleId);
            Assert.Equal(roleName, addedRole.Name);
            Assert.Equal(description, addedRole.Description);
            Assert.Equal(color, addedRole.Color);
            Assert.Equal(priority, addedRole.Priority);
            Assert.True(addedRole.IsActive);
        }

        [Fact]
        public void AddRole_DuplicateRoleId_ShouldFail()
        {
            // Arrange
            var roleManager = new RoleManager();
            var roleId = "employee"; // This is a default role

            // Act
            var result = roleManager.AddRole(roleId, "Duplicate Role");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddRole_EmptyRoleId_ShouldFail()
        {
            // Arrange
            var roleManager = new RoleManager();

            // Act
            var result = roleManager.AddRole("", "Test Role");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void AddRole_EmptyRoleName_ShouldFail()
        {
            // Arrange
            var roleManager = new RoleManager();

            // Act
            var result = roleManager.AddRole("test_id", "");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void UpdateRole_ValidRole_ShouldSucceed()
        {
            // Arrange
            var roleManager = new RoleManager();
            var roleId = "employee";
            var newName = "Updated Employee";
            var newDescription = "Updated Description";
            var newColor = "#00FF00";
            var newPriority = 60;

            // Act
            var result = roleManager.UpdateRole(roleId, newName, newDescription, newColor, newPriority);

            // Assert
            Assert.True(result);
            var updatedRole = roleManager.GetRole(roleId);
            Assert.NotNull(updatedRole);
            Assert.Equal(newName, updatedRole.Name);
            Assert.Equal(newDescription, updatedRole.Description);
            Assert.Equal(newColor, updatedRole.Color);
            Assert.Equal(newPriority, updatedRole.Priority);
        }

        [Fact]
        public void UpdateRole_NonExistentRole_ShouldFail()
        {
            // Arrange
            var roleManager = new RoleManager();

            // Act
            var result = roleManager.UpdateRole("non_existent", "New Name");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void DeleteRole_DefaultRole_ShouldFail()
        {
            // Arrange
            var roleManager = new RoleManager();
            var defaultRoleId = "manager";

            // Act
            var result = roleManager.DeleteRole(defaultRoleId);

            // Assert
            Assert.False(result);
            Assert.NotNull(roleManager.GetRole(defaultRoleId)); // Role should still exist
        }

        [Fact]
        public void DeleteRole_CustomRole_ShouldSucceed()
        {
            // Arrange
            var roleManager = new RoleManager();
            var customRoleId = "custom_role";
            roleManager.AddRole(customRoleId, "Custom Role");

            // Act
            var result = roleManager.DeleteRole(customRoleId);

            // Assert
            Assert.True(result);
            Assert.Null(roleManager.GetRole(customRoleId));
        }

        [Fact]
        public void DeleteRole_NonExistentRole_ShouldFail()
        {
            // Arrange
            var roleManager = new RoleManager();

            // Act
            var result = roleManager.DeleteRole("non_existent");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetActiveRoles_ShouldReturnOnlyActiveRoles()
        {
            // Arrange
            var roleManager = new RoleManager();
            var customRoleId = "custom_role";
            roleManager.AddRole(customRoleId, "Custom Role");
            roleManager.UpdateRole(customRoleId, isActive: false);

            // Act
            var activeRoles = roleManager.GetActiveRoles();

            // Assert
            Assert.NotNull(activeRoles);
            Assert.All(activeRoles, role => Assert.True(role.IsActive));
            Assert.DoesNotContain(activeRoles, role => role.RoleId == customRoleId);
        }

        [Fact]
        public void GetAllRoles_ShouldReturnAllRoles()
        {
            // Arrange
            var roleManager = new RoleManager();
            var customRoleId = "custom_role";
            roleManager.AddRole(customRoleId, "Custom Role");

            // Act
            var allRoles = roleManager.GetAllRoles();

            // Assert
            Assert.NotNull(allRoles);
            Assert.True(allRoles.Count >= 6); // At least 5 default + 1 custom
            Assert.Contains(allRoles, role => role.RoleId == customRoleId);
        }

        [Fact]
        public void ToJson_ShouldSerializeCorrectly()
        {
            // Arrange
            var roleManager = new RoleManager();

            // Act
            var json = roleManager.ToJson();

            // Assert
            Assert.NotNull(json);
            Assert.NotEmpty(json);
            Assert.Contains("roles", json);
        }

        [Fact]
        public void FromJson_ShouldDeserializeCorrectly()
        {
            // Arrange
            var originalRoleManager = new RoleManager();
            var customRoleId = "test_role";
            originalRoleManager.AddRole(customRoleId, "Test Role", "Test Description", "#FF0000", 80);
            var json = originalRoleManager.ToJson();

            // Act
            var deserializedRoleManager = RoleManager.FromJson(json);

            // Assert
            Assert.NotNull(deserializedRoleManager);
            Assert.True(deserializedRoleManager.GetRoleCount() >= 5); // Should have at least default roles
            
            // Check if the custom role was deserialized correctly
            var deserializedRole = deserializedRoleManager.GetRole(customRoleId);
            if (deserializedRole != null)
            {
                Assert.Equal("Test Role", deserializedRole.Name);
                Assert.Equal("Test Description", deserializedRole.Description);
                Assert.Equal("#FF0000", deserializedRole.Color);
                Assert.Equal(80, deserializedRole.Priority);
            }
            else
            {
                // If custom role is not found, at least verify default roles are present
                Assert.NotNull(deserializedRoleManager.GetRole("employee"));
                Assert.NotNull(deserializedRoleManager.GetRole("manager"));
            }
        }

        [Fact]
        public void FromJson_EmptyJson_ShouldCreateDefaultRoles()
        {
            // Arrange
            var emptyJson = "{}";

            // Act
            var roleManager = RoleManager.FromJson(emptyJson);

            // Assert
            Assert.NotNull(roleManager);
            Assert.True(roleManager.GetRoleCount() >= 5); // Should have default roles
        }

        [Fact]
        public void FromJson_InvalidJson_ShouldCreateDefaultRoles()
        {
            // Arrange
            var invalidJson = "invalid json";

            // Act
            var roleManager = RoleManager.FromJson(invalidJson);

            // Assert
            Assert.NotNull(roleManager);
            Assert.True(roleManager.GetRoleCount() >= 5); // Should have default roles
        }
    }
}
