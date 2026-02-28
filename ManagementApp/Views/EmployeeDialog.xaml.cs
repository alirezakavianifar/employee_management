using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Shared.Models;
using Shared.Utils;
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
        public string ShieldColor { get; private set; } = "Blue";
        public bool ShowShield { get; private set; } = true;
        public List<string> StickerPaths { get; private set; } = new List<string>();
        public string MedalBadgePath { get; private set; } = string.Empty;
        public string PersonnelId { get; private set; } = string.Empty;
        public string Phone { get; private set; } = string.Empty;
        public bool ShowPhone { get; private set; } = true;

        // Backward compatibility
        public string Role => RoleId;

        private readonly MainController? _controller;

        public EmployeeDialog(MainController? controller = null)
        {
            _controller = controller;
            InitializeComponent();
            LoadRoles();
            LoadShiftGroups();
            LoadShieldColors();
        }

        public EmployeeDialog(Shared.Models.Employee employee, MainController? controller = null) : this(controller)
        {
            FirstNameTextBox.Text = employee.FirstName;
            LastNameTextBox.Text = employee.LastName;
            PersonnelIdTextBox.Text = employee.PersonnelId;
            PhoneTextBox.Text = employee.Phone;
            PhotoPath = employee.PhotoPath;
            IsManagerCheckBox.IsChecked = employee.IsManager;
            ShieldColor = employee.ShieldColor;
            ShowShield = employee.ShowShield;
            ShowShieldCheckBox.IsChecked = employee.ShowShield;
            ShowPhone = employee.ShowPhone;
            ShowPhoneCheckBox.IsChecked = employee.ShowPhone;
            StickerPaths = employee.StickerPaths ?? new List<string>();
            MedalBadgePath = employee.MedalBadgePath;
            
            // Load photo preview if photo exists
            if (!string.IsNullOrEmpty(PhotoPath) && File.Exists(PhotoPath))
            {
                LoadPhotoPreview(PhotoPath);
            }
            
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
            
            // Set the selected shield color
            foreach (ComboBoxItem item in ShieldColorComboBox.Items)
            {
                if (item.Tag?.ToString() == employee.ShieldColor)
                {
                    ShieldColorComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Load stickers
            StickersListBox.Items.Clear();
            foreach (var stickerPath in StickerPaths)
            {
                StickersListBox.Items.Add(Path.GetFileName(stickerPath));
            }
            
            // Load medal/badge path
            MedalBadgePathTextBox.Text = string.IsNullOrEmpty(MedalBadgePath) ? "" : Path.GetFileName(MedalBadgePath);
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
                        var localizedRoleName = ResourceManager.GetString("role_" + role.RoleId, role.Name);
                        var item = new ComboBoxItem
                        {
                            Content = localizedRoleName,
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
                    MessageBox.Show(ResourceManager.GetString("err_access_controller", "Error accessing controller."), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(ResourceManager.GetString("err_load_roles", "Error loading roles: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(ResourceManager.GetString("err_access_controller", "Error accessing controller."), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(ResourceManager.GetString("err_load_shift_groups", "Error loading shift groups: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadShieldColors()
        {
            ShieldColorComboBox.Items.Clear();
            var colors = new[] { "Red", "Blue", "Yellow", "Black", "Orange", "Green", "Gray" };
            var colorNames = new Dictionary<string, string>
            {
                { "Red", "Red" },
                { "Blue", "Blue" },
                { "Yellow", "Yellow" },
                { "Black", "Black" },
                { "Orange", "Orange" },
                { "Green", "Green" },
                { "Gray", "Gray" }
            };
            
            foreach (var color in colors)
            {
                var item = new ComboBoxItem
                {
                    Content = colorNames[color],
                    Tag = color
                };
                ShieldColorComboBox.Items.Add(item);
            }
            
            // Select default (Blue)
            var defaultColor = ShieldColorComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == "Blue");
            ShieldColorComboBox.SelectedItem = defaultColor ?? ShieldColorComboBox.Items[0];
        }

        private void AddStickerButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PNG Files (*.png)|*.png|All Files (*.*)|*.*",
                Title = "Select sticker file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var config = AppConfigHelper.Config;
                    var dataDir = config.DataDirectory;
                    var stickersDir = Path.Combine(dataDir, "Stickers");
                    
                    if (!Directory.Exists(stickersDir))
                    {
                        Directory.CreateDirectory(stickersDir);
                    }
                    
                    var fileName = Path.GetFileName(openFileDialog.FileName);
                    var destPath = Path.Combine(stickersDir, fileName);
                    
                    // Copy file if it's not already in the stickers directory
                    if (!Path.GetFullPath(openFileDialog.FileName).Equals(Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(openFileDialog.FileName, destPath, true);
                    }
                    
                    StickerPaths.Add(destPath);
                    StickersListBox.Items.Add(fileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(ResourceManager.GetString("err_add_sticker", "Error adding sticker: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveStickerButton_Click(object sender, RoutedEventArgs e)
        {
            if (StickersListBox.SelectedIndex >= 0 && StickersListBox.SelectedIndex < StickerPaths.Count)
            {
                StickerPaths.RemoveAt(StickersListBox.SelectedIndex);
                StickersListBox.Items.RemoveAt(StickersListBox.SelectedIndex);
            }
        }

        private void SelectMedalButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "PNG Files (*.png)|*.png|All Files (*.*)|*.*",
                Title = "Select badge file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var config = AppConfigHelper.Config;
                    var dataDir = config.DataDirectory;
                    var medalsDir = Path.Combine(dataDir, "Medals");
                    
                    if (!Directory.Exists(medalsDir))
                    {
                        Directory.CreateDirectory(medalsDir);
                    }
                    
                    var fileName = Path.GetFileName(openFileDialog.FileName);
                    var destPath = Path.Combine(medalsDir, fileName);
                    
                    // Copy file if it's not already in the medals directory
                    if (!Path.GetFullPath(openFileDialog.FileName).Equals(Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                    {
                        // If destination file exists and might be locked, use a unique filename
                        if (File.Exists(destPath))
                        {
                            try
                            {
                                // Try to copy/overwrite - if it fails due to lock, use unique name
                                File.Copy(openFileDialog.FileName, destPath, true);
                            }
                            catch (System.IO.IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
                            {
                                // File is locked - use unique filename with timestamp
                                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                                var extension = Path.GetExtension(fileName);
                                var uniqueFileName = $"{nameWithoutExt}_{DateTimeOffset.Now.ToUnixTimeSeconds()}{extension}";
                                destPath = Path.Combine(medalsDir, uniqueFileName);
                                File.Copy(openFileDialog.FileName, destPath, false);
                                fileName = uniqueFileName;
                            }
                        }
                        else
                        {
                            // File doesn't exist, safe to copy
                            File.Copy(openFileDialog.FileName, destPath, false);
                        }
                    }
                    
                    MedalBadgePath = destPath;
                    MedalBadgePathTextBox.Text = Path.GetFileName(destPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(ResourceManager.GetString("err_select_badge", "Error selecting badge: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemoveMedalButton_Click(object sender, RoutedEventArgs e)
        {
            MedalBadgePath = string.Empty;
            MedalBadgePathTextBox.Text = "";
        }

        private void SelectPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
                Title = "Select employee photo"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    PhotoPath = openFileDialog.FileName;
                    LoadPhotoPreview(PhotoPath);

                    // Try to detect name and personnel ID from filename (format: FirstName_LastName_PersonnelId.ext)
                    var controller = _controller ?? GetController();
                    if (controller != null)
                    {
                        var (detectedFirstName, detectedLastName) = controller.DetectNameFromFolder(PhotoPath);
                        var detectedPersonnelId = controller.DetectPersonnelIdFromFilename(PhotoPath);
                        
                        if (detectedFirstName != null && detectedLastName != null)
                        {
                            // Auto-fill name fields if they are empty or ask user if they want to update
                            if (string.IsNullOrWhiteSpace(FirstNameTextBox.Text) && string.IsNullOrWhiteSpace(LastNameTextBox.Text))
                            {
                                // Auto-fill if both fields are empty
                                FirstNameTextBox.Text = detectedFirstName;
                                LastNameTextBox.Text = detectedLastName;
                            }
                            else if (FirstNameTextBox.Text != detectedFirstName || LastNameTextBox.Text != detectedLastName)
                            {
                                // Ask user if they want to update existing values
                                var result = MessageBox.Show(
                                    string.Format(ResourceManager.GetString("msg_name_detected", "Name detected from folder: {0} {1}\nDo you want to update the name?"), detectedFirstName, detectedLastName),
                                    ResourceManager.GetString("header_name_detected", "Name detected from folder"),
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);
                                
                                if (result == MessageBoxResult.Yes)
                                {
                                    FirstNameTextBox.Text = detectedFirstName;
                                    LastNameTextBox.Text = detectedLastName;
                                }
                            }
                        }

                        // Auto-fill personnel ID from filename if detected and field is empty
                        if (detectedPersonnelId != null && string.IsNullOrWhiteSpace(PersonnelIdTextBox.Text))
                        {
                            PersonnelIdTextBox.Text = detectedPersonnelId;
                        }
                        else if (detectedPersonnelId != null && PersonnelIdTextBox.Text != detectedPersonnelId)
                        {
                            // Ask user if they want to update existing personnel ID
                            var result = MessageBox.Show(
                                string.Format(ResourceManager.GetString("msg_personnel_id_detected", "Personnel ID detected from filename: {0}\nDo you want to update the personnel ID?"), detectedPersonnelId),
                                ResourceManager.GetString("header_personnel_id_detected", "Personnel ID detected"),
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            
                            if (result == MessageBoxResult.Yes)
                            {
                                PersonnelIdTextBox.Text = detectedPersonnelId;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(string.Format(ResourceManager.GetString("err_select_photo", "Error selecting photo: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RemovePhotoButton_Click(object sender, RoutedEventArgs e)
        {
            PhotoPath = string.Empty;
            PhotoPreview.Source = null;
        }

        private void LoadPhotoPreview(string photoPath)
        {
            try
            {
                if (File.Exists(photoPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(photoPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    PhotoPreview.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(ResourceManager.GetString("err_load_photo_preview", "Error loading photo preview: {0}"), ex.Message), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
            // Validation: Either name fields must be filled OR photo filename must contain detectable name (format: FirstName_LastName_PersonnelId.ext)
            var hasNameFields = !string.IsNullOrWhiteSpace(FirstNameTextBox.Text) && !string.IsNullOrWhiteSpace(LastNameTextBox.Text);
            var hasPhotoWithDetectedName = false;
            
            if (!hasNameFields && !string.IsNullOrEmpty(PhotoPath))
            {
                // Try to detect name from filename (format: FirstName_LastName_PersonnelId.ext)
                var controller = _controller ?? GetController();
                if (controller != null)
                {
                    var (detectedFirstName, detectedLastName) = controller.DetectNameFromFolder(PhotoPath);
                    if (detectedFirstName != null && detectedLastName != null)
                    {
                        hasPhotoWithDetectedName = true;
                        // Auto-fill the name fields from detected name
                        FirstNameTextBox.Text = detectedFirstName;
                        LastNameTextBox.Text = detectedLastName;
                    }
                }
            }

            if (!hasNameFields && !hasPhotoWithDetectedName)
            {
                MessageBox.Show(ResourceManager.GetString("err_name_required", "Please enter first and last name or select a photo with filename format FirstName_LastName_PersonnelId.ext"), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (RoleComboBox.SelectedItem == null)
            {
                MessageBox.Show(ResourceManager.GetString("msg_select_role", "Please select a role"), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ShiftGroupComboBox.SelectedItem == null)
            {
                MessageBox.Show(ResourceManager.GetString("msg_select_shift_group", "Please select a shift group"), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var phoneInput = PhoneTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(phoneInput))
            {
                // Relaxed constraints: allow any characters, length up to 50
                var phonePattern = @"^.{1,50}$";
                if (!Regex.IsMatch(phoneInput, phonePattern))
                {
                    MessageBox.Show(ResourceManager.GetString("msg_invalid_phone", "Please enter a valid phone number (1 to 50 characters)."), ResourceManager.GetString("msg_warning", "Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            FirstName = FirstNameTextBox.Text.Trim();
            LastName = LastNameTextBox.Text.Trim();
            PersonnelId = PersonnelIdTextBox.Text.Trim();
            Phone = PhoneTextBox.Text.Trim();
            RoleId = (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "employee";
            ShiftGroupId = (ShiftGroupComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "default";
            ShieldColor = (ShieldColorComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Blue";
            ShowShield = ShowShieldCheckBox.IsChecked ?? true;
            ShowPhone = ShowPhoneCheckBox.IsChecked ?? true;
            IsManager = IsManagerCheckBox.IsChecked ?? false;
            
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
