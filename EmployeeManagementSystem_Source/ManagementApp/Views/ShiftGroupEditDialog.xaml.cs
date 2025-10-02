using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Services;

namespace ManagementApp.Views
{
    public partial class ShiftGroupEditDialog : Window
    {
        private readonly ILogger<ShiftGroupEditDialog> _logger;
        private readonly ShiftGroup? _originalGroup;

        // Properties for accessing form data
        public string GroupId => $"group_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
        public new string Name => NameTextBox?.Text?.Trim() ?? "";
        public string Description => DescriptionTextBox?.Text?.Trim() ?? "";
        public string Color => ColorTextBox?.Text?.Trim() ?? "#4CAF50";
        public int MorningCapacity => 15;
        public int EveningCapacity => 15;
        public bool IsGroupActive => true;

        public ShiftGroupEditDialog()
        {
            try
            {
                InitializeComponent();
                _logger = LoggingService.CreateLogger<ShiftGroupEditDialog>();
                Title = "افزودن گروه شیفت جدید";
                
                // Set default values after initialization
                SetDefaultValues();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in ShiftGroupEditDialog constructor");
                MessageBox.Show($"خطا در ایجاد فرم: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        public ShiftGroupEditDialog(ShiftGroup group) : this()
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
                if (ColorTextBox != null)
                    ColorTextBox.Text = "#4CAF50";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting default values");
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

                if (ColorTextBox != null)
                {
                    try
                    {
                        ColorTextBox.Text = safeColor;
                        _logger?.LogInformation("Set ColorTextBox.Text to: {Text}", safeColor);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error setting ColorTextBox.Text");
                        throw;
                    }
                }
                else
                {
                    _logger?.LogWarning("ColorTextBox is null");
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
    }
}