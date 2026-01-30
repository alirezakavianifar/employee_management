using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Services;
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
        public int MorningCapacity => 15;
        public int EveningCapacity => 15;
        public bool IsGroupActive => true;
        public string MorningForemanId => (MorningForemanComboBox?.SelectedItem as Employee)?.EmployeeId ?? string.Empty;
        public string EveningForemanId => (EveningForemanComboBox?.SelectedItem as Employee)?.EmployeeId ?? string.Empty;

        public ShiftGroupEditDialog(MainController? controller = null)
        {
            try
            {
                InitializeComponent();
                _controller = controller;
                _logger = LoggingService.CreateLogger<ShiftGroupEditDialog>();
                Title = "افزودن گروه شیفت جدید";
                
                // Set default values after initialization
                SetDefaultValues();
                LoadEmployees();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ShiftGroupEditDialog constructor");
                MessageBox.Show($"خطا در ایجاد فرم: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        public ShiftGroupEditDialog(ShiftGroup group, MainController? controller = null) : this(controller)
        {
            _originalGroup = group;
            Title = "ویرایش گروه شیفت";
            
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
                            MessageBox.Show($"خطا در بارگذاری داده‌ها: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in Loaded event handler");
                    MessageBox.Show($"خطا در بارگذاری فرم: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private void SetDefaultValues()
        {
            try
            {
                if (NameTextBox != null)
                    NameTextBox.Text = "گروه جدید";
                if (DescriptionTextBox != null)
                    DescriptionTextBox.Text = "توضیحات گروه";
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

                // Foremen are required, so don't add null option
                var employeeList = employees.ToList();

                if (MorningForemanComboBox != null)
                {
                    MorningForemanComboBox.ItemsSource = employeeList;
                    // DisplayMemberPath removed - ItemTemplate in XAML handles display
                }

                if (EveningForemanComboBox != null)
                {
                    EveningForemanComboBox.ItemsSource = employeeList;
                    // DisplayMemberPath removed - ItemTemplate in XAML handles display
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
                var safeName = _originalGroup.Name ?? "گروه جدید";
                var safeDescription = _originalGroup.Description ?? "بدون توضیحات";
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

                // Load foreman selections
                if (_controller != null)
                {
                    var employees = _controller.GetAllEmployees();
                    var employeeList = employees.ToList();

                    // Ensure ComboBoxes have the employee list
                    if (MorningForemanComboBox != null)
                    {
                        MorningForemanComboBox.ItemsSource = employeeList;
                        // DisplayMemberPath removed - ItemTemplate in XAML handles display
                    }

                    if (EveningForemanComboBox != null)
                    {
                        EveningForemanComboBox.ItemsSource = employeeList;
                        // DisplayMemberPath removed - ItemTemplate in XAML handles display
                    }

                    // Set morning foreman
                    if (MorningForemanComboBox != null && _originalGroup.MorningShift != null && !string.IsNullOrEmpty(_originalGroup.MorningShift.TeamLeaderId))
                    {
                        var morningForeman = employees.FirstOrDefault(emp => emp.EmployeeId == _originalGroup.MorningShift.TeamLeaderId);
                        MorningForemanComboBox.SelectedItem = morningForeman;
                    }

                    // Set evening foreman
                    if (EveningForemanComboBox != null && _originalGroup.EveningShift != null && !string.IsNullOrEmpty(_originalGroup.EveningShift.TeamLeaderId))
                    {
                        var eveningForeman = employees.FirstOrDefault(emp => emp.EmployeeId == _originalGroup.EveningShift.TeamLeaderId);
                        EveningForemanComboBox.SelectedItem = eveningForeman;
                    }
                }

                _logger?.LogInformation("Successfully loaded group data");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading group data");
                MessageBox.Show($"خطا در بارگذاری داده‌های گروه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(Name))
                {
                    MessageBox.Show("لطفاً نام گروه را وارد کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                    NameTextBox?.Focus();
                    return;
                }

                // Validate that morning foreman is selected
                if (string.IsNullOrEmpty(MorningForemanId))
                {
                    MessageBox.Show("لطفاً سرپرست شیفت صبح را انتخاب کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                    MorningForemanComboBox?.Focus();
                    return;
                }

                // Validate that evening foreman is selected
                if (string.IsNullOrEmpty(EveningForemanId))
                {
                    MessageBox.Show("لطفاً سرپرست شیفت عصر را انتخاب کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                    EveningForemanComboBox?.Focus();
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in OkButton_Click");
                MessageBox.Show($"خطا در تأیید: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"خطا در باز کردن پالت رنگ: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}