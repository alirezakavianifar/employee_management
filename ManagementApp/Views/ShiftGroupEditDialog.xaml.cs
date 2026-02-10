using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Services;
using Shared.Utils;
using ManagementApp.Controllers;

namespace ManagementApp.Views
{
    public partial class ShiftGroupEditDialog : Window
    {
        private readonly ILogger<ShiftGroupEditDialog> _logger;
        private readonly ShiftGroup? _originalGroup;
        private readonly MainController? _controller;

        // Properties for accessing form data
        public string GroupId => _originalGroup?.GroupId ?? $"group_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
        public new string Name => NameTextBox?.Text?.Trim() ?? "";
        public string Description => DescriptionTextBox?.Text?.Trim() ?? "";
        public string Color => ColorHexTextBlock?.Text?.Trim() ?? "#4CAF50";
        public int MorningCapacity => int.TryParse(MorningCapacityTextBox?.Text, out int val) ? val : 15;
        public int AfternoonCapacity => int.TryParse(AfternoonCapacityTextBox?.Text, out int val) ? val : 15;
        public int NightCapacity => int.TryParse(NightCapacityTextBox?.Text, out int val) ? val : 15;
        public bool IsGroupActive => true;
        public string MorningForemanId => (MorningForemanComboBox?.SelectedItem as Employee)?.EmployeeId ?? string.Empty;
        public string AfternoonForemanId => (AfternoonForemanComboBox?.SelectedItem as Employee)?.EmployeeId ?? string.Empty;
        public string NightForemanId => (NightForemanComboBox?.SelectedItem as Employee)?.EmployeeId ?? string.Empty;

        public ShiftGroupEditDialog(MainController? controller = null)
        {
            try
            {
                InitializeComponent();
                _controller = controller;
                _logger = LoggingService.CreateLogger<ShiftGroupEditDialog>();
                Title = ResourceManager.GetString("dialog_add_shift_group", "Add new shift group");
                
                // Set default values after initialization
                SetDefaultValues();
                LoadEmployees();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ShiftGroupEditDialog constructor");
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")} creating form: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        public ShiftGroupEditDialog(ShiftGroup group, MainController? controller = null) : this(controller)
        {
            _originalGroup = group;
            Title = ResourceManager.GetString("dialog_edit_shift_group", "Edit shift group");
            
            // Load data after controls are initialized
            this.Loaded += (s, e) => 
            {
                try
                {
                    // Add a small delay to ensure controls are fully initialized
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            LoadGroupData();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error in delayed LoadGroupData");
                            MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")} loading data: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in Loaded event handler");
                    MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")} loading form: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private void SetDefaultValues()
        {
            try
            {
                if (NameTextBox != null)
                    NameTextBox.Text = ResourceManager.GetString("new_group", "New group");
                if (DescriptionTextBox != null)
                    DescriptionTextBox.Text = ResourceManager.GetString("no_description", "Group description");
                if (MorningCapacityTextBox != null) MorningCapacityTextBox.Text = "15";
                if (AfternoonCapacityTextBox != null) AfternoonCapacityTextBox.Text = "15";
                if (NightCapacityTextBox != null) NightCapacityTextBox.Text = "15";
                UpdateColorDisplay("#4CAF50");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting default values");
            }
        }

        private void LoadEmployees()
        {
            try
            {
                if (_controller == null)
                {
                    _logger?.LogWarning("Controller is null, cannot load employees");
                    return;
                }

                var employees = _controller.GetAllEmployees();
                if (employees == null)
                    employees = new List<Employee>();

                // Add "None" option for optional supervisor
                var employeeList = employees.ToList();
                employeeList.Insert(0, new Employee { EmployeeId = string.Empty, FirstName = ResourceManager.GetString("role_none", "None"), LastName = "" });

                if (MorningForemanComboBox != null)
                {
                    MorningForemanComboBox.ItemsSource = employeeList;
                }

                if (AfternoonForemanComboBox != null)
                {
                    AfternoonForemanComboBox.ItemsSource = employeeList;
                }
                
                if (NightForemanComboBox != null)
                {
                    NightForemanComboBox.ItemsSource = employeeList;
                }

                _logger?.LogInformation("Loaded {Count} employees for foreman selection", employees.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading employees");
            }
        }

        private void LoadGroupData()
        {
            if (_originalGroup == null) 
            {
                _logger?.LogWarning("LoadGroupData called with null _originalGroup");
                return;
            }

            try
            {
                _logger?.LogInformation("Loading group data for group: {GroupId}, Name: {Name}", 
                    _originalGroup.GroupId, _originalGroup.Name);

                // Ensure the group has valid data
                var safeName = _originalGroup.Name ?? "New group";
                var safeDescription = _originalGroup.Description ?? "No description";
                var safeColor = _originalGroup.Color ?? "#4CAF50";

                // Set text with additional safety checks
                if (NameTextBox != null)
                {
                    try
                    {
                        NameTextBox.Text = safeName;
                        _logger?.LogInformation("Set NameTextBox.Text to: {Text}", safeName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error setting NameTextBox.Text");
                        throw;
                    }
                }
                else
                {
                    _logger?.LogWarning("NameTextBox is null");
                }

                if (DescriptionTextBox != null)
                {
                    try
                    {
                        DescriptionTextBox.Text = safeDescription;
                        _logger?.LogInformation("Set DescriptionTextBox.Text to: {Text}", safeDescription);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error setting DescriptionTextBox.Text");
                        throw;
                    }
                }
                else
                {
                    _logger?.LogWarning("DescriptionTextBox is null");
                }

                UpdateColorDisplay(safeColor);
                _logger?.LogInformation("Set color display to: {Text}", safeColor);

                // Set Capacity
                if (MorningCapacityTextBox != null) MorningCapacityTextBox.Text = _originalGroup.MorningCapacity.ToString();
                if (AfternoonCapacityTextBox != null) AfternoonCapacityTextBox.Text = _originalGroup.AfternoonCapacity.ToString();
                if (NightCapacityTextBox != null) NightCapacityTextBox.Text = _originalGroup.NightCapacity.ToString();

                // Load foreman selections
                if (_controller != null)
                {
                    var employees = _controller.GetAllEmployees();
                    var employeeList = employees.ToList();
                    // Add "None" option to local list for matching
                    var noneEmployee = new Employee { EmployeeId = string.Empty, FirstName = ResourceManager.GetString("role_none", "None"), LastName = "" };
                    employeeList.Insert(0, noneEmployee);

                    // Ensure ComboBoxes have the employee list
                    if (MorningForemanComboBox != null) MorningForemanComboBox.ItemsSource = employeeList;
                    if (AfternoonForemanComboBox != null) AfternoonForemanComboBox.ItemsSource = employeeList;
                    if (NightForemanComboBox != null) NightForemanComboBox.ItemsSource = employeeList;

                    // Helper to find employee or return None
                    Employee FindEmployeeOrNone(string? id)
                    {
                        if (string.IsNullOrEmpty(id)) return noneEmployee;
                        return employeeList.FirstOrDefault(e => e.EmployeeId == id) ?? noneEmployee;
                    }

                    // Set morning foreman
                    if (MorningForemanComboBox != null)
                    {
                        MorningForemanComboBox.SelectedItem = FindEmployeeOrNone(_originalGroup.MorningShift?.TeamLeaderId);
                    }

                    // Set afternoon foreman
                    if (AfternoonForemanComboBox != null)
                    {
                         AfternoonForemanComboBox.SelectedItem = FindEmployeeOrNone(_originalGroup.AfternoonShift?.TeamLeaderId);
                    }
                    
                    // Set night foreman
                    if (NightForemanComboBox != null)
                    {
                        NightForemanComboBox.SelectedItem = FindEmployeeOrNone(_originalGroup.NightShift?.TeamLeaderId);
                    }
                }

                _logger?.LogInformation("Successfully loaded group data");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading group data");
                MessageBox.Show($"Error loading group data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(Name))
                {
                    MessageBox.Show("Please enter the group name.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    NameTextBox?.Focus();
                    return;
                }

                // Supervisor validation removed - optional

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OkButton_Click");
                MessageBox.Show($"Error confirming: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentColor = Color;
                var colorDialog = new ColorPalettePopup(currentColor)
                {
                    Owner = this
                };

                if (colorDialog.ShowDialog() == true)
                {
                    UpdateColorDisplay(colorDialog.SelectedColor);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error opening color palette");
                MessageBox.Show($"Error opening color palette: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateColorDisplay(string colorHex)
        {
            try
            {
                if (ColorPreviewBorder != null && ColorHexTextBlock != null)
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                    ColorPreviewBorder.Background = new System.Windows.Media.SolidColorBrush(color);
                    ColorHexTextBlock.Text = colorHex;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating color display");
                // Fallback to default color
                if (ColorPreviewBorder != null && ColorHexTextBlock != null)
                {
                    var defaultColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50");
                    ColorPreviewBorder.Background = new System.Windows.Media.SolidColorBrush(defaultColor);
                    ColorHexTextBlock.Text = "#4CAF50";
                }
            }
        }

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}