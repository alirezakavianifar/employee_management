using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Shared.Models;
using ManagementApp.Controllers;

namespace ManagementApp.Views
{
    public partial class RoleDialog : Window
    {
        public List<Role> SelectedRoles { get; private set; } = new();
        private List<Role> _allRoles = new();
        private List<Role> _filteredRoles = new();

        private readonly MainController? _controller;

        public RoleDialog(MainController? controller = null)
        {
            _controller = controller;
            InitializeComponent();
            
            // Delay loading roles until the window is fully loaded
            Loaded += (s, e) => LoadRoles();
        }

        private void LoadRoles()
        {
            try
            {
                // Ensure UI controls are initialized
                if (RoleListBox == null)
                {
                    MessageBox.Show("Error loading UI.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                var controller = _controller ?? GetController();
                if (controller != null)
                {
                    _allRoles = controller.GetAllRoles();
                    _filteredRoles = new List<Role>(_allRoles);
                    RefreshRoleList();
                }
                else
                {
                    MessageBox.Show("Error accessing main controller.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading roles: {ex.Message}\n\nDetails: {ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
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

        private void RefreshRoleList()
        {
            try
            {
                if (RoleListBox != null)
                {
                    RoleListBox.ItemsSource = null;
                    RoleListBox.ItemsSource = _filteredRoles;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating role list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RoleSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (RoleSearchBox == null) return;
                
                var searchText = RoleSearchBox.Text.ToLower();
                
                if (string.IsNullOrEmpty(searchText) || searchText == "Search roles...")
                {
                    _filteredRoles = new List<Role>(_allRoles);
                }
                else
                {
                    _filteredRoles = _allRoles.Where(role => 
                        role.Name.ToLower().Contains(searchText) ||
                        role.Description.ToLower().Contains(searchText)).ToList();
                }
                
                RefreshRoleList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error searching: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RoleListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (RoleListBox == null || EditRoleButton == null || DeleteRoleButton == null) return;
                
                var selectedRole = RoleListBox.SelectedItem as Role;
                EditRoleButton.IsEnabled = selectedRole != null;
                DeleteRoleButton.IsEnabled = selectedRole != null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting role: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddRoleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var roleEditDialog = new RoleEditDialog();
                if (roleEditDialog.ShowDialog() == true)
                {
                    var controller = _controller ?? GetController();
                    if (controller != null)
                    {
                        var success = controller.AddRole(
                            roleEditDialog.RoleId,
                            roleEditDialog.RoleName,
                            roleEditDialog.Description,
                            roleEditDialog.Color,
                            roleEditDialog.Priority
                        );

                        if (success)
                        {
                            LoadRoles();
                            MessageBox.Show("Role was added successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Error adding role. The role ID may already exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Error accessing controller.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding role: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditRoleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedRole = RoleListBox.SelectedItem as Role;
                if (selectedRole == null) return;

                var roleEditDialog = new RoleEditDialog(selectedRole);
                if (roleEditDialog.ShowDialog() == true)
                {
                    var controller = _controller ?? GetController();
                    if (controller != null)
                    {
                        var success = controller.UpdateRole(
                        selectedRole.RoleId,
                        roleEditDialog.RoleName,
                        roleEditDialog.Description,
                        roleEditDialog.Color,
                        roleEditDialog.Priority
                    );

                        if (success)
                        {
                            LoadRoles();
                            MessageBox.Show("Role was updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Error updating role.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Error accessing controller.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error editing role: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteRoleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedRole = RoleListBox.SelectedItem as Role;
                if (selectedRole == null) return;

                var result = MessageBox.Show(
                    $"Are you sure you want to delete role '{selectedRole.Name}'?\n\nNote: Default roles cannot be deleted.",
                    "Confirm delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var controller = _controller ?? GetController();
                    if (controller != null)
                    {
                        var success = controller.DeleteRole(selectedRole.RoleId);
                        if (success)
                        {
                            LoadRoles();
                            MessageBox.Show("Role was deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Error deleting role. The role may be default or in use.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Error accessing controller.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting role: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
