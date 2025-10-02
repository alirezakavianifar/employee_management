using Xunit;
using Shared.Models;

namespace SharedTests
{
    public class EmployeeTests
    {
        [Fact]
        public void Employee_DefaultConstructor_ShouldSetDefaultValues()
        {
            // Arrange & Act
            var employee = new Employee();

            // Assert
            Assert.NotNull(employee);
            Assert.Equal("employee", employee.RoleId);
            Assert.Equal("employee", employee.Role); // Backward compatibility
            Assert.False(employee.IsManager);
            Assert.NotNull(employee.EmployeeId);
            Assert.NotNull(employee.FirstName);
            Assert.NotNull(employee.LastName);
            Assert.NotNull(employee.PhotoPath);
        }

        [Fact]
        public void Employee_ParameterizedConstructor_ShouldSetValues()
        {
            // Arrange
            var employeeId = "emp_001";
            var firstName = "John";
            var lastName = "Doe";
            var roleId = "manager";
            var photoPath = "/path/to/photo.jpg";
            var isManager = true;

            // Act
            var employee = new Employee(employeeId, firstName, lastName, roleId, photoPath, isManager);

            // Assert
            Assert.Equal(employeeId, employee.EmployeeId);
            Assert.Equal(firstName, employee.FirstName);
            Assert.Equal(lastName, employee.LastName);
            Assert.Equal(roleId, employee.RoleId);
            Assert.Equal(roleId, employee.Role); // Backward compatibility
            Assert.Equal(photoPath, employee.PhotoPath);
            Assert.Equal(isManager, employee.IsManager);
        }

        [Fact]
        public void Employee_Update_ShouldUpdateSpecifiedFields()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "employee");
            var newFirstName = "Jane";
            var newLastName = "Smith";
            var newRoleId = "supervisor";
            var newPhotoPath = "/new/path/photo.jpg";
            var newIsManager = true;

            // Act
            employee.Update(newFirstName, newLastName, newRoleId, newPhotoPath, newIsManager);

            // Assert
            Assert.Equal(newFirstName, employee.FirstName);
            Assert.Equal(newLastName, employee.LastName);
            Assert.Equal(newRoleId, employee.RoleId);
            Assert.Equal(newRoleId, employee.Role); // Backward compatibility
            Assert.Equal(newPhotoPath, employee.PhotoPath);
            Assert.Equal(newIsManager, employee.IsManager);
        }

        [Fact]
        public void Employee_Update_WithNullValues_ShouldNotUpdate()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "employee");
            var originalFirstName = employee.FirstName;
            var originalLastName = employee.LastName;
            var originalRoleId = employee.RoleId;

            // Act
            employee.Update(null, null, null, null, null);

            // Assert
            Assert.Equal(originalFirstName, employee.FirstName);
            Assert.Equal(originalLastName, employee.LastName);
            Assert.Equal(originalRoleId, employee.RoleId);
        }

        [Fact]
        public void Employee_Update_WithEmptyStrings_ShouldNotUpdate()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "employee");
            var originalFirstName = employee.FirstName;
            var originalLastName = employee.LastName;
            var originalRoleId = employee.RoleId;

            // Act
            employee.Update("", "", "", "", null);

            // Assert
            Assert.Equal(originalFirstName, employee.FirstName);
            Assert.Equal(originalLastName, employee.LastName);
            Assert.Equal(originalRoleId, employee.RoleId);
        }

        [Fact]
        public void Employee_FullName_ShouldReturnConcatenatedName()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "employee");

            // Act
            var fullName = employee.FullName;

            // Assert
            Assert.Equal("John Doe", fullName);
        }

        [Fact]
        public void Employee_DisplayName_ShouldReturnFirstName()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "employee");

            // Act
            var displayName = employee.DisplayName;

            // Assert
            Assert.Equal("John", displayName);
        }

        [Fact]
        public void Employee_ToDictionary_ShouldIncludeAllFields()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "manager", "/path/photo.jpg", true);

            // Act
            var dictionary = employee.ToDictionary();

            // Assert
            Assert.NotNull(dictionary);
            Assert.Equal("emp_001", dictionary["employee_id"]);
            Assert.Equal("John", dictionary["first_name"]);
            Assert.Equal("Doe", dictionary["last_name"]);
            Assert.Equal("manager", dictionary["role"]); // Backward compatibility
            Assert.Equal("manager", dictionary["role_id"]);
            Assert.Equal("/path/photo.jpg", dictionary["photo_path"]);
            Assert.Equal(true, dictionary["is_manager"]);
            Assert.Contains("created_at", dictionary.Keys);
            Assert.Contains("updated_at", dictionary.Keys);
        }

        [Fact]
        public void Employee_ToJson_ShouldSerializeCorrectly()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "employee");

            // Act
            var json = employee.ToJson();

            // Assert
            Assert.NotNull(json);
            Assert.NotEmpty(json);
            Assert.Contains("emp_001", json);
            Assert.Contains("John", json);
            Assert.Contains("Doe", json);
            Assert.Contains("employee", json);
        }

        [Fact]
        public void Employee_FromJson_ShouldDeserializeCorrectly()
        {
            // Arrange
            var originalEmployee = new Employee("emp_001", "John", "Doe", "manager", "/path/photo.jpg", true);
            var json = originalEmployee.ToJson();

            // Act
            var deserializedEmployee = Employee.FromJson(json);

            // Assert
            Assert.NotNull(deserializedEmployee);
            Assert.Equal(originalEmployee.EmployeeId, deserializedEmployee.EmployeeId);
            Assert.Equal(originalEmployee.FirstName, deserializedEmployee.FirstName);
            Assert.Equal(originalEmployee.LastName, deserializedEmployee.LastName);
            Assert.Equal(originalEmployee.RoleId, deserializedEmployee.RoleId);
            Assert.Equal(originalEmployee.PhotoPath, deserializedEmployee.PhotoPath);
            Assert.Equal(originalEmployee.IsManager, deserializedEmployee.IsManager);
        }

        [Fact]
        public void Employee_Equals_ShouldCompareByEmployeeId()
        {
            // Arrange
            var employee1 = new Employee("emp_001", "John", "Doe", "employee");
            var employee2 = new Employee("emp_001", "Jane", "Smith", "manager");
            var employee3 = new Employee("emp_002", "John", "Doe", "employee");

            // Act & Assert
            Assert.Equal(employee1, employee2); // Same ID
            Assert.NotEqual(employee1, employee3); // Different ID
        }

        [Fact]
        public void Employee_GetHashCode_ShouldReturnEmployeeIdHashCode()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "employee");

            // Act
            var hashCode = employee.GetHashCode();

            // Assert
            Assert.Equal("emp_001".GetHashCode(), hashCode);
        }

        [Fact]
        public void Employee_ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var employee = new Employee("emp_001", "John", "Doe", "employee");

            // Act
            var stringRepresentation = employee.ToString();

            // Assert
            Assert.Contains("emp_001", stringRepresentation);
            Assert.Contains("John Doe", stringRepresentation);
        }
    }
}
