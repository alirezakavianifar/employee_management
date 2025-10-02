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
                    GroupSearchBox.Text = "جستجو در گروه‌ها...";
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
                        group.Name = "نام نامشخص";
                    if (string.IsNullOrEmpty(group.Description))
                        group.Description = "بدون توضیحات";
                    if (string.IsNullOrEmpty(group.Color))
                        group.Color = "#4CAF50";
                    if (group.MorningCapacity <= 0)
                        group.MorningCapacity = 15;
                    if (group.EveningCapacity <= 0)
                        group.EveningCapacity = 15;
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
                MessageBox.Show($"خطا در بارگذاری گروه‌های شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ListBoxItem CreateGroupListItem(ShiftGroup group)
        {
            try
            {
                if (group == null)
                {
                    _logger.LogWarning("CreateGroupListItem called with null group");
                    return new ListBoxItem { Content = "گروه نامشخص" };
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
                    Text = group.Name ?? "نام نامشخص", 
                    FontWeight = FontWeights.Bold, 
                    FontSize = 14 
                };
                
                // Description
                var descText = new TextBlock 
                { 
                    Text = group.Description ?? "بدون توضیحات", 
                    FontSize = 12, 
                    Foreground = Brushes.Gray 
                };
                
                // Capacity info
                var capacityText = new TextBlock 
                { 
                    Text = $"صبح: {group.MorningCapacity} | عصر: {group.EveningCapacity}", 
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
                return new ListBoxItem { Content = "خطا در نمایش گروه" };
            }
        }

        private void FilterGroups()
        {
            try
            {
                var searchText = GroupSearchBox?.Text?.ToLower() ?? "";
                
                // Clear existing items
                GroupListBox.Items.Clear();
                
                if (string.IsNullOrEmpty(searchText) || searchText == "جستجو در گروه‌ها...")
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
                // Create dialog programmatically to avoid XAML issues
                var dialog = new Window()
                {
                    Title = "افزودن گروه شیفت جدید",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };

                var mainGrid = new Grid() { Margin = new Thickness(20) };
                mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });

                // Name input
                var nameLabel = new Label() { Content = "نام گروه:", FontWeight = FontWeights.Bold };
                var nameTextBox = new TextBox() { Height = 25, Margin = new Thickness(0, 5, 0, 15) };
                nameTextBox.Text = "گروه جدید";
                
                Grid.SetRow(nameLabel, 0);
                Grid.SetRow(nameTextBox, 0);
                mainGrid.Children.Add(nameLabel);
                mainGrid.Children.Add(nameTextBox);

                // Description input
                var descLabel = new Label() { Content = "توضیحات:", FontWeight = FontWeights.Bold };
                var descTextBox = new TextBox() { Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Margin = new Thickness(0, 5, 0, 15) };
                descTextBox.Text = "توضیحات گروه";
                
                Grid.SetRow(descLabel, 1);
                Grid.SetRow(descTextBox, 1);
                mainGrid.Children.Add(descLabel);
                mainGrid.Children.Add(descTextBox);

                // Color input
                var colorLabel = new Label() { Content = "رنگ گروه:", FontWeight = FontWeights.Bold };
                var colorTextBox = new TextBox() { Height = 25, Margin = new Thickness(0, 5, 0, 15) };
                colorTextBox.Text = "#4CAF50";
                
                Grid.SetRow(colorLabel, 2);
                Grid.SetRow(colorTextBox, 2);
                mainGrid.Children.Add(colorLabel);
                mainGrid.Children.Add(colorTextBox);

                // Buttons
                var buttonPanel = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
                var okButton = new Button() { Content = "تأیید", Width = 80, Height = 30, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
                var cancelButton = new Button() { Content = "لغو", Width = 80, Height = 30, IsCancel = true };
                
                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                
                Grid.SetRow(buttonPanel, 4);
                mainGrid.Children.Add(buttonPanel);

                dialog.Content = mainGrid;

                bool? result = null;
                okButton.Click += (s, args) => { result = true; dialog.Close(); };
                cancelButton.Click += (s, args) => { result = false; dialog.Close(); };

                dialog.ShowDialog();

                if (result == true && !string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    var groupName = nameTextBox.Text.Trim();
                    var groupId = $"group_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
                    var description = descTextBox.Text?.Trim() ?? "";
                    var color = colorTextBox.Text?.Trim() ?? "#4CAF50";
                    
                    var success = _controller.AddShiftGroup(
                        groupId, 
                        groupName, 
                        description, 
                        color, 
                        15, 
                        15);
                    
                    if (success)
                    {
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
                        
                        MessageBox.Show($"گروه شیفت '{groupName}' با موفقیت اضافه شد.", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding shift group");
                MessageBox.Show($"خطا در افزودن گروه شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedGroup == null)
                {
                    MessageBox.Show("لطفاً یک گروه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _logger.LogInformation("Opening edit dialog for group: {GroupId}, Name: {Name}", 
                    _selectedGroup.GroupId, _selectedGroup.Name);

                // Ensure the selected group has valid data before passing to dialog
                if (string.IsNullOrEmpty(_selectedGroup.Name))
                    _selectedGroup.Name = "گروه جدید";
                if (string.IsNullOrEmpty(_selectedGroup.Description))
                    _selectedGroup.Description = "بدون توضیحات";
                if (string.IsNullOrEmpty(_selectedGroup.Color))
                    _selectedGroup.Color = "#4CAF50";

                var dialog = new ShiftGroupEditDialog(_selectedGroup);
                if (dialog.ShowDialog() == true)
                {
                    var success = _controller.UpdateShiftGroup(
                        _selectedGroup.GroupId,
                        dialog.Name,
                        dialog.Description,
                        dialog.Color,
                        dialog.MorningCapacity,
                        dialog.EveningCapacity,
                        dialog.IsGroupActive);
                    
                    if (success)
                    {
                        LoadGroups();
                        MessageBox.Show($"گروه شیفت '{dialog.Name}' با موفقیت بروزرسانی شد.", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing shift group");
                MessageBox.Show($"خطا در ویرایش گروه شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedGroup == null)
                {
                    MessageBox.Show("لطفاً یک گروه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_selectedGroup.GroupId == "default")
                {
                    MessageBox.Show("نمی‌توان گروه پیش‌فرض را حذف کرد", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"آیا مطمئن هستید که می‌خواهید گروه '{_selectedGroup.Name}' را حذف کنید؟\n\nاین عمل غیرقابل بازگشت است.",
                    "تأیید حذف",
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
                        MessageBox.Show($"گروه شیفت '{groupToDelete.Name}' با موفقیت حذف شد.", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shift group");
                MessageBox.Show($"خطا در حذف گروه شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
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
