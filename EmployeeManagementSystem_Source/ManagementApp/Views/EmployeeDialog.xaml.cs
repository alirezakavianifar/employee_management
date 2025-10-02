using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Shared.Models;
using ManagementApp.Controllers;

namespace ManagementApp.Views
{
    public partial class EmployeeDialog : Window
    {
        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public string RoleId { get; private set; } = "employee";
        public string ShiftGroupId { get; private set; } = "default";
        public string PhotoPath { get; private set; } = string.Empty;
        public bool IsManager { get; private set; } = false;

        // Backward compatibility
        public string Role => RoleId;

        private readonly MainController? _controller;

        public EmployeeDialog(MainController? controller = null)
        {
            _controller = controller;
            InitializeComponent();
            LoadRoles();
            LoadShiftGroups();
        }

        public EmployeeDialog(Shared.Models.Employee employee, MainController? controller = null) : this(controller)
        {
            FirstNameTextBox.Text = employee.FirstName;
            LastNameTextBox.Text = employee.LastName;
            PhotoPath = employee.PhotoPath;
            IsManagerCheckBox.IsChecked = employee.IsManager;
            
            // Set the selected role
            foreach (ComboBoxItem item in RoleComboBox.Items)
            {
                if (item.Tag?.ToString() == employee.RoleId)
                {
                    RoleComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Set the selected shift group
            foreach (ComboBoxItem item in ShiftGroupComboBox.Items)
            {
                if (item.Tag?.ToString() == employee.ShiftGroupId)
                {
                    ShiftGroupComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void LoadRoles()
        {
            try
            {
                var controller = _controller ?? GetController();
                if (controller != null)
                {
                    var roles = controller.GetActiveRoles();
                    RoleComboBox.Items.Clear();
                    
                    foreach (var role in roles)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = role.Name,
                            Tag = role.RoleId,
                            ToolTip = role.Description
                        };
                        RoleComboBox.Items.Add(item);
                    }
                    
                    // Select default role if available
                    if (RoleComboBox.Items.Count > 0)
                    {
                        var defaultRole = RoleComboBox.Items.Cast<ComboBoxItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == "employee");
                        RoleComboBox.SelectedItem = defaultRole ?? RoleComboBox.Items[0];
                    }
                }
                else
                {
                    MessageBox.Show("خطا در دسترسی به کنترلر.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در بارگذاری نقش‌ها: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadShiftGroups()
        {
            try
            {
                var controller = _controller ?? GetController();
                if (controller != null)
                {
                    var shiftGroups = controller.GetActiveShiftGroups();
                    ShiftGroupComboBox.Items.Clear();
                    
                    foreach (var group in shiftGroups)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = group.Name,
                            Tag = group.GroupId,
                            ToolTip = group.Description
                        };
                        ShiftGroupComboBox.Items.Add(item);
                    }
                    
                    // Select default group if available
                    if (ShiftGroupComboBox.Items.Count > 0)
                    {
                        var defaultGroup = ShiftGroupComboBox.Items.Cast<ComboBoxItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == "default");
                        ShiftGroupComboBox.SelectedItem = defaultGroup ?? ShiftGroupComboBox.Items[0];
                    }
                }
                else
                {
                    MessageBox.Show("خطا در دسترسی به کنترلر.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در بارگذاری گروه‌های شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private MainController? GetController()
        {
            try
            {
                // Try to get controller from static instance
                if (MainWindow.Instance?.Controller != null)
                {
                    return MainWindow.Instance.Controller;
                }

                // Fallback: try Application.Current.MainWindow
                var mainWindow = Application.Current?.MainWindow as MainWindow;
                if (mainWindow?.Controller != null)
                {
                    return mainWindow.Controller;
                }

                // Last resort: create a new controller
                return new MainController();
            }
            catch
            {
                return new MainController();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FirstNameTextBox.Text) || string.IsNullOrWhiteSpace(LastNameTextBox.Text))
            {
                MessageBox.Show("لطفاً نام و نام خانوادگی را وارد کنید", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (RoleComboBox.SelectedItem == null)
            {
                MessageBox.Show("لطفاً یک نقش انتخاب کنید", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ShiftGroupComboBox.SelectedItem == null)
            {
                MessageBox.Show("لطفاً یک گروه شیفت انتخاب کنید", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FirstName = FirstNameTextBox.Text.Trim();
            LastName = LastNameTextBox.Text.Trim();
            RoleId = (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "employee";
            ShiftGroupId = (ShiftGroupComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default";
            IsManager = IsManagerCheckBox.IsChecked ?? false;
            
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
