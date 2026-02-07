using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Services;
using Shared.Utils;
using ManagementApp.Controllers;

namespace ManagementApp.Views
{
    public partial class ShiftGroupDialog : Window
    {
        private readonly MainController _controller;
        private readonly ILogger<ShiftGroupDialog> _logger;
        private List<ShiftGroup> _allGroups = new();
        private ShiftGroup? _selectedGroup;

        public ShiftGroupDialog(MainController controller)
        {
            InitializeComponent();
            _controller = controller;
            _logger = LoggingService.CreateLogger<ShiftGroupDialog>();
            
            // Set the search box text after initialization
            try
            {
                if (GroupSearchBox != null)
                {
                    GroupSearchBox.Text = ResourceManager.GetString("search_groups", "Search groups...");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting GroupSearchBox text");
            }
            
            LoadGroups();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            _controller.ShiftGroupsUpdated += OnShiftGroupsUpdated;
        }

        private void OnShiftGroupsUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                LoadGroups();
            });
        }

        private void LoadGroups()
        {
            try
            {
                _allGroups = _controller.GetAllShiftGroups();
                
                // Ensure _allGroups is not null
                if (_allGroups == null)
                {
                    _allGroups = new List<ShiftGroup>();
                }
                
                // Ensure all groups have valid properties
                foreach (var group in _allGroups)
                {
                    if (group == null) continue; // Skip null groups
                    
                    if (string.IsNullOrEmpty(group.Name))
                        group.Name = ResourceManager.GetString("unspecified_name", "Unspecified name");
                    if (string.IsNullOrEmpty(group.Description))
                        group.Description = ResourceManager.GetString("no_description", "No description");
                    if (string.IsNullOrEmpty(group.Color))
                        group.Color = "#4CAF50";
                    if (group.MorningCapacity <= 0)
                        group.MorningCapacity = 15;
                    if (group.AfternoonCapacity <= 0)
                        group.AfternoonCapacity = 15;
                    if (group.NightCapacity <= 0)
                        group.NightCapacity = 15;
                }
                
                // Clear existing items and add groups manually
                if (GroupListBox != null)
                {
                    GroupListBox.Items.Clear();
                    foreach (var group in _allGroups)
                    {
                        if (group != null)
                        {
                            var item = CreateGroupListItem(group);
                            GroupListBox.Items.Add(item);
                        }
                    }
                }
                
                // Update button states after loading
                UpdateButtonStates();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shift groups");
                MessageBox.Show(string.Format(ResourceManager.GetString("err_load_shift_groups", "Error loading shift groups: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ListBoxItem CreateGroupListItem(ShiftGroup group)
        {
            try
            {
                if (group == null)
                {
                    _logger.LogWarning("CreateGroupListItem called with null group");
                    return new ListBoxItem { Content = ResourceManager.GetString("unknown_group", "Unknown group") };
                }

                var listItem = new ListBoxItem();
                listItem.Tag = group; // Store the group object for later reference
                
                var stackPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
                
                // Color rectangle with error handling
                Rectangle rectangle;
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(group.Color ?? "#4CAF50");
                    rectangle = new Rectangle 
                    { 
                        Width = 20, 
                        Height = 20, 
                        Fill = new SolidColorBrush(color),
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                }
                catch
                {
                    // Fallback to default color if color parsing fails
                    rectangle = new Rectangle 
                    { 
                        Width = 20, 
                        Height = 20, 
                        Fill = Brushes.Green,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                }
                
                // Content stack panel
                var contentPanel = new StackPanel();
                
                // Name
                var nameText = new TextBlock 
                { 
                    Text = group.Name ?? ResourceManager.GetString("unspecified_name", "Unspecified name"), 
                    FontWeight = FontWeights.Bold, 
                    FontSize = 14 
                };
                
                // Description
                var descText = new TextBlock 
                { 
                    Text = group.Description ?? ResourceManager.GetString("no_description", "No description"), 
                    FontSize = 12, 
                    Foreground = Brushes.Gray 
                };
                
                // Capacity info
                var capacityText = new TextBlock 
                { 
                    Text = string.Format(ResourceManager.GetString("shift_capacities_format", "Morning: {0} | Afternoon: {1} | Night: {2}"), group.MorningCapacity, group.AfternoonCapacity, group.NightCapacity), 
                    FontSize = 11, 
                    Foreground = Brushes.DarkBlue 
                };
                
                contentPanel.Children.Add(nameText);
                contentPanel.Children.Add(descText);
                contentPanel.Children.Add(capacityText);
                
                stackPanel.Children.Add(rectangle);
                stackPanel.Children.Add(contentPanel);
                
                listItem.Content = stackPanel;
                return listItem;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group list item");
                return new ListBoxItem { Content = ResourceManager.GetString("error_displaying_group", "Error displaying group") };
            }
        }

        private void FilterGroups()
        {
            try
            {
                var searchText = GroupSearchBox?.Text?.ToLower() ?? "";
                
                // Clear existing items
                GroupListBox.Items.Clear();
                
                if (string.IsNullOrEmpty(searchText) || searchText == ResourceManager.GetString("search_groups", "Search groups..."))
                {
                    // Show all groups
                    foreach (var group in _allGroups)
                    {
                        var item = CreateGroupListItem(group);
                        GroupListBox.Items.Add(item);
                    }
                }
                else
                {
                    // Filter groups
                    var filteredGroups = _allGroups.Where(g => 
                        g != null &&
                        ((!string.IsNullOrEmpty(g.Name) && g.Name.ToLower().Contains(searchText)) ||
                        (!string.IsNullOrEmpty(g.Description) && g.Description.ToLower().Contains(searchText)) ||
                        (!string.IsNullOrEmpty(g.GroupId) && g.GroupId.ToLower().Contains(searchText)))).ToList();
                    
                    foreach (var group in filteredGroups)
                    {
                        var item = CreateGroupListItem(group);
                        GroupListBox.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering groups");
                // Fallback to showing all groups if filtering fails
                GroupListBox.Items.Clear();
                foreach (var group in _allGroups)
                {
                    var item = CreateGroupListItem(group);
                    GroupListBox.Items.Add(item);
                }
            }
        }

        private void GroupSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterGroups();
        }

        private void GroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupListBox.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is ShiftGroup group)
            {
                _selectedGroup = group;
            }
            else
            {
                _selectedGroup = null;
            }
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            EditGroupButton.IsEnabled = _selectedGroup != null;
            DeleteGroupButton.IsEnabled = _selectedGroup != null && _selectedGroup.GroupId != "default";
        }

        private void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ShiftGroupEditDialog(_controller);
                if (dialog.ShowDialog() == true)
                {
                    var groupName = dialog.Name;
                    var groupId = dialog.GroupId;
                    var description = dialog.Description;
                    var color = dialog.Color;
                    
                    var success = _controller.AddShiftGroup(
                        groupId, 
                        groupName, 
                        description, 
                        "", // supervisorName (not used)
                        color, 
                        dialog.MorningCapacity, 
                        dialog.AfternoonCapacity,
                        dialog.NightCapacity);
                    
                    if (success)
                    {
                        // Set foremen for each shift
                        if (!string.IsNullOrEmpty(dialog.MorningForemanId))
                        {
                            _controller.SetTeamLeader("morning", dialog.MorningForemanId, groupId);
                        }

                        if (!string.IsNullOrEmpty(dialog.AfternoonForemanId))
                        {
                            _controller.SetTeamLeader("afternoon", dialog.AfternoonForemanId, groupId);
                        }
                        
                        if (!string.IsNullOrEmpty(dialog.NightForemanId))
                        {
                            _controller.SetTeamLeader("night", dialog.NightForemanId, groupId);
                        }
                        
                        LoadGroups();
                        
                        // Auto-select the newly created group
                        var newGroup = _allGroups.FirstOrDefault(g => g.GroupId == groupId);
                        if (newGroup != null)
                        {
                            // Find and select the new group in the list
                            for (int i = 0; i < GroupListBox.Items.Count; i++)
                            {
                                if (GroupListBox.Items[i] is ListBoxItem item && item.Tag == newGroup)
                                {
                                    GroupListBox.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                        
                        MessageBox.Show(string.Format(ResourceManager.GetString("msg_group_added", "Shift group '{0}' was added successfully."), groupName), ResourceManager.GetString("msg_success", "Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding shift group");
                MessageBox.Show(string.Format(ResourceManager.GetString("err_add_group", "Error adding shift group: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedGroup == null)
                {
                    MessageBox.Show(ResourceManager.GetString("msg_select_group", "Please select a group"), ResourceManager.GetString("msg_warning", "Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _logger.LogInformation("Opening edit dialog for group: {GroupId}, Name: {Name}", 
                    _selectedGroup.GroupId, _selectedGroup.Name);

                // Ensure the selected group has valid data before passing to dialog
                if (string.IsNullOrEmpty(_selectedGroup.Name))
                    _selectedGroup.Name = ResourceManager.GetString("new_group", "New group");
                if (string.IsNullOrEmpty(_selectedGroup.Description))
                    _selectedGroup.Description = ResourceManager.GetString("no_description", "No description");
                if (string.IsNullOrEmpty(_selectedGroup.Color))
                    _selectedGroup.Color = "#4CAF50";

                var dialog = new ShiftGroupEditDialog(_selectedGroup, _controller);
                if (dialog.ShowDialog() == true)
                {
                    // Store group ID before UpdateShiftGroup in case _selectedGroup reference becomes stale
                    var groupId = _selectedGroup?.GroupId;
                    if (string.IsNullOrEmpty(groupId))
                    {
                        MessageBox.Show("Error: Invalid group ID", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    var success = _controller?.UpdateShiftGroup(
                        groupId,
                        dialog.Name,
                        dialog.Description,
                        "", // supervisorName (not used)
                        dialog.Color,
                        dialog.MorningCapacity,
                        dialog.AfternoonCapacity,
                        dialog.NightCapacity,
                        dialog.IsGroupActive) ?? false;
                    
                    if (success && _controller != null)
                    {
                        // Set foremen for each shift
                        if (!string.IsNullOrEmpty(dialog.MorningForemanId))
                        {
                            _controller.SetTeamLeader("morning", dialog.MorningForemanId, groupId);
                        }
                        else
                        {
                            _controller.SetTeamLeader("morning", string.Empty, groupId);
                        }

                        if (!string.IsNullOrEmpty(dialog.AfternoonForemanId))
                        {
                            _controller.SetTeamLeader("afternoon", dialog.AfternoonForemanId, groupId);
                        }
                        else
                        {
                            _controller.SetTeamLeader("afternoon", string.Empty, groupId);
                        }
                        
                        if (!string.IsNullOrEmpty(dialog.NightForemanId))
                        {
                            _controller.SetTeamLeader("night", dialog.NightForemanId, groupId);
                        }
                        else
                        {
                            _controller.SetTeamLeader("night", string.Empty, groupId);
                        }
                        
                        LoadGroups();
                        MessageBox.Show(string.Format(ResourceManager.GetString("msg_group_updated", "Shift group '{0}' was updated successfully."), dialog.Name), ResourceManager.GetString("msg_success", "Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing shift group");
                MessageBox.Show(string.Format(ResourceManager.GetString("err_edit_group", "Error editing shift group: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedGroup == null)
                {
                    MessageBox.Show("Please select a group", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_selectedGroup.GroupId == "default")
                {
                    MessageBox.Show(ResourceManager.GetString("err_cannot_delete_default_group", "Cannot delete the default group"), ResourceManager.GetString("msg_warning", "Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
 
                var result = MessageBox.Show(
                    string.Format(ResourceManager.GetString("msg_confirm_delete_group", "Are you sure you want to delete group '{0}'?\n\nThis action cannot be undone."), _selectedGroup.Name),
                    ResourceManager.GetString("header_confirm_delete", "Confirm Delete"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var groupToDelete = _selectedGroup; // Store reference before deletion
                    var success = _controller.DeleteShiftGroup(_selectedGroup.GroupId);
                    if (success)
                    {
                        _selectedGroup = null; // Clear selection before reloading
                        LoadGroups();
                        MessageBox.Show(string.Format(ResourceManager.GetString("msg_group_deleted", "Shift group '{0}' was deleted successfully."), groupToDelete.Name), ResourceManager.GetString("msg_success", "Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shift group");
                MessageBox.Show(string.Format(ResourceManager.GetString("err_delete_group", "Error deleting shift group: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _controller.ShiftGroupsUpdated -= OnShiftGroupsUpdated;
            base.OnClosed(e);
        }
    }
}
