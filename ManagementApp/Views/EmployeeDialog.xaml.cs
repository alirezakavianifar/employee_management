using System;
using System.Windows;

namespace ManagementApp.Views
{
    public partial class EmployeeDialog : Window
    {
        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public string Role { get; private set; } = string.Empty;
        public string PhotoPath { get; private set; } = string.Empty;

        public EmployeeDialog()
        {
            InitializeComponent();
        }

        public EmployeeDialog(Shared.Models.Employee employee) : this()
        {
            FirstNameTextBox.Text = employee.FirstName;
            LastNameTextBox.Text = employee.LastName;
            RoleTextBox.Text = employee.Role;
            PhotoPath = employee.PhotoPath;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FirstNameTextBox.Text) || string.IsNullOrWhiteSpace(LastNameTextBox.Text))
            {
                MessageBox.Show("لطفاً نام و نام خانوادگی را وارد کنید", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FirstName = FirstNameTextBox.Text.Trim();
            LastName = LastNameTextBox.Text.Trim();
            Role = RoleTextBox.Text.Trim();
            
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
