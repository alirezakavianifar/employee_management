using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ManagementApp.Controllers;
using ManagementApp.Extensions;
using ManagementApp.Services;
using Shared.Models;
using Shared.Services;
using Shared.Utils;

namespace ManagementApp.Views
{
    public class GroupStatistic
    {
        public string GroupName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly MainController _controller;
        public MainController Controller => _controller;
        private readonly ILogger<MainWindow> _logger;
        
        // Static reference for dialogs to access the main controller
        public static MainWindow? Instance { get; private set; }
        private readonly DispatcherTimer _timer;
        private readonly PdfReportService _pdfService;
        private Employee? _selectedEmployee;
        private Shared.Models.Task? _selectedTask;
        
        // Converter instance as property for XAML binding
        public ManagementApp.Converters.EmployeePhotoConverter EmployeePhotoConverter { get; } = new ManagementApp.Converters.EmployeePhotoConverter();

        public MainWindow()
        {
            // Initialize logger first (before InitializeComponent to catch any XAML errors)
            _logger = LoggingService.CreateLogger<MainWindow>();
            
            try
            {
                InitializeComponent();
                _logger.LogInformation("MainWindow: InitializeComponent completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MainWindow: Failed during InitializeComponent");
                MessageBox.Show($"خطا در بارگذاری رابط کاربری:\n\n{ex.Message}\n\nجزئیات:\n{ex}", 
                    "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                throw; // Re-throw to let App.xaml.cs handle it
            }
            
            try
            {
                // EmployeePhotoConverter is now defined in XAML resources
                
                _controller = new MainController();
                _logger.LogInformation("MainWindow: MainController created");
                
                _pdfService = new PdfReportService();
                _logger.LogInformation("MainWindow: PdfReportService created");
                
                // Set static instance for dialogs to access
                Instance = this;
                
                // Setup timer for status updates
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _timer.Tick += Timer_Tick;
                _timer.Start();
                _logger.LogInformation("MainWindow: Timer started");

                // Subscribe to controller events
                _controller.EmployeesUpdated += OnEmployeesUpdated;
                _controller.RolesUpdated += OnRolesUpdated;
                _controller.ShiftGroupsUpdated += OnShiftGroupsUpdated;
                _controller.AbsencesUpdated += OnDailyPreviewDataUpdated;
                _controller.ShiftGroupsUpdated += OnDailyPreviewDataUpdated;
                _logger.LogInformation("MainWindow: Controller events subscribed");
                
                // Initialize settings display
                UpdateSettingsDisplay();
                _controller.ShiftsUpdated += OnShiftsUpdated;
                _controller.AbsencesUpdated += OnAbsencesUpdated;
                _controller.AbsencesUpdated += LoadAbsenceLists; // Refresh absence lists when absences change
                _controller.TasksUpdated += OnTasksUpdated;
                _controller.SettingsUpdated += OnSettingsUpdated;
                _controller.SyncTriggered += OnSyncTriggered;
                _logger.LogInformation("MainWindow: Settings display updated");

                // Initialize UI
                InitializeUI();
                _logger.LogInformation("MainWindow: UI initialized");
                
                LoadData();
                _logger.LogInformation("MainWindow: Data loaded");

                // Debug data persistence issues
                _controller.DebugDataPersistence();

                _logger.LogInformation("MainWindow initialized");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MainWindow: Error during initialization");
                MessageBox.Show($"خطا در راه‌اندازی پنجره اصلی:\n\n{ex.Message}\n\nجزئیات:\n{ex}", 
                    "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                throw; // Re-throw to let App.xaml.cs handle it
            }
        }

        private void InitializeUI()
        {
            try
            {
                // Current date is bound in XAML
                
                // Initialize task target date to today
                TaskTargetDatePicker.SelectedDate = GeorgianDateHelper.GetCurrentGeorgianDate();
                
                // Initialize report dates
                ReportStartDatePicker.SelectedDate = GeorgianDateHelper.GetCurrentGeorgianDate();
                ReportEndDatePicker.SelectedDate = GeorgianDateHelper.GetCurrentGeorgianDate();
                
                // Initialize assigned person ComboBox
                InitializeAssignedPersonComboBox();
                
                // Initialize role ComboBox
                InitializeRoleComboBox();

                // Initialize daily progress groups
                LoadDailyProgressGroups();
                
                // Set default date to today
                DailyProgressDatePicker.SelectedDate = GeorgianDateHelper.GetCurrentGeorgianDate();
                
                // Subscribe to date picker property changes
                // Note: GeorgianDatePicker doesn't expose SelectedDateChanged event,
                // so we'll reload progress when user changes group/shift type or clicks record button
                // The date change will be picked up when those actions occur
                
                // Load initial progress data
                LoadDailyProgress();
                LoadWeeklyProgress();

                // Setup employee search
                EmployeeSearchBox.GotFocus += (s, e) =>
                {
                    if (EmployeeSearchBox.Text == "جستجو...")
                    {
                        EmployeeSearchBox.Text = "";
                        EmployeeSearchBox.Foreground = Brushes.Black;
                    }
                };

                EmployeeSearchBox.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrEmpty(EmployeeSearchBox.Text))
                    {
                        EmployeeSearchBox.Text = "جستجو...";
                        EmployeeSearchBox.Foreground = Brushes.Gray;
                    }
                };

                EmployeeSearchBox.TextChanged += (s, e) =>
                {
                    if (EmployeeSearchBox.Text != "جستجو...")
                    {
                        FilterEmployees(EmployeeSearchBox.Text);
                    }
                };

                // Setup shift employee search
                ShiftEmployeeSearchBox.GotFocus += (s, e) =>
                {
                    if (ShiftEmployeeSearchBox.Text == "جستجو...")
                    {
                        ShiftEmployeeSearchBox.Text = "";
                        ShiftEmployeeSearchBox.Foreground = Brushes.Black;
                    }
                };

                ShiftEmployeeSearchBox.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrEmpty(ShiftEmployeeSearchBox.Text))
                    {
                        ShiftEmployeeSearchBox.Text = "جستجو...";
                        ShiftEmployeeSearchBox.Foreground = Brushes.Gray;
                    }
                };

                ShiftEmployeeSearchBox.TextChanged += (s, e) =>
                {
                    if (ShiftEmployeeSearchBox.Text != "جستجو...")
                    {
                        FilterShiftEmployees(ShiftEmployeeSearchBox.Text);
                    }
                };

                // Subscribe to LayoutUpdated to re-attach drag handlers when items are regenerated
                // This ensures handlers are attached after tab switches or other UI updates
                ShiftEmployeeListBox.LayoutUpdated += ShiftEmployeeListBox_LayoutUpdated;

                // Initialize rotation configuration
                InitializeRotationConfiguration();

                _logger.LogInformation("UI initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UI");
            }
        }

        private void InitializeAssignedPersonComboBox()
        {
            try
            {
                // Add common management positions
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "مدیر کل" });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "مدیر منابع انسانی" });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "سرپرست شیفت" });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "کارشناس منابع انسانی" });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "مدیر عملیات" });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "معاون مدیر" });
                
                // Add current employees
                var employees = _controller.GetAllEmployees();
                foreach (var employee in employees)
                {
                    ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = employee.FullName });
                }
                
                // Set default selection
                ReportAssignedToComboBox.SelectedIndex = 0;
                
                _logger.LogInformation("Assigned person ComboBox initialized with {Count} items", ReportAssignedToComboBox.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing assigned person ComboBox");
            }
        }

        private void InitializeRoleComboBox()
        {
            try
            {
                var roles = _controller.GetActiveRoles();
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
                
                _logger.LogInformation("Role ComboBox initialized with {Count} items", RoleComboBox.Items.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing role ComboBox");
            }
        }

        private void InitializeRotationConfiguration()
        {
            try
            {
                bool isEnabled = false;
                
                // Check if auto-rotation is already enabled
                if (_controller.Settings.TryGetValue("auto_rotate_shifts", out var autoRotate) && 
                    autoRotate is bool enabled && enabled)
                {
                    isEnabled = true;
                    // Show the expandable section
                    RotationConfigExpander.Visibility = Visibility.Visible;
                    RotationConfigExpander.IsExpanded = false; // Start collapsed, user can expand if needed
                    
                    // Load rotation settings
                    LoadRotationSettings();
                }
                else
                {
                    // Hide the expandable section
                    RotationConfigExpander.Visibility = Visibility.Collapsed;
                    RotationConfigExpander.IsExpanded = false;
                }
                
                // Set checkbox state
                AutoRotateCheckBox.IsChecked = isEnabled;
                
                _logger.LogInformation("Rotation configuration initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing rotation configuration");
            }
        }

        private void LoadData()
        {
            try
            {
                LoadEmployees();
                LoadShifts();
                LoadAbsences();
                LoadTasks();
                UpdateStatus("داده‌ها بارگذاری شدند");
                
                _logger.LogInformation("Data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data");
                UpdateStatus("خطا در بارگذاری داده‌ها");
            }
        }

        #region Employee Management

        private void LoadEmployees()
        {
            try
            {
                _logger.LogInformation("LoadEmployees: Starting to load employees");
                
                if (_controller == null)
                {
                    _logger.LogError("LoadEmployees: Controller is null!");
                    return;
                }
                
                var employees = _controller.GetAllEmployees();
                _logger.LogInformation("LoadEmployees: Retrieved {Count} employees from controller", employees?.Count ?? 0);
                
                if (employees == null)
                {
                    _logger.LogWarning("LoadEmployees: Employees list is null, using empty list");
                    employees = new List<Employee>();
                }
                
                EmployeeListBox.ItemsSource = employees;
                
                // Filter out absent employees for shift assignment
                var todayGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
                var availableEmployees = new List<Employee>();
                
                if (_controller.AbsenceManager != null)
                {
                    availableEmployees = employees.Where(emp => 
                        emp != null && !_controller.AbsenceManager.HasAbsenceForEmployee(emp, todayGeorgian)).ToList();
                }
                else
                {
                    _logger.LogWarning("LoadEmployees: AbsenceManager is null, using all employees");
                    availableEmployees = employees.Where(emp => emp != null).ToList();
                }
                
                _logger.LogInformation("LoadEmployees: {AvailableCount} employees available for shift assignment", availableEmployees.Count);
                ShiftEmployeeListBox.ItemsSource = availableEmployees;
                
                // Attach handlers when containers are generated
                // Use Dispatcher.BeginInvoke to ensure UI is fully rendered
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (ShiftEmployeeListBox.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                        {
                            AttachDragHandlers();
                        }
                        else
                        {
                            // Subscribe to status changed event
                            EventHandler? statusChangedHandler = null;
                            statusChangedHandler = (s, e) =>
                            {
                                if (ShiftEmployeeListBox.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                                {
                                    AttachDragHandlers();
                                    ShiftEmployeeListBox.ItemContainerGenerator.StatusChanged -= statusChangedHandler;
                                }
                            };
                            ShiftEmployeeListBox.ItemContainerGenerator.StatusChanged += statusChangedHandler;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in Dispatcher.BeginInvoke for AttachDragHandlers");
                    }
                }), DispatcherPriority.Loaded);
                
                _logger.LogInformation("LoadEmployees: Completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employees: {Message}", ex.Message);
            }
        }

        private void FilterEmployees(string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query) || query == "جستجو...")
                {
                    LoadEmployees();
                    return;
                }

                var filteredEmployees = _controller.SearchEmployees(query);
                EmployeeListBox.ItemsSource = filteredEmployees;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering employees");
            }
        }

        private void FilterShiftEmployees(string query)
        {
            try
            {
                var todayGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
                
                if (string.IsNullOrEmpty(query) || query == "جستجو...")
                {
                    // Reload all available employees for shift assignment
                    var employees = _controller.GetAllEmployees();
                    var availableEmployees = new List<Employee>();
                    
                    if (_controller.AbsenceManager != null)
                    {
                        availableEmployees = employees.Where(emp => 
                            emp != null && !_controller.AbsenceManager.HasAbsenceForEmployee(emp, todayGeorgian)).ToList();
                    }
                    else
                    {
                        availableEmployees = employees.Where(emp => emp != null).ToList();
                    }
                    
                    ShiftEmployeeListBox.ItemsSource = availableEmployees;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AttachDragHandlers();
                    }), DispatcherPriority.Loaded);
                    return;
                }

                // Filter employees based on search query
                var allEmployees = _controller.GetAllEmployees();
                var lowerQuery = query.ToLower();
                
                var filtered = allEmployees.Where(emp =>
                    emp != null &&
                    (emp.FirstName.ToLower().Contains(lowerQuery) ||
                     emp.LastName.ToLower().Contains(lowerQuery) ||
                     emp.FullName.ToLower().Contains(lowerQuery)) &&
                    (_controller.AbsenceManager == null || 
                     !_controller.AbsenceManager.HasAbsenceForEmployee(emp, todayGeorgian))).ToList();
                
                ShiftEmployeeListBox.ItemsSource = filtered;
                
                // Re-attach handlers after filtering
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    AttachDragHandlers();
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering shift employees");
            }
        }

        private void EmployeeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _selectedEmployee = EmployeeListBox.SelectedItem as Employee;
                if (_selectedEmployee != null)
                {
                    LoadEmployeeDetails(_selectedEmployee);
                    LoadEmployeeAbsences(_selectedEmployee);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling employee selection");
            }
        }

        private void LoadEmployeeDetails(Employee employee)
        {
            try
            {
                FirstNameTextBox.Text = employee.FirstName;
                LastNameTextBox.Text = employee.LastName;
                
                // Set the selected role in ComboBox
                foreach (ComboBoxItem item in RoleComboBox.Items)
                {
                    if (item.Tag?.ToString() == employee.RoleId)
                    {
                        RoleComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                IsManagerCheckBox.IsChecked = employee.IsManager;
                
                // Load employee photo
                var photo = employee.GetPhotoImageSource(200);
                EmployeePhoto.Source = photo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee details");
            }
        }

        private void LoadEmployeeAbsences(Employee employee)
        {
            try
            {
                var absences = _controller.AbsenceManager.GetAbsencesByEmployee(employee);
                AbsenceListBox.ItemsSource = absences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee absences");
            }
        }

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new EmployeeDialog(_controller);
                if (dialog.ShowDialog() == true)
                {
                    var success = _controller.AddEmployee(dialog.FirstName, dialog.LastName, dialog.RoleId, dialog.ShiftGroupId, dialog.PhotoPath, dialog.IsManager,
                                                         dialog.ShieldColor, dialog.ShowShield, dialog.StickerPaths, dialog.MedalBadgePath, dialog.PersonnelId);
                    if (success)
                    {
                        LoadEmployees();
                        UpdateStatus($"کارمند {dialog.FirstName} {dialog.LastName} اضافه شد");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding employee");
                MessageBox.Show($"خطا در افزودن کارمند: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new EmployeeDialog(_selectedEmployee, _controller);
                if (dialog.ShowDialog() == true)
                {
                    var success = _controller.UpdateEmployee(_selectedEmployee.EmployeeId, dialog.FirstName, dialog.LastName, dialog.RoleId, dialog.ShiftGroupId, dialog.PhotoPath, dialog.IsManager,
                                                           dialog.ShieldColor, dialog.ShowShield, dialog.StickerPaths, dialog.MedalBadgePath, dialog.PersonnelId);
                    if (success)
                    {
                        LoadEmployees();
                        LoadEmployeeDetails(_selectedEmployee);
                        UpdateStatus($"کارمند {dialog.FirstName} {dialog.LastName} بروزرسانی شد");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing employee");
                MessageBox.Show($"خطا در ویرایش کارمند: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageRoles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new RoleDialog(_controller);
                dialog.ShowDialog();
                // Roles are automatically saved when modified in the dialog
                UpdateStatus("مدیریت نقش‌ها تکمیل شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing roles");
                MessageBox.Show($"خطا در مدیریت نقش‌ها: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageShiftGroups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ShiftGroupDialog(_controller);
                dialog.ShowDialog();
                // Shift groups are automatically saved when modified in the dialog
                UpdateStatus("مدیریت گروه‌های شیفت تکمیل شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing shift groups");
                MessageBox.Show($"خطا در مدیریت گروه‌های شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("DeleteEmployee_Click: Starting employee deletion");
                
                if (_selectedEmployee == null)
                {
                    _logger.LogWarning("DeleteEmployee_Click: No employee selected");
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _logger.LogInformation("DeleteEmployee_Click: Selected employee: {FullName} (ID: {EmployeeId})", 
                    _selectedEmployee.FullName, _selectedEmployee.EmployeeId);

                var result = MessageBox.Show($"آیا از حذف کارمند {_selectedEmployee.FullName} اطمینان دارید؟", 
                    "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _logger.LogInformation("DeleteEmployee_Click: User confirmed deletion, calling controller");
                    
                    if (_controller == null)
                    {
                        _logger.LogError("DeleteEmployee_Click: Controller is null!");
                        MessageBox.Show("خطا در کنترلر", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    // Store employee name before deletion to avoid null reference
                    var employeeName = _selectedEmployee.FullName;
                    var employeeId = _selectedEmployee.EmployeeId;
                    
                    var success = _controller.DeleteEmployee(employeeId);
                    if (success)
                    {
                        _logger.LogInformation("DeleteEmployee_Click: Employee deleted successfully, refreshing UI");
                        
                        // Clear selected employee before refreshing UI
                        _selectedEmployee = null;
                        
                        LoadEmployees();
                        UpdateStatus($"کارمند {employeeName} حذف شد");
                    }
                    else
                    {
                        _logger.LogWarning("DeleteEmployee_Click: Controller returned false for deletion");
                    }
                }
                else
                {
                    _logger.LogInformation("DeleteEmployee_Click: User cancelled deletion");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee in UI: {Message}", ex.Message);
                MessageBox.Show($"خطا در حذف کارمند: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportCSV_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    Title = "انتخاب فایل CSV"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var (imported, skipped) = _controller.ImportEmployeesFromCsv(openFileDialog.FileName);
                    LoadEmployees();
                    UpdateStatus($"{imported} کارمند وارد شد، {skipped} کارمند نادیده گرفته شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing CSV");
                MessageBox.Show($"خطا در وارد کردن CSV: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
                    Title = "انتخاب عکس کارمند"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Detect name and personnel ID from filename (format: FirstName_LastName_PersonnelId.ext)
                    var (detectedFirstName, detectedLastName) = _controller.DetectNameFromFolder(openFileDialog.FileName);
                    var detectedPersonnelId = _controller.DetectPersonnelIdFromFilename(openFileDialog.FileName);
                    
                    string? updatePersonnelId = null;
                    
                    // If personnel ID detected and different from current, ask user if they want to update
                    if (detectedPersonnelId != null)
                    {
                        if (string.IsNullOrEmpty(_selectedEmployee.PersonnelId))
                        {
                            // Auto-fill if empty
                            updatePersonnelId = detectedPersonnelId;
                        }
                        else if (_selectedEmployee.PersonnelId != detectedPersonnelId)
                        {
                            var result = MessageBox.Show(
                                $"کد پرسنلی تشخیص داده شده از نام فایل: {detectedPersonnelId}\nآیا می‌خواهید کد پرسنلی کارمند را به‌روزرسانی کنید؟",
                                "تشخیص کد پرسنلی",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            
                            if (result == MessageBoxResult.Yes)
                            {
                                updatePersonnelId = detectedPersonnelId;
                            }
                        }
                    }
                    
                    // If name detected and different from current, ask user if they want to update
                    if (detectedFirstName != null && detectedLastName != null)
                    {
                        if (detectedFirstName != _selectedEmployee.FirstName || detectedLastName != _selectedEmployee.LastName)
                        {
                            var result = MessageBox.Show(
                                $"نام تشخیص داده شده از پوشه: {detectedFirstName} {detectedLastName}\nآیا می‌خواهید نام کارمند را به‌روزرسانی کنید؟",
                                "تشخیص نام از پوشه",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            
                            if (result == MessageBoxResult.Yes)
                            {
                                _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                                    firstName: detectedFirstName, 
                                    lastName: detectedLastName, 
                                    photoPath: openFileDialog.FileName,
                                    personnelId: updatePersonnelId);
                            }
                            else
                            {
                                _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                                    photoPath: openFileDialog.FileName,
                                    personnelId: updatePersonnelId);
                            }
                        }
                        else
                        {
                            _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                                photoPath: openFileDialog.FileName,
                                personnelId: updatePersonnelId);
                        }
                    }
                    else
                    {
                        // Update employee photo path (will be copied to employee images folder automatically)
                        _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                            photoPath: openFileDialog.FileName,
                            personnelId: updatePersonnelId);
                    }
                    
                    LoadEmployees();
                    LoadEmployeeDetails(_selectedEmployee);
                    UpdateStatus("عکس کارمند بروزرسانی شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting photo");
                MessageBox.Show($"خطا در انتخاب عکس: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateBadge_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate employee has photo
                if (string.IsNullOrEmpty(_selectedEmployee.PhotoPath) || !_selectedEmployee.HasPhoto())
                {
                    MessageBox.Show("کارمند انتخاب شده عکس ندارد. لطفاً ابتدا عکس کارمند را انتخاب کنید.", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Generate badge
                var badgePath = _controller.GenerateEmployeeBadge(_selectedEmployee.EmployeeId);

                if (!string.IsNullOrEmpty(badgePath))
                {
                    MessageBox.Show($"کارت شناسایی با موفقیت تولید شد:\n{badgePath}", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStatus("کارت شناسایی تولید شد");
                    
                    // Optionally display the generated badge
                    try
                    {
                        if (File.Exists(badgePath))
                        {
                            var badgeBitmap = new BitmapImage();
                            badgeBitmap.BeginInit();
                            badgeBitmap.UriSource = new Uri(badgePath, UriKind.Absolute);
                            badgeBitmap.CacheOption = BitmapCacheOption.OnLoad;
                            badgeBitmap.EndInit();
                            badgeBitmap.Freeze();
                            EmployeePhoto.Source = badgeBitmap;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not display generated badge in preview");
                    }
                }
                else
                {
                    MessageBox.Show("خطا در تولید کارت شناسایی. لطفاً مطمئن شوید که قالب کارت شناسایی در مسیر صحیح قرار دارد.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating badge");
                MessageBox.Show($"خطا در تولید کارت شناسایی: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveEmployeeChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedRoleId = (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "employee";
                var success = _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                    FirstNameTextBox.Text, LastNameTextBox.Text, selectedRoleId, _selectedEmployee.ShiftGroupId, null, IsManagerCheckBox.IsChecked);
                
                if (success)
                {
                    LoadEmployees();
                    UpdateStatus("تغییرات کارمند ذخیره شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving employee changes");
                MessageBox.Show($"خطا در ذخیره تغییرات: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MarkAbsent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedType = (AbsenceTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (string.IsNullOrEmpty(selectedType))
                {
                    MessageBox.Show("لطفاً نوع غیبت را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var customDate = AbsenceDatePicker.SelectedDate;
                var success = _controller.MarkEmployeeAbsent(_selectedEmployee, selectedType, AbsenceNotesTextBox.Text, customDate);
                if (success)
                {
                    LoadEmployeeAbsences(_selectedEmployee);
                    LoadEmployees(); // Refresh to update shift availability
                    LoadAbsenceLists(); // Refresh categorized absence lists
                    UpdateStatus($"کارمند {_selectedEmployee.FullName} به عنوان {selectedType} ثبت شد");
                    AbsenceNotesTextBox.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking employee absent");
                MessageBox.Show($"خطا در ثبت غیبت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveAbsence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var success = _controller.RemoveAbsence(_selectedEmployee);
                if (success)
                {
                    LoadEmployeeAbsences(_selectedEmployee);
                    LoadEmployees(); // Refresh to update shift availability
                    LoadAbsenceLists(); // Refresh categorized absence lists
                    UpdateStatus($"غیبت کارمند {_selectedEmployee.FullName} حذف شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing absence");
                MessageBox.Show($"خطا در حذف غیبت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Shift Management

        private void LoadShiftGroups()
        {
            try
            {
                // Preserve current selection if any
                string? previouslySelectedGroupId = null;
                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem currentItem && currentItem.Tag is string currentGroupId)
                {
                    previouslySelectedGroupId = currentGroupId;
                }

                var shiftGroups = _controller.GetAllShiftGroups();
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
                
                // Reselect previous group if possible; otherwise select default
                if (ShiftGroupComboBox.Items.Count > 0)
                {
                    ComboBoxItem? toSelect = null;
                    if (!string.IsNullOrEmpty(previouslySelectedGroupId))
                    {
                        toSelect = ShiftGroupComboBox.Items.Cast<ComboBoxItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == previouslySelectedGroupId);
                    }

                    if (toSelect == null)
                    {
                        toSelect = ShiftGroupComboBox.Items.Cast<ComboBoxItem>()
                            .FirstOrDefault(item => item.Tag?.ToString() == "default")
                            ?? (ComboBoxItem)ShiftGroupComboBox.Items[0];
                    }

                    ShiftGroupComboBox.SelectedItem = toSelect;
                }
                
                _logger.LogInformation("Loaded {Count} shift groups", shiftGroups.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shift groups");
            }
        }


        private void ShiftGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string groupId)
                {
                    _logger.LogInformation("Shift group changed to: {GroupId}", groupId);
                    LoadShiftSlots();
                    UpdateShiftStatistics();
                    
                    // Load auto-rotation setting
                    // Note: AutoRotateCheckBox not yet implemented in XAML
                    // if (_controller.Settings.TryGetValue("auto_rotate_shifts", out var autoRotate) && autoRotate is bool enabled)
                    // {
                    //     AutoRotateCheckBox.IsChecked = enabled;
                    // }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling shift group selection change");
            }
        }

        private void LoadShifts()
        {
            try
            {
                LoadShiftGroups();
                LoadShiftSlots();
                UpdateShiftStatistics();
                
                // Initialize auto-rotate checkbox state from settings
                if (AutoRotateCheckBox != null)
                {
                    if (_controller.Settings.TryGetValue("auto_rotate_shifts", out var autoRotate) && autoRotate is bool enabled)
                    {
                        AutoRotateCheckBox.IsChecked = enabled;
                    }
                    else
                    {
                        AutoRotateCheckBox.IsChecked = false;
                    }
                }
                
                // Load categorized absence lists
                LoadAbsenceLists();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shifts");
            }
        }

        private void LoadShiftSlots()
        {
            try
            {
                // Get the selected shift group
                ShiftGroup? selectedGroup = null;
                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string groupId)
                {
                    selectedGroup = _controller.GetShiftGroup(groupId);
                }
                
                // Fallback to default group if no selection
                if (selectedGroup == null)
                {
                    selectedGroup = _controller.GetShiftGroup("default");
                }
                
                // Use shift group capacities or fallback to default ShiftManager
                int morningCapacity = selectedGroup?.MorningCapacity ?? _controller.ShiftManager.Capacity;
                int afternoonCapacity = selectedGroup?.AfternoonCapacity ?? _controller.ShiftManager.Capacity;
                int nightCapacity = selectedGroup?.NightCapacity ?? _controller.ShiftManager.Capacity;
                
                _logger.LogInformation("LoadShiftSlots: Selected group: {GroupName}, Morning: {MorningCapacity}, Afternoon: {AfternoonCapacity}, Night: {NightCapacity}", 
                    selectedGroup?.Name ?? "Default", morningCapacity, afternoonCapacity, nightCapacity);
                
                // Clear existing slots
                MorningShiftPanel.Children.Clear();
                AfternoonShiftPanel.Children.Clear();
                NightShiftPanel.Children.Clear();

                // Update capacity text box to show morning capacity (primary)
                ShiftCapacityTextBox.Text = morningCapacity.ToString();
                
                // Update supervisor displays
                UpdateSupervisorDisplay("morning");
                UpdateSupervisorDisplay("afternoon");
                UpdateSupervisorDisplay("night");

                // Create morning shift slots in a grid layout
                var morningGrid = CreateShiftGrid("morning", morningCapacity, selectedGroup);
                MorningShiftPanel.Children.Add(morningGrid);

                // Create afternoon shift slots in a grid layout
                var afternoonGrid = CreateShiftGrid("afternoon", afternoonCapacity, selectedGroup);
                AfternoonShiftPanel.Children.Add(afternoonGrid);

                // Create night shift slots in a grid layout
                var nightGrid = CreateShiftGrid("night", nightCapacity, selectedGroup);
                NightShiftPanel.Children.Add(nightGrid);
                
                _logger.LogInformation("LoadShiftSlots: Grids created.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shift slots");
            }
        }

        private Grid CreateShiftGrid(string shiftType, int capacity, ShiftGroup? shiftGroup = null)
        {
            var grid = new Grid();
            
            // Calculate optimal number of columns (aim for 3-4 columns)
            int columns = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(capacity)));
            int rows = (int)Math.Ceiling((double)capacity / columns);

            // Add column definitions
            for (int col = 0; col < columns; col++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Add row definitions
            for (int row = 0; row < rows; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            }

            // Add slots to the grid
            for (int i = 0; i < capacity; i++)
            {
                var slot = CreateShiftSlot(shiftType, i);
                int row = i / columns;
                int col = i % columns;
                
                Grid.SetRow(slot, row);
                Grid.SetColumn(slot, col);
                
                grid.Children.Add(slot);
            }

            return grid;
        }

        private void UpdateSupervisorDisplay(string shiftType)
        {
            try
            {
                // Get the supervisor content panel
                StackPanel? supervisorContent = null;
                
                switch (shiftType)
                {
                    case "morning":
                        supervisorContent = MorningSupervisorContent;
                        break;
                    case "afternoon":
                        supervisorContent = AfternoonSupervisorContent;
                        break;
                    case "night":
                        supervisorContent = NightSupervisorContent;
                        break;
                }

                if (supervisorContent == null)
                    return;

                // Clear existing content
                supervisorContent.Children.Clear();

                // Get current group ID
                string? groupId = null;
                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                {
                    groupId = selectedGroupId;
                }

                // Get current supervisor
                var supervisor = _controller.GetTeamLeader(shiftType, groupId);

                if (supervisor != null)
                {
                    // Display supervisor with photo and name
                    var supervisorStackPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Supervisor photo
                    var image = new Image
                    {
                        Width = 60,
                        Height = 60,
                        Source = supervisor.GetPhotoImageSource(60),
                        Stretch = Stretch.UniformToFill,
                        Margin = new Thickness(0, 0, 0, 5),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    // Supervisor name
                    var nameTextBlock = new TextBlock
                    {
                        Text = supervisor.FullName,
                        FontFamily = new FontFamily("Tahoma"),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Foreground = Brushes.DarkBlue,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };

                    supervisorStackPanel.Children.Add(image);
                    supervisorStackPanel.Children.Add(nameTextBlock);
                    supervisorContent.Children.Add(supervisorStackPanel);
                }
                else
                {
                    // Display placeholder
                    var placeholderText = new TextBlock
                    {
                        Text = "هیچ سرپرستی انتخاب نشده\n(کارمند را اینجا بکشید)",
                        FontFamily = new FontFamily("Tahoma"),
                        FontSize = 10,
                        TextAlignment = TextAlignment.Center,
                        Foreground = Brushes.Gray,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    supervisorContent.Children.Add(placeholderText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supervisor display for {ShiftType}", shiftType);
            }
        }

        private Border CreateShiftSlot(string shiftType, int slotIndex)
        {
            var border = new Border
            {
                Width = 120,
                Height = 120,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2),
                Background = Brushes.LightGray,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Get employee from selected shift group
            Employee? employee = null;
            if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string groupId)
            {
                var selectedGroup = _controller.GetShiftGroup(groupId);
                if (selectedGroup != null)
                {
                    Shift? shift = null;
                    switch (shiftType)
                    {
                        case "morning":
                            shift = selectedGroup.MorningShift;
                            break;
                        case "afternoon":
                            shift = selectedGroup.AfternoonShift;
                            break;
                        case "night":
                            shift = selectedGroup.NightShift;
                            break;
                    }
                    employee = shift?.GetEmployeeAtSlot(slotIndex);
                }
            }
            
            // Fallback to default ShiftManager if no group selected
            if (employee == null)
            {
                employee = _controller.ShiftManager.GetShift(shiftType)?.GetEmployeeAtSlot(slotIndex);
            }
            
            // Note: Absent employees are now properly removed from shift assignments
            // in the MarkEmployeeAbsent method, so no need to hide them here
            
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5)
            };

            if (employee != null)
            {
                // Wrap employee content in a draggable container
                var employeeContainer = new Border
                {
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent,
                    Padding = new Thickness(0)
                };

                // Employee photo
                var image = new Image
                {
                    Width = 60,
                    Height = 60,
                    Source = employee.GetPhotoImageSource(60),
                    Stretch = Stretch.UniformToFill,
                    Margin = new Thickness(0, 0, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Employee name
                var nameTextBlock = new TextBlock
                {
                    Text = $"{employee.FirstName}\n{employee.LastName}",
                    FontFamily = new FontFamily("Tahoma"),
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 100,
                    Foreground = Brushes.DarkBlue,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var employeeStackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                employeeStackPanel.Children.Add(image);
                employeeStackPanel.Children.Add(nameTextBlock);

                employeeContainer.Child = employeeStackPanel;

                // Store employee in Tag for drag operations (shift context is passed via method parameters)
                employeeContainer.Tag = employee;

                // Add drag handlers to enable dragging employee back to list
                employeeContainer.PreviewMouseLeftButtonDown += (s, e) => ShiftSlotEmployee_PreviewMouseLeftButtonDown(s, e, employee, shiftType, slotIndex);
                employeeContainer.MouseMove += (s, e) => ShiftSlotEmployee_MouseMove(s, e, employee, shiftType, slotIndex);

                stackPanel.Children.Add(employeeContainer);
            }
            else
            {
                // Empty slot
                var emptyTextBlock = new TextBlock
                {
                    Text = $"جایگاه\n{slotIndex + 1}",
                    FontFamily = new FontFamily("Tahoma"),
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Brushes.Gray
                };

                stackPanel.Children.Add(emptyTextBlock);
            }

            border.Child = stackPanel;

            // Enable drop functionality
            border.AllowDrop = true;
            border.DragOver += (s, e) => Slot_DragOver(s, e, shiftType, slotIndex);
            border.DragLeave += (s, e) => Slot_DragLeave(s, e, shiftType, slotIndex);
            border.Drop += (s, e) => Slot_Drop(s, e, shiftType, slotIndex);
            
            // Add click functionality for easier assignment
            border.MouseLeftButtonUp += (s, e) => Slot_Click(s, e, shiftType, slotIndex);
            
            // Add right-click functionality to remove employee
            border.MouseRightButtonUp += (s, e) => Slot_RightClick(s, e, shiftType, slotIndex);

            return border;
        }

        private void UpdateShiftStatistics()
        {
            try
            {
                // Get the selected shift group
                ShiftGroup? selectedGroup = null;
                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string groupId)
                {
                    selectedGroup = _controller.GetShiftGroup(groupId);
                }
                
                // Fallback to default group if no selection
                if (selectedGroup == null)
                {
                    selectedGroup = _controller.GetShiftGroup("default");
                }
                
                // Use shift group data or fallback to default ShiftManager
                int morningCount, afternoonCount, nightCount, morningCapacity, afternoonCapacity, nightCapacity;
                
                if (selectedGroup != null)
                {
                    morningCount = selectedGroup.MorningShift.AssignedEmployees.Count(emp => emp != null);
                    afternoonCount = selectedGroup.AfternoonShift.AssignedEmployees.Count(emp => emp != null);
                    nightCount = selectedGroup.NightShift.AssignedEmployees.Count(emp => emp != null);
                    morningCapacity = selectedGroup.MorningCapacity;
                    afternoonCapacity = selectedGroup.AfternoonCapacity;
                    nightCapacity = selectedGroup.NightCapacity;
                }
                else
                {
                    morningCount = _controller.ShiftManager.MorningShift.AssignedEmployees.Count(emp => emp != null);
                    afternoonCount = _controller.ShiftManager.AfternoonShift.AssignedEmployees.Count(emp => emp != null);
                    nightCount = _controller.ShiftManager.NightShift.AssignedEmployees.Count(emp => emp != null);
                    morningCapacity = _controller.ShiftManager.Capacity;
                    afternoonCapacity = _controller.ShiftManager.Capacity;
                    nightCapacity = _controller.ShiftManager.Capacity;
                }

                MorningShiftStats.Text = $"{morningCount}/{morningCapacity}";
                AfternoonShiftStats.Text = $"{afternoonCount}/{afternoonCapacity}";
                NightShiftStats.Text = $"{nightCount}/{nightCapacity}";
                
                _logger.LogInformation("Updated shift stats");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shift statistics");
            }
        }

        private void ChangeCapacity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(ShiftCapacityTextBox.Text, out int newCapacity) && newCapacity > 0)
                {
                    _logger.LogInformation("ChangeCapacity_Click: Changing capacity from {CurrentCapacity} to {NewCapacity}", 
                        _controller.ShiftManager.Capacity, newCapacity);
                    
                    _controller.SetShiftCapacity(newCapacity);
                    
                    _logger.LogInformation("ChangeCapacity_Click: After SetShiftCapacity, capacity is {Capacity}", 
                        _controller.ShiftManager.Capacity);
                    
                    LoadShiftSlots();
                    UpdateShiftStatistics();
                    UpdateStatus($"ظرفیت شیفت به {newCapacity} تغییر یافت");
                }
                else
                {
                    MessageBox.Show("لطفاً یک عدد صحیح مثبت وارد کنید", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing shift capacity");
                MessageBox.Show($"خطا در تغییر ظرفیت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearMorningShift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("آیا از پاک کردن شیفت صبح اطمینان دارید؟", 
                    "تأیید پاک کردن", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Get the selected group ID
                    string? groupId = null;
                    if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                    {
                        groupId = selectedGroupId;
                    }
                    
                    _controller.ClearShift("morning", groupId);
                    LoadShiftSlots();
                    UpdateShiftStatistics();
                    UpdateStatus("شیفت صبح پاک شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing morning shift");
                MessageBox.Show($"خطا در پاک کردن شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearAfternoonShift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("آیا از پاک کردن شیفت عصر اطمینان دارید؟", 
                    "تأیید پاک کردن", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Get the selected group ID
                    string? groupId = null;
                    if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                    {
                        groupId = selectedGroupId;
                    }
                    
                    _controller.ClearShift("afternoon", groupId);
                    LoadShiftSlots();
                    UpdateShiftStatistics();
                    UpdateStatus("شیفت عصر پاک شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing afternoon shift");
                MessageBox.Show($"خطا در پاک کردن شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearNightShift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("آیا از پاک کردن شیفت شب اطمینان دارید؟", 
                    "تأیید پاک کردن", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Get the selected group ID
                    string? groupId = null;
                    if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                    {
                        groupId = selectedGroupId;
                    }
                    
                    _controller.ClearShift("night", groupId);
                    LoadShiftSlots();
                    UpdateShiftStatistics();
                    UpdateStatus("شیفت شب پاک شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing night shift");
                MessageBox.Show($"خطا در پاک کردن شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwapShifts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? groupId = null;
                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                {
                    groupId = selectedGroupId;
                }

                var result = MessageBox.Show("آیا از جابجایی شیفت‌های صبح و عصر اطمینان دارید؟", 
                    "تأیید جابجایی", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    if (_controller.SwapShifts(groupId))
                    {
                        LoadShiftSlots();
                        UpdateShiftStatistics();
                        UpdateStatus("شیفت‌ها با موفقیت جابجا شدند");
                    }
                    else
                    {
                        MessageBox.Show("خطا در جابجایی شیفت‌ها", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error swapping shifts");
                MessageBox.Show($"خطا در جابجایی شیفت‌ها: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void AutoRotateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Store auto-rotation setting
                _controller.Settings["auto_rotate_shifts"] = true;
                
                // Set default day if not already set
                if (!_controller.Settings.ContainsKey("auto_rotate_day"))
                {
                    _controller.Settings["auto_rotate_day"] = "Saturday";
                }
                
                _controller.NotifySettingsUpdated();
                _controller.SaveData();
                
                // Show and configure the expandable section
                RotationConfigExpander.Visibility = Visibility.Visible;
                RotationConfigExpander.IsExpanded = true;
                
                // Load rotation settings into UI
                LoadRotationSettings();
                
                UpdateStatus("جابجایی خودکار شیفت‌ها فعال شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling auto-rotation");
            }
        }

        private void AutoRotateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                _controller.Settings["auto_rotate_shifts"] = false;
                _controller.NotifySettingsUpdated();
                _controller.SaveData();
                
                // Hide the expandable section
                RotationConfigExpander.Visibility = Visibility.Collapsed;
                RotationConfigExpander.IsExpanded = false;
                
                UpdateStatus("جابجایی خودکار شیفت‌ها غیرفعال شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling auto-rotation");
            }
        }

        private void RotationTypeRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Weekly rotation is the only option for now
            // This can be extended in the future for other rotation types
            UpdateRotationConfigurationUI();
        }

        private void RotationDayComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (RotationDayComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string dayName)
                {
                    _controller.Settings["auto_rotate_day"] = dayName;
                    _controller.NotifySettingsUpdated();
                    _controller.SaveData();
                    
                    UpdateRotationConfigurationUI();
                    UpdateStatus($"روز چرخش به {selectedItem.Content} تغییر یافت");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing rotation day");
            }
        }

        private void LoadRotationSettings()
        {
            try
            {
                // Initialize day combo box with Persian day names
                RotationDayComboBox.Items.Clear();
                
                // Mapping: Persian day name -> English day name (for storage)
                var dayMapping = new Dictionary<string, string>
                {
                    { "شنبه", "Saturday" },
                    { "یکشنبه", "Sunday" },
                    { "دوشنبه", "Monday" },
                    { "سه‌شنبه", "Tuesday" },
                    { "چهارشنبه", "Wednesday" },
                    { "پنج‌شنبه", "Thursday" },
                    { "جمعه", "Friday" }
                };
                
                var currentDay = _controller.Settings.GetValueOrDefault("auto_rotate_day", "Saturday").ToString() ?? "Saturday";
                
                foreach (var day in dayMapping)
                {
                    var item = new ComboBoxItem
                    {
                        Content = day.Key,
                        Tag = day.Value
                    };
                    RotationDayComboBox.Items.Add(item);
                    
                    // Select the current day
                    if (day.Value == currentDay)
                    {
                        RotationDayComboBox.SelectedItem = item;
                    }
                }
                
                // If no selection was made, select Saturday as default
                if (RotationDayComboBox.SelectedItem == null && RotationDayComboBox.Items.Count > 0)
                {
                    RotationDayComboBox.SelectedIndex = 0; // Saturday
                }
                
                UpdateRotationConfigurationUI();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rotation settings");
            }
        }

        private void UpdateRotationConfigurationUI()
        {
            try
            {
                if (!_controller.Settings.TryGetValue("auto_rotate_shifts", out var autoRotate) || 
                    !(autoRotate is bool enabled && enabled))
                {
                    return;
                }
                
                var rotationDay = _controller.Settings.GetValueOrDefault("auto_rotate_day", "Saturday").ToString() ?? "Saturday";
                
                // Get Persian day name for display
                var dayMapping = new Dictionary<string, string>
                {
                    { "Saturday", "شنبه" },
                    { "Sunday", "یکشنبه" },
                    { "Monday", "دوشنبه" },
                    { "Tuesday", "سه‌شنبه" },
                    { "Wednesday", "چهارشنبه" },
                    { "Thursday", "پنج‌شنبه" },
                    { "Friday", "جمعه" }
                };
                
                var persianDayName = dayMapping.GetValueOrDefault(rotationDay, "شنبه");
                
                // Update schedule info
                RotationScheduleInfo.Text = $"شیفت‌ها به صورت خودکار هر هفته در روز {persianDayName} جابجا می‌شوند.";
                
                // Calculate and display next rotation date using controller method
                var nextRotationDate = _controller.GetNextRotationDate();
                if (nextRotationDate.HasValue)
                {
                    var shamsiDate = ShamsiDateHelper.ToShamsiString(nextRotationDate.Value);
                    var formattedDate = ShamsiDateHelper.FormatForDisplay(shamsiDate);
                    NextRotationDate.Text = $"چرخش بعدی: {formattedDate} ({persianDayName})";
                }
                else
                {
                    NextRotationDate.Text = "تاریخ چرخش بعدی محاسبه نشد";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rotation configuration UI");
            }
        }


        #endregion

        #region Drag and Drop

        private Point _dragStartPoint;
        private Employee? _draggedEmployee;
        private string? _draggedShiftType;
        private string? _draggedGroupId;
        private int _draggedSlotIndex = -1;

        private bool _isAttachingHandlers = false;
        
        private void AttachDragHandlers()
        {
            // Prevent infinite loops from LayoutUpdated events
            if (_isAttachingHandlers)
                return;
                
            try
            {
                _isAttachingHandlers = true;
                
                for (int i = 0; i < ShiftEmployeeListBox.Items.Count; i++)
                {
                    if (ShiftEmployeeListBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
                    {
                        var employee = item.DataContext as Employee;
                        if (employee != null)
                        {
                            // Find the Border in the template using VisualTreeHelper
                            Border? border = FindVisualChild<Border>(item);
                            
                            // Find the Image directly within the ListBoxItem (more reliable than checking Border name)
                            Image? image = FindVisualChild<Image>(item);
                            
                            if (border != null)
                            {
                                border.Tag = employee;
                                
                                // Set image source for employee photo only if not already set
                                if (image != null && image.Source == null)
                                {
                                    try
                                    {
                                        var imageSource = EmployeePhotoConverter.Convert(employee, typeof(ImageSource), null, System.Globalization.CultureInfo.CurrentCulture) as ImageSource;
                                        image.Source = imageSource;
                                    }
                                    catch
                                    {
                                        // Silently handle errors - placeholder will show
                                    }
                                }
                                
                                // Remove existing handlers to prevent duplicates
                                border.PreviewMouseLeftButtonDown -= EmployeeItem_PreviewMouseLeftButtonDown;
                                border.MouseMove -= EmployeeItem_MouseMove;
                                border.MouseEnter -= EmployeeItem_MouseEnter;
                                border.MouseLeave -= EmployeeItem_MouseLeave;
                                
                                // Attach drag event handlers to Border
                                border.PreviewMouseLeftButtonDown += EmployeeItem_PreviewMouseLeftButtonDown;
                                border.MouseMove += EmployeeItem_MouseMove;
                                border.MouseEnter += EmployeeItem_MouseEnter;
                                border.MouseLeave += EmployeeItem_MouseLeave;
                            }
                            else
                            {
                                // Fallback: attach to ListBoxItem if Border not found
                                item.Tag = employee;
                                item.PreviewMouseLeftButtonDown -= ListBoxItem_PreviewMouseLeftButtonDown;
                                item.PreviewMouseLeftButtonDown += ListBoxItem_PreviewMouseLeftButtonDown;
                                item.MouseMove -= ListBoxItem_MouseMove;
                                item.MouseMove += ListBoxItem_MouseMove;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attaching drag handlers");
            }
            finally
            {
                _isAttachingHandlers = false;
            }
        }

        private void ShiftEmployeeListBox_LayoutUpdated(object? sender, EventArgs e)
        {
            try
            {
                // Re-attach handlers whenever layout is updated (e.g., after tab switches)
                // This ensures handlers are always attached when items are regenerated
                if (ShiftEmployeeListBox.ItemContainerGenerator.Status == 
                    System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    // Use BeginInvoke to ensure this happens after layout is complete
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AttachDragHandlers();
                    }), DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShiftEmployeeListBox_LayoutUpdated");
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // When Employee Management tab (index 0) is selected, load absence lists
                if (MainTabControl.SelectedIndex == 0) // Employee management tab
                {
                    LoadAbsenceLists();
                }
                // When shift management tab (index 1) is selected, ensure handlers are attached
                else if (MainTabControl.SelectedIndex == 1) // Shift management tab
                {
                    _logger.LogInformation("MainTabControl: Shift management tab selected, ensuring drag handlers are attached");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (ShiftEmployeeListBox.ItemContainerGenerator.Status == 
                            System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                        {
                            AttachDragHandlers();
                        }
                        else
                        {
                            // Subscribe to status changed event if containers aren't ready yet
                            EventHandler? statusChangedHandler = null;
                            statusChangedHandler = (s, ev) =>
                            {
                                if (ShiftEmployeeListBox.ItemContainerGenerator.Status == 
                                    System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                                {
                                    AttachDragHandlers();
                                    ShiftEmployeeListBox.ItemContainerGenerator.StatusChanged -= statusChangedHandler;
                                }
                            };
                            ShiftEmployeeListBox.ItemContainerGenerator.StatusChanged += statusChangedHandler;
                        }
                    }), DispatcherPriority.Loaded);
                }
                // When daily preview tab is selected, update the preview
                else if (MainTabControl.SelectedItem is TabItem selectedTab && 
                         selectedTab.Header?.ToString() == "پیش‌نمایش روزانه")
                {
                    UpdateDailyPreview();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MainTabControl_SelectionChanged");
            }
        }
        
        private void ShiftEmployeeListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            
            var listBox = sender as ListBox;
            if (listBox == null) return;
            
            var hitTestResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitTestResult?.VisualHit == null) return;
            
            // Find the ListBoxItem that was clicked
            DependencyObject? current = hitTestResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item)
                {
                    var employee = item.DataContext as Employee ?? item.Tag as Employee;
                    if (employee != null)
                    {
                        _dragStartPoint = e.GetPosition(null);
                        _draggedEmployee = employee;
                        return;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        
        private void ShiftEmployeeListBox_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed && _draggedEmployee != null)
                {
                    var currentPoint = e.GetPosition(null);
                    var diff = _dragStartPoint - currentPoint;

                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        var dragData = new DataObject(typeof(Employee), _draggedEmployee);
                        var result = DragDrop.DoDragDrop(ShiftEmployeeListBox, dragData, DragDropEffects.Move);
                        _draggedEmployee = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShiftEmployeeListBox_MouseMove");
            }
        }

        private void ShiftEmployeeListBox_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // Reset visual feedback first
                if (sender is ListBox listBox)
                {
                    listBox.BorderBrush = null;
                    listBox.BorderThickness = new Thickness(0);
                    
                    var parent = VisualTreeHelper.GetParent(listBox);
                    while (parent != null)
                    {
                        if (parent is Border border)
                        {
                            border.BorderBrush = Brushes.Gray;
                            border.BorderThickness = new Thickness(1);
                            border.Background = Brushes.White;
                            break;
                        }
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                }
                
                // Check if this is an employee being dragged from a shift slot
                if (e.Data.GetDataPresent(typeof(Employee)))
                {
                    var employee = e.Data.GetData(typeof(Employee)) as Employee;
                    if (employee != null)
                    {
                        // Try to get shift context from drag data
                        string? shiftType = null;
                        string? groupId = null;
                        
                        if (e.Data.GetDataPresent("ShiftType"))
                        {
                            shiftType = e.Data.GetData("ShiftType")?.ToString();
                        }
                        
                        if (e.Data.GetDataPresent("GroupId"))
                        {
                            groupId = e.Data.GetData("GroupId")?.ToString();
                        }
                        
                        // If shift context is not in drag data, try to find it from controller
                        if (string.IsNullOrEmpty(shiftType))
                        {
                            // Query controller to find which shift the employee is assigned to
                            var allGroups = _controller.GetAllShiftGroups();
                            foreach (var group in allGroups)
                            {
                                if (group.MorningShift.IsEmployeeAssigned(employee))
                                {
                                    shiftType = "morning";
                                    groupId = group.GroupId;
                                    break;
                                }
                                else if (group.AfternoonShift.IsEmployeeAssigned(employee))
                                {
                                    shiftType = "afternoon";
                                    groupId = group.GroupId;
                                    break;
                                }
                                else if (group.NightShift.IsEmployeeAssigned(employee))
                                {
                                    shiftType = "night";
                                    groupId = group.GroupId;
                                    break;
                                }
                            }
                            
                            // Fallback to default shift manager
                            if (string.IsNullOrEmpty(shiftType))
                            {
                                if (_controller.ShiftManager.MorningShift.IsEmployeeAssigned(employee))
                                {
                                    shiftType = "morning";
                                }
                                else if (_controller.ShiftManager.AfternoonShift.IsEmployeeAssigned(employee))
                                {
                                    shiftType = "afternoon";
                                }
                                else if (_controller.ShiftManager.NightShift.IsEmployeeAssigned(employee))
                                {
                                    shiftType = "night";
                                }
                            }
                        }
                        
                        // Remove employee from shift if we found the shift type
                        if (!string.IsNullOrEmpty(shiftType))
                        {
                            var success = _controller.RemoveEmployeeFromShift(employee, shiftType, groupId);
                            if (success)
                            {
                                LoadShiftSlots();
                                UpdateShiftStatistics();
                                LoadEmployees(); // Refresh employee lists
                                UpdateStatus($"کارمند {employee.FullName} از شیفت {shiftType} حذف شد و به لیست بازگشت");
                            }
                            else
                            {
                                UpdateStatus($"خطا در حذف کارمند {employee.FullName} از شیفت");
                            }
                        }
                        else
                        {
                            // Employee is not in a shift - check if they're in an absence list
                            var todayGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
                            var absences = _controller.AbsenceManager.GetAbsencesByEmployee(employee);
                            var todayAbsence = absences.FirstOrDefault(a => a.Date == todayGeorgian);
                            
                            if (todayAbsence != null)
                            {
                                // Remove the absence
                                var success = _controller.AbsenceManager.RemoveAbsence(todayAbsence);
                                if (success)
                                {
                                    _controller.SaveData();
                                    LoadAbsenceLists(); // Refresh absence lists
                                    LoadEmployees(); // Refresh employee lists
                                    UpdateStatus($"{employee.FullName} به لیست اصلی بازگردانده شد");
                                }
                                else
                                {
                                    UpdateStatus($"خطا در بازگرداندن {employee.FullName}");
                                }
                            }
                            else
                            {
                                // If no specific absence found, try removing all absences for today
                                var success = _controller.RemoveAbsence(employee);
                                if (success)
                                {
                                    LoadAbsenceLists();
                                    LoadEmployees();
                                    UpdateStatus($"{employee.FullName} به لیست اصلی بازگردانده شد");
                                }
                                else
                                {
                                    UpdateStatus($"کارمند {employee.FullName} در هیچ شیفتی یافت نشد و غیبتی ندارد");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShiftEmployeeListBox_Drop");
                MessageBox.Show($"خطا در بازگرداندن کارمند به لیست: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShiftEmployeeListBox_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                // Check if this is an employee being dragged from a shift slot (has shift context)
                if (e.Data.GetDataPresent(typeof(Employee)) && e.Data.GetDataPresent("ShiftType"))
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                    
                    // Visual feedback: highlight the list box border
                    if (sender is ListBox listBox)
                    {
                        // Try to find the parent Border or GroupBox for visual feedback
                        var parent = VisualTreeHelper.GetParent(listBox);
                        while (parent != null)
                        {
                            if (parent is Border border)
                            {
                                border.BorderBrush = Brushes.DodgerBlue;
                                border.BorderThickness = new Thickness(2);
                                border.Background = new SolidColorBrush(Color.FromArgb(50, 173, 216, 230));
                                break;
                            }
                            if (parent is GroupBox groupBox)
                            {
                                // GroupBox doesn't have easy border styling, so we'll use the listbox itself
                                listBox.BorderBrush = Brushes.DodgerBlue;
                                listBox.BorderThickness = new Thickness(2);
                                break;
                            }
                            parent = VisualTreeHelper.GetParent(parent);
                        }
                    }
                }
                else if (e.Data.GetDataPresent(typeof(Employee)))
                {
                    // Employee from absence list or other source - allow with visual feedback
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                    
                    // Visual feedback: highlight the list box border for absence list drags
                    if (sender is ListBox listBox)
                    {
                        // Try to find the parent Border or GroupBox for visual feedback
                        var parent = VisualTreeHelper.GetParent(listBox);
                        while (parent != null)
                        {
                            if (parent is Border border)
                            {
                                border.BorderBrush = Brushes.LightGreen;
                                border.BorderThickness = new Thickness(2);
                                border.Background = new SolidColorBrush(Color.FromArgb(50, 144, 238, 144));
                                break;
                            }
                            if (parent is GroupBox groupBox)
                            {
                                // GroupBox doesn't have easy border styling, so we'll use the listbox itself
                                listBox.BorderBrush = Brushes.LightGreen;
                                listBox.BorderThickness = new Thickness(2);
                                break;
                            }
                            parent = VisualTreeHelper.GetParent(parent);
                        }
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShiftEmployeeListBox_DragEnter");
            }
        }

        private void ShiftEmployeeListBox_DragLeave(object sender, DragEventArgs e)
        {
            try
            {
                // Reset visual feedback
                if (sender is ListBox listBox)
                {
                    listBox.BorderBrush = null;
                    listBox.BorderThickness = new Thickness(0);
                    
                    // Reset parent border if found
                    var parent = VisualTreeHelper.GetParent(listBox);
                    while (parent != null)
                    {
                        if (parent is Border border)
                        {
                            border.BorderBrush = Brushes.Gray;
                            border.BorderThickness = new Thickness(1);
                            border.Background = Brushes.White;
                            break;
                        }
                        parent = VisualTreeHelper.GetParent(parent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShiftEmployeeListBox_DragLeave");
            }
        }

        #region Supervisor Drag & Drop

        private void SupervisorArea_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(typeof(Employee)))
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SupervisorArea_DragOver");
            }
        }

        private void SupervisorArea_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(typeof(Employee)) && sender is Border border)
                {
                    // Highlight the supervisor area
                    border.Background = new SolidColorBrush(Color.FromArgb(150, 25, 118, 210)); // Darker blue with transparency
                    border.BorderBrush = Brushes.DarkBlue;
                    border.BorderThickness = new Thickness(3);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SupervisorArea_DragEnter");
            }
        }

        private void SupervisorArea_DragLeave(object sender, DragEventArgs e)
        {
            try
            {
                if (sender is Border border)
                {
                    // Reset visual feedback
                    border.Background = new SolidColorBrush(Color.FromArgb(255, 227, 242, 253)); // Original light blue
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 25, 118, 210)); // Original blue
                    border.BorderThickness = new Thickness(2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SupervisorArea_DragLeave");
            }
        }

        private void SupervisorArea_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // Reset visual feedback first
                if (sender is Border border)
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(255, 227, 242, 253));
                    border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 25, 118, 210));
                    border.BorderThickness = new Thickness(2);
                }

                // Get shift type from Tag
                if (sender is Border supervisorBorder && supervisorBorder.Tag is string shiftType)
                {
                    // Extract employee from drag data
                    if (e.Data.GetDataPresent(typeof(Employee)))
                    {
                        var employee = e.Data.GetData(typeof(Employee)) as Employee;
                        if (employee != null)
                        {
                            // Get current group ID
                            string? groupId = null;
                            if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                            {
                                groupId = selectedGroupId;
                            }

                            // Get current supervisor
                            var currentSupervisor = _controller.GetTeamLeader(shiftType, groupId);

                            // If there's a current supervisor and they're assigned to the shift, remove them
                            if (currentSupervisor != null)
                            {
                                // Check if current supervisor is assigned to this shift
                                var selectedGroup = _controller.GetShiftGroup(groupId ?? "default");
                                if (selectedGroup != null)
                                {
                                    var shift = shiftType switch
                                    {
                                        "morning" => selectedGroup.MorningShift,
                                        "afternoon" => selectedGroup.AfternoonShift,
                                        "night" => selectedGroup.NightShift,
                                        _ => null
                                    };
                                    if (shift != null && shift.IsEmployeeAssigned(currentSupervisor))
                                    {
                                        // Remove current supervisor from shift
                                        var success = _controller.RemoveEmployeeFromShift(currentSupervisor, shiftType, groupId);
                                        if (success)
                                        {
                                            _logger.LogInformation("Removed previous supervisor {SupervisorName} from {ShiftType} shift", 
                                                currentSupervisor.FullName, shiftType);
                                        }
                                    }
                                }
                            }

                            // Set new supervisor
                            var setSuccess = _controller.SetTeamLeader(shiftType, employee.EmployeeId, groupId);
                            if (setSuccess)
                            {
                                // Check if new supervisor is assigned to shift
                                var selectedGroup = _controller.GetShiftGroup(groupId ?? "default");
                                if (selectedGroup != null)
                                {
                                    var shift = shiftType switch
                                    {
                                        "morning" => selectedGroup.MorningShift,
                                        "afternoon" => selectedGroup.AfternoonShift,
                                        "night" => selectedGroup.NightShift,
                                        _ => null
                                    };
                                    if (shift != null && !shift.IsEmployeeAssigned(employee))
                                    {
                                        // Automatically add supervisor to shift if not already assigned
                                        var assignResult = _controller.AssignEmployeeToShift(employee, shiftType, null, groupId);
                                        if (assignResult.Success)
                                        {
                                            _logger.LogInformation("Automatically added supervisor {SupervisorName} to {ShiftType} shift", 
                                                employee.FullName, shiftType);
                                        }
                                        else if (assignResult.Conflict != null)
                                        {
                                            // For supervisor assignment, we'll handle conflicts silently or show a message
                                            string? targetGroupName = groupId != null ? _controller.ShiftGroupManager.GetShiftGroup(groupId)?.Name : "پیش‌فرض";
                                            var dialogResult = ShowAssignmentConflictDialog(assignResult.Conflict, employee, targetGroupName ?? "پیش‌فرض", shiftType);
                                            if (dialogResult == MessageBoxResult.Yes)
                                            {
                                                var removed = _controller.RemoveEmployeeFromPreviousAssignment(employee, assignResult.Conflict, groupId);
                                                if (removed)
                                                {
                                                    var retryResult = _controller.AssignEmployeeToShift(employee, shiftType, null, groupId);
                                                    if (retryResult.Success)
                                                    {
                                                        _logger.LogInformation("Automatically added supervisor {SupervisorName} to {ShiftType} shift after conflict resolution", 
                                                            employee.FullName, shiftType);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                // Refresh UI
                                UpdateSupervisorDisplay(shiftType);
                                LoadShiftSlots();
                                LoadEmployees();
                                UpdateShiftStatistics();
                                UpdateStatus($"کارمند {employee.FullName} به عنوان سرپرست شیفت {shiftType} انتخاب شد");
                            }
                            else
                            {
                                UpdateStatus($"خطا در انتخاب سرپرست برای شیفت {shiftType}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SupervisorArea_Drop");
                MessageBox.Show($"خطا در انتخاب سرپرست: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Assignment Conflict Resolution

        private MessageBoxResult ShowAssignmentConflictDialog(AssignmentConflict conflict, Employee employee, string targetGroupName, string targetShiftType)
        {
            string message;
            string title = "تایید تخصیص";

            var targetShiftName = targetShiftType == "morning" ? "صبح" : targetShiftType == "evening" ? "عصر" : targetShiftType;

            switch (conflict.Type)
            {
                case ConflictType.Absent:
                    message = $"کارمند {employee.FullName} به عنوان {conflict.AbsenceType} ثبت شده است.\n\nآیا می‌خواهید غیبت را حذف کرده و کارمند را به گروه {targetGroupName} (شیفت {targetShiftName}) تخصیص دهید؟";
                    break;

                case ConflictType.DifferentShift:
                    var currentShiftName = conflict.CurrentShiftType == "morning" ? "صبح" : conflict.CurrentShiftType == "evening" ? "عصر" : conflict.CurrentShiftType;
                    message = $"کارمند {employee.FullName} قبلاً به شیفت {currentShiftName} در این گروه تخصیص داده شده است.\n\nآیا می‌خواهید از شیفت قبلی حذف شده و به شیفت {targetShiftName} تخصیص داده شود؟";
                    break;

                case ConflictType.DifferentGroup:
                    message = $"کارمند {employee.FullName} قبلاً به گروه {conflict.CurrentGroupName} تخصیص داده شده است.\n\nآیا می‌خواهید از گروه قبلی حذف شده و به گروه {targetGroupName} (شیفت {targetShiftName}) تخصیص داده شود؟";
                    break;

                default:
                    message = $"آیا می‌خواهید کارمند {employee.FullName} را به گروه {targetGroupName} (شیفت {targetShiftName}) تخصیص دهید؟";
                    break;
            }

            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        }

        private void HandleAssignmentResult(AssignmentResult result, Employee employee, string shiftType, int? slotIndex, string? groupId, Action? onSuccess = null)
        {
            if (result.Success)
            {
                // Success - proceed with existing logic
                if (onSuccess != null)
                {
                    onSuccess();
                }
                else
                {
                    LoadShiftSlots();
                    UpdateShiftStatistics();
                    LoadEmployees();
                    LoadAbsenceLists();
                    UpdateStatus($"کارمند {employee.FullName} به شیفت {shiftType} تخصیص داده شد");
                }
            }
            else if (result.Conflict != null)
            {
                // Conflict detected - show confirmation dialog
                string? targetGroupName = null;
                if (groupId != null)
                {
                    var targetGroup = _controller.ShiftGroupManager.GetShiftGroup(groupId);
                    targetGroupName = targetGroup?.Name ?? groupId;
                }
                else
                {
                    targetGroupName = "پیش‌فرض";
                }

                var dialogResult = ShowAssignmentConflictDialog(result.Conflict, employee, targetGroupName, shiftType);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    // User confirmed - remove from previous assignment and retry
                    var removed = _controller.RemoveEmployeeFromPreviousAssignment(employee, result.Conflict, groupId);
                    if (removed)
                    {
                        // Retry assignment
                        var retryResult = _controller.AssignEmployeeToShift(employee, shiftType, slotIndex, groupId);
                        if (retryResult.Success)
                        {
                            if (onSuccess != null)
                            {
                                onSuccess();
                            }
                            else
                            {
                                LoadShiftSlots();
                                UpdateShiftStatistics();
                                LoadEmployees();
                                LoadAbsenceLists();
                                UpdateStatus($"کارمند {employee.FullName} به شیفت {shiftType} تخصیص داده شد");
                            }
                        }
                        else
                        {
                            UpdateStatus($"خطا در تخصیص کارمند {employee.FullName} به شیفت");
                        }
                    }
                    else
                    {
                        UpdateStatus($"خطا در حذف تخصیص قبلی کارمند {employee.FullName}");
                    }
                }
                // If user clicked No, do nothing
            }
            else if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                // Error occurred
                UpdateStatus($"خطا: {result.ErrorMessage}");
            }
        }

        #endregion

        #region Absence Management Lists

        private List<Employee> GetEmployeesByAbsenceCategory(string category, string date)
        {
            try
            {
                var absences = _controller.AbsenceManager.GetAbsencesByCategory(category);
                var employees = absences
                    .Where(a => a.Date == date)
                    .Select(a => a.Employee)
                    .Distinct()
                    .ToList();
                return employees;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees by absence category {Category}", category);
                return new List<Employee>();
            }
        }

        private void LoadAbsenceLists()
        {
            try
            {
                var todayGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();

                // Load Absent employees (غایب)
                var absentEmployees = GetEmployeesByAbsenceCategory("غایب", todayGeorgian);
                AbsentEmployeesListBox.ItemsSource = absentEmployees;
                AbsentEmployeesExpander.Header = $"کارمندان غایب ({absentEmployees.Count})";

                // Load Sick employees (بیمار)
                var sickEmployees = GetEmployeesByAbsenceCategory("بیمار", todayGeorgian);
                SickEmployeesListBox.ItemsSource = sickEmployees;
                SickEmployeesExpander.Header = $"کارمندان بیمار ({sickEmployees.Count})";

                // Load Leave employees (مرخصی)
                var leaveEmployees = GetEmployeesByAbsenceCategory("مرخصی", todayGeorgian);
                LeaveEmployeesListBox.ItemsSource = leaveEmployees;
                LeaveEmployeesExpander.Header = $"کارمندان مرخصی ({leaveEmployees.Count})";

                // Update Employee Management section lists
                EmployeeManagementAbsentListBox.ItemsSource = absentEmployees;
                EmployeeManagementAbsentExpander.Header = $"کارمندان غایب ({absentEmployees.Count})";

                EmployeeManagementSickListBox.ItemsSource = sickEmployees;
                EmployeeManagementSickExpander.Header = $"کارمندان بیمار ({sickEmployees.Count})";

                EmployeeManagementLeaveListBox.ItemsSource = leaveEmployees;
                EmployeeManagementLeaveExpander.Header = $"کارمندان مرخصی ({leaveEmployees.Count})";

                _logger.LogInformation("Loaded absence lists - Absent: {AbsentCount}, Sick: {SickCount}, Leave: {LeaveCount}", 
                    absentEmployees.Count, sickEmployees.Count, leaveEmployees.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading absence lists");
            }
        }

        private void AbsenceEmployeeListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is ListBox listBox)
                {
                    var item = GetItemFromListBox(listBox, e.GetPosition(listBox));
                    if (item != null && listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                    {
                        if (listBoxItem.DataContext is Employee employee)
                        {
                            _dragStartPoint = e.GetPosition(null);
                            _draggedEmployee = employee;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AbsenceEmployeeListBox_PreviewMouseLeftButtonDown");
            }
        }

        private void AbsenceEmployeeListBox_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed && _draggedEmployee != null)
                {
                    var currentPoint = e.GetPosition(null);
                    var diff = _dragStartPoint - currentPoint;

                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        var dragData = new DataObject(typeof(Employee), _draggedEmployee);
                        var result = DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
                        _draggedEmployee = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AbsenceEmployeeListBox_MouseMove");
            }
        }

        private void AbsenceEmployeeListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Only process if this is a simple click (not part of a drag operation)
                // If _draggedEmployee is null, it means a drag operation occurred (was cleared in MouseMove)
                // If _draggedEmployee is not null, it means no drag occurred, so process the click
                if (_draggedEmployee == null)
                {
                    // A drag operation occurred, ignore the click
                    return;
                }

                if (sender is ListBox listBox)
                {
                    var item = GetItemFromListBox(listBox, e.GetPosition(listBox));
                    if (item != null && item is Employee employee && employee == _draggedEmployee)
                    {
                        // Clear the dragged employee flag
                        var clickedEmployee = _draggedEmployee;
                        _draggedEmployee = null;

                        // Show confirmation dialog
                        var result = MessageBox.Show(
                            $"آیا می‌خواهید {clickedEmployee.FullName} را به لیست اصلی بازگردانید؟",
                            "بازگرداندن به لیست اصلی",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Get today's date
                            var todayGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
                            
                            // Find and remove today's absence for this employee
                            var absences = _controller.AbsenceManager.GetAbsencesByEmployee(clickedEmployee);
                            var todayAbsence = absences.FirstOrDefault(a => a.Date == todayGeorgian);
                            
                            if (todayAbsence != null)
                            {
                                var success = _controller.AbsenceManager.RemoveAbsence(todayAbsence);
                                if (success)
                                {
                                    // Save data and refresh UI
                                    _controller.SaveData();
                                    
                                    // Refresh lists
                                    LoadAbsenceLists();
                                    LoadEmployees();
                                    
                                    UpdateStatus($"{clickedEmployee.FullName} به لیست اصلی بازگردانده شد");
                                }
                                else
                                {
                                    UpdateStatus($"خطا در بازگرداندن {clickedEmployee.FullName}");
                                }
                            }
                            else
                            {
                                // If no specific absence found, try removing all absences for today
                                var success = _controller.RemoveAbsence(clickedEmployee);
                                if (success)
                                {
                                    LoadAbsenceLists();
                                    LoadEmployees();
                                    UpdateStatus($"{clickedEmployee.FullName} به لیست اصلی بازگردانده شد");
                                }
                                else
                                {
                                    UpdateStatus($"غیبت امروز برای {clickedEmployee.FullName} یافت نشد");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Clear the dragged employee flag if clicked item doesn't match
                        _draggedEmployee = null;
                    }
                }
                else
                {
                    // Clear the dragged employee flag
                    _draggedEmployee = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AbsenceEmployeeListBox_MouseLeftButtonUp");
                MessageBox.Show($"خطا در بازگرداندن کارمند: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                _draggedEmployee = null; // Clear on error
            }
        }

        private object? GetItemFromListBox(ListBox listBox, Point point)
        {
            try
            {
                var hit = VisualTreeHelper.HitTest(listBox, point);
                if (hit != null)
                {
                    var depObj = hit.VisualHit;
                    while (depObj != null && depObj != listBox)
                    {
                        if (depObj is ListBoxItem item)
                        {
                            return item.DataContext;
                        }
                        depObj = VisualTreeHelper.GetParent(depObj);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetItemFromListBox");
            }
            return null;
        }

        #endregion

        #region Employee List Drag Handlers

        private void EmployeeListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is ListBox listBox)
                {
                    var item = GetItemFromListBox(listBox, e.GetPosition(listBox));
                    if (item != null && item is Employee employee)
                    {
                        _dragStartPoint = e.GetPosition(null);
                        _draggedEmployee = employee;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EmployeeListBox_PreviewMouseLeftButtonDown");
            }
        }

        private void EmployeeListBox_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed && _draggedEmployee != null)
                {
                    var currentPoint = e.GetPosition(null);
                    var diff = _dragStartPoint - currentPoint;

                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        var dragData = new DataObject(typeof(Employee), _draggedEmployee);
                        dragData.SetData("SourceList", "EmployeeList"); // Mark the source
                        var result = DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
                        _draggedEmployee = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EmployeeListBox_MouseMove");
            }
        }

        private void EmployeeListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Clear the dragged employee when mouse button is released without a drag
                _draggedEmployee = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EmployeeListBox_MouseLeftButtonUp");
            }
        }

        #endregion

        #region Absence List Drop Handlers

        private void AbsentEmployeesListBox_Drop(object sender, DragEventArgs e)
        {
            HandleAbsenceListDrop(e, "غایب", "غایب");
        }

        private void SickEmployeesListBox_Drop(object sender, DragEventArgs e)
        {
            HandleAbsenceListDrop(e, "بیمار", "بیمار");
        }

        private void LeaveEmployeesListBox_Drop(object sender, DragEventArgs e)
        {
            HandleAbsenceListDrop(e, "مرخصی", "مرخصی");
        }

        private void HandleAbsenceListDrop(DragEventArgs e, string category, string categoryDisplay)
        {
            try
            {
                if (e.Data.GetDataPresent(typeof(Employee)))
                {
                    var employee = e.Data.GetData(typeof(Employee)) as Employee;
                    if (employee != null)
                    {
                        // Check if employee is already in an absence list for today
                        var todayGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
                        var existingAbsences = _controller.AbsenceManager.GetAbsencesByEmployee(employee);
                        var todayAbsence = existingAbsences.FirstOrDefault(a => a.Date == todayGeorgian);
                        
                        if (todayAbsence != null)
                        {
                            UpdateStatus($"{employee.FullName} قبلاً به عنوان {todayAbsence.Category} ثبت شده است");
                            return;
                        }

                        var success = _controller.MarkEmployeeAbsent(employee, category);
                        if (success)
                        {
                            LoadAbsenceLists();
                            LoadEmployees();
                            LoadShiftSlots();
                            UpdateShiftStatistics();
                            UpdateStatus($"{employee.FullName} به عنوان {categoryDisplay} ثبت شد");
                        }
                        else
                        {
                            UpdateStatus($"خطا در ثبت {employee.FullName} به عنوان {categoryDisplay}");
                        }
                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandleAbsenceListDrop for category {Category}", category);
                MessageBox.Show($"خطا در ثبت غیبت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AbsentEmployeesListBox_DragEnter(object sender, DragEventArgs e)
        {
            HandleAbsenceListDragEnter(sender, e, Brushes.IndianRed);
        }

        private void SickEmployeesListBox_DragEnter(object sender, DragEventArgs e)
        {
            HandleAbsenceListDragEnter(sender, e, Brushes.Orange);
        }

        private void LeaveEmployeesListBox_DragEnter(object sender, DragEventArgs e)
        {
            HandleAbsenceListDragEnter(sender, e, Brushes.MediumPurple);
        }

        private void HandleAbsenceListDragEnter(object sender, DragEventArgs e, Brush highlightColor)
        {
            try
            {
                if (e.Data.GetDataPresent(typeof(Employee)))
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;

                    // Visual feedback: highlight the list box
                    if (sender is ListBox listBox)
                    {
                        listBox.BorderBrush = highlightColor;
                        listBox.BorderThickness = new Thickness(2);
                        listBox.Background = new SolidColorBrush(Color.FromArgb(30, 
                            ((SolidColorBrush)highlightColor).Color.R,
                            ((SolidColorBrush)highlightColor).Color.G,
                            ((SolidColorBrush)highlightColor).Color.B));
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandleAbsenceListDragEnter");
            }
        }

        private void AbsentEmployeesListBox_DragLeave(object sender, DragEventArgs e)
        {
            ResetAbsenceListBoxVisual(sender as ListBox);
        }

        private void SickEmployeesListBox_DragLeave(object sender, DragEventArgs e)
        {
            ResetAbsenceListBoxVisual(sender as ListBox);
        }

        private void LeaveEmployeesListBox_DragLeave(object sender, DragEventArgs e)
        {
            ResetAbsenceListBoxVisual(sender as ListBox);
        }

        private void ResetAbsenceListBoxVisual(ListBox? listBox)
        {
            try
            {
                if (listBox != null)
                {
                    listBox.BorderBrush = null;
                    listBox.BorderThickness = new Thickness(0);
                    listBox.Background = Brushes.Transparent;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetAbsenceListBoxVisual");
            }
        }

        #endregion
        
        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.Tag is Employee employee)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedEmployee = employee;
            }
        }
        
        private void ListBoxItem_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (sender is ListBoxItem item && item.Tag is Employee employee)
                {
                    if (e.LeftButton == MouseButtonState.Pressed && _draggedEmployee == employee)
                    {
                        var currentPoint = e.GetPosition(null);
                        var diff = _dragStartPoint - currentPoint;

                        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                        {
                            var dragData = new DataObject(typeof(Employee), employee);
                            var result = DragDrop.DoDragDrop(item, dragData, DragDropEffects.Move);
                            _draggedEmployee = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ListBoxItem mouse move");
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void EmployeeItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Employee employee)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedEmployee = employee;
            }
        }

        private void EmployeeItem_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (sender is Border border && border.Tag is Employee employee)
                {
                    if (e.LeftButton == MouseButtonState.Pressed && _draggedEmployee == employee)
                    {
                        var currentPoint = e.GetPosition(null);
                        var diff = _dragStartPoint - currentPoint;

                        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                        {
                            var dragData = new DataObject(typeof(Employee), employee);
                            var result = DragDrop.DoDragDrop(border, dragData, DragDropEffects.Move);
                            _draggedEmployee = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee mouse move");
            }
        }

        private void EmployeeItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215));
                border.BorderBrush = Brushes.DodgerBlue;
            }
        }

        private void EmployeeItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = Brushes.White;
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));
            }
        }

        private void Employee_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e, Employee employee)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedEmployee = employee;
        }

        private void Employee_MouseMove(object sender, MouseEventArgs e, Employee employee)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed && _draggedEmployee == employee)
                {
                    var currentPoint = e.GetPosition(null);
                    var diff = _dragStartPoint - currentPoint;

                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        var dragData = new DataObject(typeof(Employee), employee);
                        DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
                        _draggedEmployee = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee mouse move");
            }
        }

        private void Employee_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(typeof(Employee)))
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee drag over");
            }
        }

        private void ShiftSlotEmployee_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e, Employee employee, string shiftType, int slotIndex)
        {
            try
            {
                if (employee != null)
                {
                    _dragStartPoint = e.GetPosition(null);
                    _draggedEmployee = employee;
                    _draggedShiftType = shiftType;
                    _draggedSlotIndex = slotIndex;
                    
                    // Get the current group ID
                    if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string groupId)
                    {
                        _draggedGroupId = groupId;
                    }
                    else
                    {
                        _draggedGroupId = "default";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in shift slot employee preview mouse left button down");
            }
        }

        private void ShiftSlotEmployee_MouseMove(object sender, MouseEventArgs e, Employee employee, string shiftType, int slotIndex)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed && _draggedEmployee != null && _draggedEmployee == employee && _draggedShiftType == shiftType && _draggedSlotIndex == slotIndex)
                {
                    var currentPoint = e.GetPosition(null);
                    var diff = _dragStartPoint - currentPoint;

                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        // Create drag data with employee and shift context
                        var dragData = new DataObject();
                        dragData.SetData(typeof(Employee), _draggedEmployee);
                        dragData.SetData("ShiftType", _draggedShiftType ?? shiftType);
                        dragData.SetData("GroupId", _draggedGroupId ?? "default");
                        dragData.SetData("SlotIndex", _draggedSlotIndex);
                        
                        var result = DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
                        
                        // Reset drag state
                        _draggedEmployee = null;
                        _draggedShiftType = null;
                        _draggedGroupId = null;
                        _draggedSlotIndex = -1;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in shift slot employee mouse move");
            }
        }

        private void Slot_DragOver(object sender, DragEventArgs e, string shiftType, int slotIndex)
        {
            try
            {
                if (sender is Border border)
                {
                    if (e.Data.GetDataPresent(typeof(Employee)))
                    {
                        e.Effects = DragDropEffects.Move;
                        border.Background = Brushes.LightBlue;
                        border.BorderBrush = Brushes.DodgerBlue;
                        border.BorderThickness = new Thickness(2);
                    }
                    else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        if (files != null && files.Length > 0)
                        {
                            var file = files[0];
                            var ext = Path.GetExtension(file).ToLower();
                            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                            {
                                e.Effects = DragDropEffects.Copy;
                                border.Background = Brushes.LightGreen;
                                border.BorderBrush = Brushes.Green;
                                border.BorderThickness = new Thickness(2);
                            }
                            else
                            {
                                e.Effects = DragDropEffects.None;
                                border.Background = Brushes.LightGray;
                                border.BorderBrush = Brushes.Gray;
                                border.BorderThickness = new Thickness(1);
                            }
                        }
                        else
                        {
                            e.Effects = DragDropEffects.None;
                            border.Background = Brushes.LightGray;
                            border.BorderBrush = Brushes.Gray;
                            border.BorderThickness = new Thickness(1);
                        }
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                        border.Background = Brushes.LightGray;
                        border.BorderBrush = Brushes.Gray;
                        border.BorderThickness = new Thickness(1);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slot drag over");
            }
        }

        private void Slot_DragLeave(object sender, DragEventArgs e, string shiftType, int slotIndex)
        {
            try
            {
                if (sender is Border border)
                {
                    // Reset background and border to default
                    border.Background = Brushes.LightGray;
                    border.BorderBrush = Brushes.Gray;
                    border.BorderThickness = new Thickness(1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slot drag leave");
            }
        }

        private void Slot_Drop(object sender, DragEventArgs e, string shiftType, int slotIndex)
        {
            try
            {
                (sender as Border)!.Background = Brushes.LightGray;

                if (e.Data.GetDataPresent(typeof(Employee)))
                {
                    var employee = e.Data.GetData(typeof(Employee)) as Employee;
                    if (employee != null)
                    {
                        // Get the selected group ID
                        string? groupId = null;
                        if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                        {
                            groupId = selectedGroupId;
                        }
                        
                        var result = _controller.AssignEmployeeToShift(employee, shiftType, slotIndex, groupId);
                        HandleAssignmentResult(result, employee, shiftType, slotIndex, groupId, () =>
                        {
                            LoadShiftSlots();
                            UpdateShiftStatistics();
                            LoadEmployees(); // Refresh employee lists
                            LoadAbsenceLists(); // Refresh absence lists
                            UpdateStatus($"کارمند {employee.FullName} به شیفت {shiftType} تخصیص داده شد");
                        });
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Handle photo file drop - detect employee name from folder or create new employee
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        var file = files[0];
                        var ext = Path.GetExtension(file).ToLower();
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                        {
                            // Try to detect employee name and personnel ID from filename (format: FirstName_LastName_PersonnelId.ext)
                            var (detectedFirstName, detectedLastName) = _controller.DetectNameFromFolder(file);
                            var detectedPersonnelId = _controller.DetectPersonnelIdFromFilename(file);
                            
                            if (detectedFirstName != null && detectedLastName != null)
                            {
                                // Find employee by name
                                var employee = _controller.GetAllEmployees()
                                    .FirstOrDefault(e => e.FirstName == detectedFirstName && e.LastName == detectedLastName);
                                
                                if (employee != null)
                                {
                                    // Update employee photo and personnel ID if detected, then assign to shift
                                    _controller.UpdateEmployee(employee.EmployeeId, 
                                        photoPath: file,
                                        personnelId: detectedPersonnelId);
                                    
                                    string? groupId = null;
                                    if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                                    {
                                        groupId = selectedGroupId;
                                    }
                                    
                                    var result = _controller.AssignEmployeeToShift(employee, shiftType, slotIndex, groupId);
                                    HandleAssignmentResult(result, employee, shiftType, slotIndex, groupId, () =>
                                    {
                                        LoadShiftSlots();
                                        UpdateShiftStatistics();
                                        LoadEmployees();
                                        UpdateStatus($"عکس کارمند {employee.FullName} به‌روزرسانی شد و به شیفت تخصیص داده شد");
                                    });
                                }
                                else
                                {
                                    // Create new employee automatically from folder name
                                    var dialogResult = MessageBox.Show(
                                        $"کارمند {detectedFirstName} {detectedLastName} یافت نشد.\nآیا می‌خواهید کارمند جدیدی با این نام ایجاد شود؟",
                                        "ایجاد کارمند جدید",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);
                                    
                                    if (dialogResult == MessageBoxResult.Yes)
                                    {
                                        var newEmployee = _controller.AddEmployee(
                                            detectedFirstName, 
                                            detectedLastName, 
                                            photoPath: file,
                                            personnelId: detectedPersonnelId ?? "");
                                        
                                        if (newEmployee)
                                        {
                                            // Find the newly created employee
                                            var createdEmployee = _controller.GetAllEmployees()
                                                .FirstOrDefault(e => e.FirstName == detectedFirstName && e.LastName == detectedLastName);
                                            
                                            if (createdEmployee != null)
                                            {
                                                string? groupId = null;
                                                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                                                {
                                                    groupId = selectedGroupId;
                                                }
                                                
                                                var assignmentResult = _controller.AssignEmployeeToShift(createdEmployee, shiftType, slotIndex, groupId);
                                                HandleAssignmentResult(assignmentResult, createdEmployee, shiftType, slotIndex, groupId, () =>
                                                {
                                                    LoadShiftSlots();
                                                    UpdateShiftStatistics();
                                                    LoadEmployees();
                                                    UpdateStatus($"کارمند جدید {detectedFirstName} {detectedLastName} ایجاد شد و به شیفت تخصیص داده شد");
                                                });
                                            }
                                        }
                                    }
                                    else
                                    {
                                        UpdateStatus($"کارمند {detectedFirstName} {detectedLastName} یافت نشد.");
                                    }
                                }
                            }
                            else
                            {
                                UpdateStatus("نام کارمند از نام فایل تشخیص داده نشد. لطفاً نام فایل را به فرمت FirstName_LastName_PersonnelId.ext تغییر دهید.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in slot drop");
                    MessageBox.Show($"خطا در تخصیص شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
        }

        private void EmployeePhoto_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        var file = files[0];
                        var ext = Path.GetExtension(file).ToLower();
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                        {
                            e.Effects = DragDropEffects.Copy;
                            if (sender is Border border)
                            {
                                border.BorderBrush = Brushes.LightGreen;
                            }
                        }
                        else
                        {
                            e.Effects = DragDropEffects.None;
                        }
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee photo drag over");
            }
        }

        private void EmployeePhoto_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (sender is Border border)
                {
                    border.BorderBrush = Brushes.Gray;
                }

                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        var file = files[0];
                        var ext = Path.GetExtension(file).ToLower();
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                        {
                            // Detect name and personnel ID from filename (format: FirstName_LastName_PersonnelId.ext)
                            var (detectedFirstName, detectedLastName) = _controller.DetectNameFromFolder(file);
                            var detectedPersonnelId = _controller.DetectPersonnelIdFromFilename(file);
                            
                            string? updatePersonnelId = null;
                            
                            // If personnel ID detected and different from current, ask user if they want to update
                            if (detectedPersonnelId != null)
                            {
                                if (string.IsNullOrEmpty(_selectedEmployee.PersonnelId))
                                {
                                    // Auto-fill if empty
                                    updatePersonnelId = detectedPersonnelId;
                                }
                                else if (_selectedEmployee.PersonnelId != detectedPersonnelId)
                                {
                                    var result = MessageBox.Show(
                                        $"کد پرسنلی تشخیص داده شده از نام فایل: {detectedPersonnelId}\nآیا می‌خواهید کد پرسنلی کارمند را به‌روزرسانی کنید؟",
                                        "تشخیص کد پرسنلی",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);
                                    
                                    if (result == MessageBoxResult.Yes)
                                    {
                                        updatePersonnelId = detectedPersonnelId;
                                    }
                                }
                            }
                            
                            if (detectedFirstName != null && detectedLastName != null)
                            {
                                if (detectedFirstName != _selectedEmployee.FirstName || detectedLastName != _selectedEmployee.LastName)
                                {
                                    var result = MessageBox.Show(
                                        $"نام تشخیص داده شده از پوشه: {detectedFirstName} {detectedLastName}\nآیا می‌خواهید نام کارمند را به‌روزرسانی کنید؟",
                                        "تشخیص نام از پوشه",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);
                                    
                                    if (result == MessageBoxResult.Yes)
                                    {
                                        _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                                            firstName: detectedFirstName, 
                                            lastName: detectedLastName, 
                                            photoPath: file,
                                            personnelId: updatePersonnelId);
                                    }
                                    else
                                    {
                                        _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                                            photoPath: file,
                                            personnelId: updatePersonnelId);
                                    }
                                }
                                else
                                {
                                    _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                                        photoPath: file,
                                        personnelId: updatePersonnelId);
                                }
                            }
                            else
                            {
                                // Name could not be detected from folder, just update photo and personnel ID
                                _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                                    photoPath: file,
                                    personnelId: updatePersonnelId);
                            }
                            
                            LoadEmployees();
                            LoadEmployeeDetails(_selectedEmployee); // Refresh employee details view
                            LoadEmployeeDetails(_selectedEmployee);
                            UpdateStatus("عکس کارمند بروزرسانی شد");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee photo drop");
                MessageBox.Show($"خطا در افزودن عکس: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Slot_Click(object sender, MouseButtonEventArgs e, string shiftType, int slotIndex)
        {
            try
            {
                // Get employee from selected shift group
                Employee? employee = null;
                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string groupId)
                {
                    var selectedGroup = _controller.GetShiftGroup(groupId);
                    if (selectedGroup != null)
                    {
                        var shift = shiftType switch
                        {
                            "morning" => selectedGroup.MorningShift,
                            "afternoon" => selectedGroup.AfternoonShift,
                            "night" => selectedGroup.NightShift,
                            _ => null
                        };
                        employee = shift?.GetEmployeeAtSlot(slotIndex);
                    }
                }
                
                // Fallback to default ShiftManager if no group selected
                if (employee == null)
                {
                    employee = _controller.ShiftManager.GetShift(shiftType)?.GetEmployeeAtSlot(slotIndex);
                }
                
                // If slot is empty, show employee selection dialog
                if (employee == null)
                {
                    ShowEmployeeSelectionDialog(shiftType, slotIndex);
                }
                // If slot has an employee, show options dialog
                else
                {
                    var result = MessageBox.Show(
                        $"کارمند {employee.FullName} در این جایگاه قرار دارد.\n\nآیا می‌خواهید کارمند دیگری را جایگزین کنید؟",
                        "جایگزینی کارمند",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        ShowEmployeeSelectionDialog(shiftType, slotIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slot click");
                MessageBox.Show($"خطا در تخصیص شیفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Slot_RightClick(object sender, MouseButtonEventArgs e, string shiftType, int slotIndex)
        {
            try
            {
                var employee = _controller.ShiftManager.GetShift(shiftType)?.GetEmployeeAtSlot(slotIndex);
                
                if (employee != null)
                {
                    var result = MessageBox.Show(
                        $"آیا می‌خواهید کارمند {employee.FullName} را از شیفت {shiftType} حذف کنید؟",
                        "حذف کارمند از شیفت",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // Get the selected group ID
                        string? groupId = null;
                        if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                        {
                            groupId = selectedGroupId;
                        }
                        
                        var success = _controller.RemoveEmployeeFromShift(employee, shiftType, groupId);
                        if (success)
                        {
                            LoadShiftSlots();
                            UpdateShiftStatistics();
                            LoadEmployees(); // Refresh employee lists
                            UpdateStatus($"کارمند {employee.FullName} از شیفت {shiftType} حذف شد");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slot right click");
                MessageBox.Show($"خطا در حذف کارمند: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowEmployeeSelectionDialog(string shiftType, int slotIndex)
        {
            try
            {
                // Show a dialog to select an employee for this slot
                var todayGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
                var availableEmployees = _controller.GetAllEmployees().Where(emp => 
                    !_controller.AbsenceManager.HasAbsenceForEmployee(emp, todayGeorgian)).ToList();

                if (availableEmployees.Count == 0)
                {
                    MessageBox.Show("هیچ کارمند در دسترسی برای تخصیص وجود ندارد", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create a simple selection dialog
                var dialog = new Window
                {
                    Title = $"تخصیص کارمند به {shiftType}",
                    Width = 300,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    FlowDirection = FlowDirection.RightToLeft
                };

                var stackPanel = new StackPanel { Margin = new Thickness(10) };
                
                var label = new Label
                {
                    Content = "کارمند مورد نظر را انتخاب کنید:",
                    FontFamily = new FontFamily("Tahoma"),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stackPanel.Children.Add(label);

                var listBox = new ListBox
                {
                    DisplayMemberPath = "FullName",
                    ItemsSource = availableEmployees,
                    Height = 250,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stackPanel.Children.Add(listBox);

                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                
                var okButton = new Button
                {
                    Content = "تأیید",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(5),
                    IsDefault = true
                };
                okButton.Click += (s, args) =>
                {
                    if (listBox.SelectedItem is Employee selectedEmployee)
                    {
                        // Get the selected group ID
                        string? groupId = null;
                        if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                        {
                            groupId = selectedGroupId;
                        }
                        
                        var result = _controller.AssignEmployeeToShift(selectedEmployee, shiftType, slotIndex, groupId);
                        HandleAssignmentResult(result, selectedEmployee, shiftType, slotIndex, groupId, () =>
                        {
                            LoadShiftSlots();
                            UpdateShiftStatistics();
                            LoadEmployees(); // Refresh employee lists
                            UpdateStatus($"کارمند {selectedEmployee.FullName} به شیفت {shiftType} تخصیص داده شد");
                        });
                    }
                    dialog.Close();
                };

                var cancelButton = new Button
                {
                    Content = "انصراف",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(5),
                    IsCancel = true
                };
                cancelButton.Click += (s, args) => dialog.Close();

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing employee selection dialog");
                MessageBox.Show($"خطا در نمایش دیالوگ انتخاب: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Task Management

        private void LoadAbsences()
        {
            try
            {
                var absences = _controller.GetAllAbsences();
                AbsenceListBox.ItemsSource = absences;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading absences");
            }
        }

        private void LoadTasks()
        {
            try
            {
                _logger.LogInformation("LoadTasks: Starting to load tasks from controller");
                
                var tasks = _controller.GetAllTasks();
                _logger.LogInformation("LoadTasks: Controller returned {Count} tasks", tasks.Count);
                
                if (tasks.Count > 0)
                {
                    _logger.LogInformation("LoadTasks: First task: {TaskTitle} (ID: {TaskId})", tasks[0].Title, tasks[0].TaskId);
                    _logger.LogInformation("LoadTasks: All task titles: {TaskTitles}", 
                        string.Join(", ", tasks.Select(t => t.Title)));
                }
                else
                {
                    _logger.LogWarning("LoadTasks: No tasks found in controller");
                }
                
                // Force UI thread update
                Dispatcher.Invoke(() =>
                {
                    _logger.LogInformation("LoadTasks: Setting TaskListBox.ItemsSource to {Count} tasks on UI thread", tasks.Count);
                    TaskListBox.ItemsSource = null;
                    TaskListBox.ItemsSource = tasks;
                    TaskListBox.UpdateLayout();
                    
                    // Verify the UI was updated
                    var actualCount = TaskListBox.Items.Count;
                    _logger.LogInformation("LoadTasks: TaskListBox.Items.Count is now {Count}", actualCount);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tasks: {Message}", ex.Message);
            }
        }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _selectedTask = TaskListBox.SelectedItem as Shared.Models.Task;
                if (_selectedTask != null)
                {
                    LoadTaskDetails(_selectedTask);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling task selection");
            }
        }

        private void LoadTaskDetails(Shared.Models.Task task)
        {
            try
            {
                _logger.LogInformation("LoadTaskDetails: Loading details for task {TaskId} - {TaskTitle}, Status: {Status} (index: {StatusIndex})", 
                    task.TaskId, task.Title, task.Status, (int)task.Status);
                
                TaskTitleTextBox.Text = task.Title;
                TaskDescriptionTextBox.Text = task.Description;
                
                // Set priority
                TaskPriorityComboBox.SelectedIndex = (int)task.Priority;
                
                TaskEstimatedHoursTextBox.Text = task.EstimatedHours.ToString();
                
                // Set the target date (already in Shamsi format)
                TaskTargetDatePicker.SelectedDate = task.TargetDate;
                
                // Set status
                _logger.LogInformation("LoadTaskDetails: Setting TaskStatusComboBox.SelectedIndex to {StatusIndex}", (int)task.Status);
                TaskStatusComboBox.SelectedIndex = (int)task.Status;
                
                TaskActualHoursTextBox.Text = task.ActualHours.ToString();
                TaskNotesTextBox.Text = task.Notes;
                
                // Load task assignments
                LoadTaskAssignments();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading task details");
            }
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new TaskDialog();
                if (dialog.ShowDialog() == true)
                {
                    var taskId = _controller.AddTask(dialog.Title, dialog.Description, dialog.Priority, dialog.EstimatedHours, dialog.TargetDate);
                    if (!string.IsNullOrEmpty(taskId))
                    {
                        LoadTasks();
                        UpdateStatus($"وظیفه {dialog.Title} اضافه شد");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding task");
                MessageBox.Show($"خطا در افزودن وظیفه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new TaskDialog(_selectedTask);
                if (dialog.ShowDialog() == true)
                {
                    var success = _controller.UpdateTask(_selectedTask.TaskId, dialog.Title, dialog.Description, dialog.Priority, dialog.EstimatedHours, dialog.TargetDate);
                    if (success)
                    {
                        LoadTasks();
                        LoadTaskDetails(_selectedTask);
                        UpdateStatus($"وظیفه {dialog.Title} بروزرسانی شد");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing task");
                MessageBox.Show($"خطا در ویرایش وظیفه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                var result = MessageBox.Show($"آیا از حذف وظیفه {taskTitle} اطمینان دارید؟", 
                    "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    var success = _controller.DeleteTask(taskId);
                    if (success)
                    {
                        LoadTasks();
                        UpdateStatus($"وظیفه {taskTitle} حذف شد");
                        _selectedTask = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task");
                MessageBox.Show($"خطا در حذف وظیفه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveTaskChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var priority = ((ComboBoxItem)TaskPriorityComboBox.SelectedItem)?.Content?.ToString() ?? "متوسط";
                var status = ((ComboBoxItem)TaskStatusComboBox.SelectedItem)?.Content?.ToString() ?? "در انتظار";
                
                double.TryParse(TaskEstimatedHoursTextBox.Text, out double estimatedHours);
                double.TryParse(TaskActualHoursTextBox.Text, out double actualHours);
                
                var targetDate = TaskTargetDatePicker.SelectedDate; // Already in Shamsi format

                var success = _controller.UpdateTask(_selectedTask.TaskId, 
                    TaskTitleTextBox.Text, TaskDescriptionTextBox.Text, priority, estimatedHours, targetDate, status, actualHours, TaskNotesTextBox.Text);
                
                if (success)
                {
                    LoadTasks();
                    UpdateStatus("تغییرات وظیفه ذخیره شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving task changes");
                MessageBox.Show($"خطا در ذخیره تغییرات: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("StartTask_Click method called");
                
                if (_selectedTask == null)
                {
                    _logger.LogWarning("StartTask_Click: No task selected");
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                _logger.LogInformation("StartTask_Click: Selected task: {TaskId} - {TaskTitle}", _selectedTask.TaskId, _selectedTask.Title);

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                _logger.LogInformation("StartTask_Click: Calling UpdateTask with status 'در حال انجام'");
                var success = _controller.UpdateTask(taskId, status: "در حال انجام");
                _logger.LogInformation("StartTask_Click: UpdateTask result: {Success}", success);
                
                if (success)
                {
                    _logger.LogInformation("StartTask_Click: Task updated successfully, refreshing UI");
                    LoadTasks();
                    // Reload task details to show updated status
                    var updatedTask = _controller.GetTask(taskId);
                    if (updatedTask != null)
                    {
                        _logger.LogInformation("StartTask_Click: Reloading task details for task: {TaskId}", taskId);
                        LoadTaskDetails(updatedTask);
                    }
                    else
                    {
                        _logger.LogWarning("StartTask_Click: Could not retrieve updated task: {TaskId}", taskId);
                    }
                    UpdateStatus($"وظیفه {taskTitle} شروع شد");
                    _logger.LogInformation("StartTask_Click: Status updated and method completed successfully");
                    
                    // Debug: Show message to confirm the operation
                    MessageBox.Show($"وظیفه {taskTitle} با موفقیت شروع شد!\nوضعیت: در حال انجام", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogError("StartTask_Click: UpdateTask failed for task: {TaskId}", taskId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting task");
                MessageBox.Show($"خطا در شروع وظیفه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CompleteTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                double.TryParse(TaskActualHoursTextBox.Text, out double actualHours);
                var success = _controller.UpdateTask(taskId, status: "تکمیل شده", actualHours: actualHours, notes: TaskNotesTextBox.Text);
                if (success)
                {
                    LoadTasks();
                    // Reload task details to show updated status
                    var updatedTask = _controller.GetTask(taskId);
                    if (updatedTask != null)
                    {
                        LoadTaskDetails(updatedTask);
                    }
                    UpdateStatus($"وظیفه {taskTitle} تکمیل شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing task");
                MessageBox.Show($"خطا در تکمیل وظیفه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void AddEmployeeToTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ShowEmployeeAssignmentDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing employee assignment dialog");
                MessageBox.Show($"خطا در نمایش دیالوگ تخصیص: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveEmployeeFromTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                if (sender is Button button && button.Tag is string employeeId)
                {
                    var employee = _controller.GetAllEmployees().FirstOrDefault(emp => emp.EmployeeId == employeeId);
                    if (employee != null)
                    {
                        var result = MessageBox.Show($"آیا می‌خواهید {employee.FullName} را از این وظیفه حذف کنید؟", 
                            "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            var success = _controller.RemoveTaskFromEmployee(taskId, employeeId);
                            if (success)
                            {
                                LoadTaskAssignments();
                                UpdateStatus($"{employee.FullName} از وظیفه {taskTitle} حذف شد");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing employee from task");
                MessageBox.Show($"خطا در حذف کارمند از وظیفه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewEmployeeTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ShowEmployeeTasksDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing employee tasks dialog");
                MessageBox.Show($"خطا در نمایش وظایف کارمند: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowEmployeeAssignmentDialog()
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                var availableGroups = _controller.GetActiveShiftGroups();
                
                if (availableGroups.Count == 0)
                {
                    MessageBox.Show("هیچ گروه شیفت فعالی یافت نشد", "اطلاع", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new Window
                {
                    Title = $"تخصیص گروه شیفت به وظیفه: {taskTitle}",
                    Width = 400,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    FlowDirection = FlowDirection.RightToLeft,
                    FontFamily = new System.Windows.Media.FontFamily("Tahoma")
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new Label
                {
                    Content = "گروه شیفت مورد نظر را انتخاب کنید:",
                    FontSize = 12,
                    Margin = new Thickness(10, 10, 10, 5),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(label, 0);
                grid.Children.Add(label);

                var comboBox = new ComboBox
                {
                    DisplayMemberPath = "Name",
                    ItemsSource = availableGroups,
                    Height = 30,
                    Margin = new Thickness(10, 5, 10, 5)
                };
                Grid.SetRow(comboBox, 1);
                grid.Children.Add(comboBox);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10)
                };
                Grid.SetRow(buttonPanel, 2);

                var assignButton = new Button
                {
                    Content = "تخصیص",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(5),
                    IsDefault = true
                };
                assignButton.Click += (s, e) =>
                {
                    if (comboBox.SelectedItem is Shared.Models.ShiftGroup selectedGroup)
                    {
                        var employees = _controller.GetEmployeesFromShiftGroup(selectedGroup.GroupId);
                        if (employees.Count == 0)
                        {
                            MessageBox.Show($"گروه {selectedGroup.Name} هیچ کارمندی ندارد", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var success = _controller.AssignTaskToShiftGroup(taskId, selectedGroup.GroupId);
                        if (success)
                        {
                            LoadTaskAssignments();
                            UpdateStatus($"تمام کارمندان گروه {selectedGroup.Name} ({employees.Count} نفر) به وظیفه {taskTitle} تخصیص داده شدند");
                            dialog.Close();
                        }
                        else
                        {
                            MessageBox.Show($"خطا در تخصیص کارمندان گروه {selectedGroup.Name} به وظیفه", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("لطفاً یک گروه شیفت را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };

                var cancelButton = new Button
                {
                    Content = "انصراف",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(5),
                    IsCancel = true
                };
                cancelButton.Click += (s, e) => dialog.Close();

                buttonPanel.Children.Add(assignButton);
                buttonPanel.Children.Add(cancelButton);
                grid.Children.Add(buttonPanel);

                dialog.Content = grid;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee assignment dialog");
                MessageBox.Show($"خطا در دیالوگ تخصیص: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowEmployeeTasksDialog()
        {
            try
            {
                var dialog = new Window
                {
                    Title = "وظایف کارمندان",
                    Width = 600,
                    Height = 500,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    FlowDirection = FlowDirection.RightToLeft,
                    FontFamily = new System.Windows.Media.FontFamily("Tahoma")
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new Label
                {
                    Content = "وظایف تخصیص داده شده به کارمندان:",
                    FontSize = 12,
                    Margin = new Thickness(10, 10, 10, 5),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(label, 0);
                grid.Children.Add(label);

                var dataGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    Height = 350,
                    Margin = new Thickness(10, 5, 10, 5),
                    IsReadOnly = true
                };

                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "کارمند",
                    Binding = new Binding("EmployeeName"),
                    Width = 150
                });

                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "وظیفه",
                    Binding = new Binding("Title"),
                    Width = 200
                });

                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "وضعیت",
                    Binding = new Binding("Status"),
                    Width = 100
                });

                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "اولویت",
                    Binding = new Binding("Priority"),
                    Width = 80
                });

                // Get all tasks with their assigned employees
                var taskAssignments = new List<dynamic>();
                foreach (var task in _controller.GetAllTasks())
                {
                    foreach (var employeeId in task.AssignedEmployees)
                    {
                        var employee = _controller.GetAllEmployees().FirstOrDefault(emp => emp.EmployeeId == employeeId);
                        if (employee != null)
                        {
                            taskAssignments.Add(new
                            {
                                EmployeeName = employee.FullName,
                                Title = task.Title,
                                Status = task.Status.ToString(),
                                Priority = task.Priority.ToString()
                            });
                        }
                    }
                }

                dataGrid.ItemsSource = taskAssignments;
                Grid.SetRow(dataGrid, 1);
                grid.Children.Add(dataGrid);

                var closeButton = new Button
                {
                    Content = "بستن",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                closeButton.Click += (s, e) => dialog.Close();

                var buttonRow = new Grid();
                buttonRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                buttonRow.Children.Add(closeButton);
                Grid.SetRow(buttonRow, 2);
                grid.Children.Add(buttonRow);

                dialog.Content = grid;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee tasks dialog");
                MessageBox.Show($"خطا در نمایش وظایف کارمندان: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTaskAssignments()
        {
            try
            {
                if (_selectedTask != null)
                {
                    // Create a list of objects with both display text and employee ID
                    var assignedEmployees = new List<object>();
                    foreach (var employeeId in _selectedTask.AssignedEmployees)
                    {
                        var employee = _controller.GetAllEmployees().FirstOrDefault(emp => emp.EmployeeId == employeeId);
                        if (employee != null)
                        {
                            assignedEmployees.Add(new { 
                                DisplayText = $"{employee.FullName} ({employeeId})", 
                                EmployeeId = employeeId 
                            });
                        }
                    }
                    AssignedEmployeesListBox.ItemsSource = assignedEmployees;
                }
                else
                {
                    AssignedEmployeesListBox.ItemsSource = new List<object>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading task assignments");
            }
        }

        #endregion

        #region Report Generation

        private void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportType = ((ComboBoxItem)ReportTypeComboBox.SelectedItem)?.Content?.ToString();
                var startDateGeorgian = ReportStartDatePicker.SelectedDate;
                var endDateGeorgian = ReportEndDatePicker.SelectedDate;

                if (string.IsNullOrEmpty(startDateGeorgian) || string.IsNullOrEmpty(endDateGeorgian))
                {
                    MessageBox.Show("لطفاً تاریخ شروع و پایان را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var report = GenerateReport(reportType, startDateGeorgian, endDateGeorgian);
                ReportPreviewTextBlock.Text = report;
                UpdateStatus("گزارش تولید شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                MessageBox.Show($"خطا در تولید گزارش: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateReport(string? reportType, string startDateGeorgian, string endDateGeorgian)
        {
            try
            {
                _logger.LogInformation("Generating report: Type={ReportType}, StartDate={StartDate}, EndDate={EndDate}", 
                    reportType, startDateGeorgian, endDateGeorgian);
                
                var report = $"گزارش {reportType}\n";
                report += $"تاریخ شروع: {GeorgianDateHelper.FormatForDisplay(startDateGeorgian)}\n";
                report += $"تاریخ پایان: {GeorgianDateHelper.FormatForDisplay(endDateGeorgian)}\n\n";

                // Load historical data for the date range
                var historicalData = LoadHistoricalData(startDateGeorgian, endDateGeorgian);
                
                _logger.LogInformation("Historical data loaded: {Count} days", historicalData.Count);
                
                if (historicalData.Count == 0)
                {
                    report += "هیچ داده‌ای برای این بازه زمانی یافت نشد.\n";
                    report += $"تاریخ‌های درخواستی: {startDateGeorgian} تا {endDateGeorgian}\n";
                    return report;
                }

                // Employee statistics (from the most recent day)
                var latestData = historicalData.OrderByDescending(kvp => kvp.Key).First().Value;
                var totalEmployees = GetEmployeeCount(latestData);
                var totalAbsences = GetTotalAbsences(historicalData);
                
                report += "آمار کارمندان:\n";
                report += $"کل کارمندان: {totalEmployees}\n";
                report += $"کل غیبت‌ها در بازه: {totalAbsences}\n\n";

                // Shift statistics (average across the period)
                var shiftStats = GetShiftStatistics(historicalData);
                
                report += "آمار شیفت‌ها (میانگین):\n";
                report += $"شیفت صبح: {shiftStats.AverageMorning:F1}/{shiftStats.Capacity}\n";
                report += $"شیفت عصر: {shiftStats.AverageEvening:F1}/{shiftStats.Capacity}\n";
                report += $"حداکثر شیفت صبح: {shiftStats.MaxMorning}/{shiftStats.Capacity}\n";
                report += $"حداکثر شیفت عصر: {shiftStats.MaxEvening}/{shiftStats.Capacity}\n\n";

                // Task statistics (total across the period)
                var taskStats = GetTaskStatistics(historicalData);
                
                report += "آمار وظایف (کل دوره):\n";
                report += $"کل وظایف: {taskStats.TotalTasks}\n";
                report += $"تکمیل شده: {taskStats.CompletedTasks}\n";
                report += $"در حال انجام: {taskStats.InProgressTasks}\n";
                report += $"در انتظار: {taskStats.PendingTasks}\n\n";

                // Daily breakdown
                report += "جزئیات روزانه:\n";
                foreach (var dayData in historicalData.OrderBy(kvp => kvp.Key))
                {
                    var date = dayData.Key;
                    var data = dayData.Value;
                    var morningCount = GetShiftCount(data, "morning");
                    var eveningCount = GetShiftCount(data, "evening");
                    var absenceCount = GetAbsenceCount(data);
                    var taskCount = GetTaskCount(data);
                    
                    report += $"{GeorgianDateHelper.FormatForDisplay(date)}: صبح({morningCount}) عصر({eveningCount}) غیبت({absenceCount}) وظیفه({taskCount})\n";
                }

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report content");
                return $"خطا در تولید گزارش: {ex.Message}";
            }
        }

        private Dictionary<string, Dictionary<string, object>> LoadHistoricalData(string startDateGeorgian, string endDateGeorgian)
        {
            var historicalData = new Dictionary<string, Dictionary<string, object>>();
            
            try
            {
                // Convert Georgian dates to file format (YYYY-MM-DD)
                var startDate = startDateGeorgian.Replace("/", "-");
                var endDate = endDateGeorgian.Replace("/", "-");
                
                _logger.LogInformation("Loading historical data from {StartDate} to {EndDate}", startDate, endDate);
                
                // Get all available report files
                var allReports = _controller.GetAllReportFiles();
                _logger.LogInformation("Found {Count} total report files", allReports.Count);
                
                foreach (var reportFile in allReports)
                {
                    _logger.LogInformation("Checking report file: {ReportFile}", reportFile);
                    
                    // Extract date from filename (report_YYYY-MM-DD.json)
                    if (reportFile.StartsWith("report_") && reportFile.EndsWith(".json"))
                    {
                        var dateStr = reportFile.Substring(7, 10); // Extract YYYY-MM-DD
                        _logger.LogInformation("Extracted date: {DateStr}", dateStr);
                        
                        // Check if this date is within our range
                        if (string.Compare(dateStr, startDate) >= 0 && string.Compare(dateStr, endDate) <= 0)
                        {
                            _logger.LogInformation("Date {DateStr} is within range, loading data", dateStr);
                            var data = _controller.ReadHistoricalReport(dateStr);
                            
                            if (data != null)
                            {
                                historicalData[dateStr] = data;
                                _logger.LogInformation("Successfully loaded data for {DateStr}", dateStr);
                                
                                // Debug: Log the structure of loaded data
                                _logger.LogInformation("Data keys for {DateStr}: {Keys}", dateStr, string.Join(", ", data.Keys));
                                
                                if (data.ContainsKey("shifts"))
                                {
                                    var shifts = data["shifts"] as Dictionary<string, object>;
                                    _logger.LogInformation("Shifts keys for {DateStr}: {Keys}", dateStr, shifts?.Keys != null ? string.Join(", ", shifts.Keys) : "null");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Failed to load data for {DateStr}", dateStr);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Date {DateStr} is outside range ({StartDate} to {EndDate})", dateStr, startDate, endDate);
                        }
                    }
                }
                
                _logger.LogInformation("Loaded {Count} historical reports for date range {StartDate} to {EndDate}", 
                    historicalData.Count, startDate, endDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading historical data");
            }
            
            return historicalData;
        }

        private int GetEmployeeCount(Dictionary<string, object> data)
        {
            try
            {
                var employees = data.GetValueOrDefault("employees") as List<object>;
                var managers = data.GetValueOrDefault("managers") as List<object>;
                
                var empCount = employees?.Count ?? 0;
                var mgrCount = managers?.Count ?? 0;
                
                _logger.LogInformation("GetEmployeeCount: employees={EmpCount}, managers={MgrCount}, total={Total}", 
                    empCount, mgrCount, empCount + mgrCount);
                
                return empCount + mgrCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetEmployeeCount");
                return 0;
            }
        }

        private int GetTotalAbsences(Dictionary<string, Dictionary<string, object>> historicalData)
        {
            var totalAbsences = 0;
            
            foreach (var dayData in historicalData.Values)
            {
                totalAbsences += GetAbsenceCount(dayData);
            }
            
            return totalAbsences;
        }

        private int GetAbsenceCount(Dictionary<string, object> data)
        {
            try
            {
                var absences = data.GetValueOrDefault("absences") as Dictionary<string, object>;
                if (absences == null) 
                {
                    _logger.LogInformation("GetAbsenceCount: no absences data found");
                    return 0;
                }
                
                var total = 0;
                foreach (var category in absences.Values)
                {
                    if (category is List<object> absenceList)
                    {
                        total += absenceList.Count;
                    }
                }
                
                _logger.LogInformation("GetAbsenceCount: total={Total}", total);
                return total;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAbsenceCount");
                return 0;
            }
        }

        private (double AverageMorning, double AverageEvening, int MaxMorning, int MaxEvening, int Capacity) GetShiftStatistics(Dictionary<string, Dictionary<string, object>> historicalData)
        {
            var morningCounts = new List<int>();
            var eveningCounts = new List<int>();
            var capacity = 15; // Default capacity
            
            foreach (var dayData in historicalData.Values)
            {
                var morningCount = GetShiftCount(dayData, "morning");
                var eveningCount = GetShiftCount(dayData, "evening");
                
                morningCounts.Add(morningCount);
                eveningCounts.Add(eveningCount);
                
                // Get capacity from first day
                if (capacity == 15)
                {
                    var shifts = dayData.GetValueOrDefault("shifts") as Dictionary<string, object>;
                    if (shifts?.ContainsKey("morning") == true)
                    {
                        var morningShift = shifts["morning"] as Dictionary<string, object>;
                        if (morningShift?.ContainsKey("capacity") == true && morningShift["capacity"] is int cap)
                        {
                            capacity = cap;
                        }
                    }
                }
            }
            
            return (
                morningCounts.Count > 0 ? morningCounts.Average() : 0,
                eveningCounts.Count > 0 ? eveningCounts.Average() : 0,
                morningCounts.Count > 0 ? morningCounts.Max() : 0,
                eveningCounts.Count > 0 ? eveningCounts.Max() : 0,
                capacity
            );
        }

        private int GetShiftCount(Dictionary<string, object> data, string shiftType)
        {
            try
            {
                var shifts = data.GetValueOrDefault("shifts") as Dictionary<string, object>;
                if (shifts?.ContainsKey(shiftType) == true)
                {
                    var shift = shifts[shiftType] as Dictionary<string, object>;
                    if (shift?.ContainsKey("assigned_employees") == true)
                    {
                        var employees = shift["assigned_employees"] as List<object>;
                        var count = employees?.Count ?? 0;
                        _logger.LogInformation("GetShiftCount: {ShiftType}={Count}", shiftType, count);
                        return count;
                    }
                }
                
                _logger.LogInformation("GetShiftCount: {ShiftType}=0 (no data found)", shiftType);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetShiftCount for {ShiftType}", shiftType);
                return 0;
            }
        }

        private (int TotalTasks, int CompletedTasks, int InProgressTasks, int PendingTasks) GetTaskStatistics(Dictionary<string, Dictionary<string, object>> historicalData)
        {
            var totalTasks = 0;
            var completedTasks = 0;
            var inProgressTasks = 0;
            var pendingTasks = 0;
            
            foreach (var dayData in historicalData.Values)
            {
                var taskCount = GetTaskCount(dayData);
                totalTasks += taskCount;
                
                // For simplicity, assume all tasks in historical data are completed
                // In a real implementation, you'd parse the task status from the data
                completedTasks += taskCount;
            }
            
            return (totalTasks, completedTasks, inProgressTasks, pendingTasks);
        }

        private int GetTaskCount(Dictionary<string, object> data)
        {
            try
            {
                var tasks = data.GetValueOrDefault("tasks") as Dictionary<string, object>;
                if (tasks?.ContainsKey("Tasks") == true)
                {
                    var taskDict = tasks["Tasks"] as Dictionary<string, object>;
                    return taskDict?.Count ?? 0;
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }


        #endregion

        #region Event Handlers

        private void OnEmployeesUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                LoadEmployees();
                LoadShifts(); // Refresh shift availability
            });
        }

        private void OnRolesUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                // Refresh role ComboBox when roles are updated
                InitializeRoleComboBox();
                _logger.LogInformation("Roles updated and ComboBox refreshed");
            });
        }

        private void OnShiftsUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                LoadShiftSlots();
                UpdateShiftStatistics();
            });
        }

        private void OnAbsencesUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                LoadAbsences();
                LoadEmployees(); // Refresh employee lists
                LoadShifts(); // Refresh shift data and absent employees list
            });
        }

        private void OnTasksUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                LoadTasks();
            });
        }

        private void OnShiftGroupsUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                // Refresh shift group selection ComboBox
                LoadShiftGroups();
                
                // Refresh progress group ComboBoxes
                LoadDailyProgressGroups();
                
                // Refresh shift-related data when shift groups are updated
                LoadShiftSlots();
                UpdateShiftStatistics();
                
                // Refresh employee lists if they depend on shift groups
                LoadEmployees();
                
                // Update status to show that shift groups were updated
                UpdateStatus("گروه‌های شیفت بروزرسانی شدند");
                
                _logger.LogInformation("Shift groups updated - UI refreshed");
            });
        }

        private void UpdateDailyPreview()
        {
            try
            {
                var today = Shared.Utils.GeorgianDateHelper.GetCurrentGeorgianDate();
                
                // Count sick employees for today
                var sickAbsences = _controller.AbsenceManager.GetAbsencesByCategory("بیمار")
                    .Where(a => a.Date == today)
                    .ToList();
                SickCountText.Text = sickAbsences.Count.ToString();
                
                // Count employees on leave for today
                var leaveAbsences = _controller.AbsenceManager.GetAbsencesByCategory("مرخصی")
                    .Where(a => a.Date == today)
                    .ToList();
                LeaveCountText.Text = leaveAbsences.Count.ToString();
                
                // Count absent employees for today
                var absentAbsences = _controller.AbsenceManager.GetAbsencesByCategory("غایب")
                    .Where(a => a.Date == today)
                    .ToList();
                AbsentCountText.Text = absentAbsences.Count.ToString();
                
                // Get group statistics
                var groupStats = new List<GroupStatistic>();
                foreach (var group in _controller.ShiftGroupManager.ShiftGroups.Values)
                {
                    if (group.IsActive)
                    {
                        var employeeCount = group.GetTotalAssignedEmployees();
                        groupStats.Add(new GroupStatistic
                        {
                            GroupName = group.Name,
                            Description = group.Description,
                            EmployeeCount = employeeCount
                        });
                    }
                }
                
                GroupStatsItemsControl.ItemsSource = groupStats;
                
                // Update date display
                DailyPreviewDateText.Text = Shared.Utils.GeorgianDateHelper.FormatForDisplay(today);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating daily preview");
            }
        }

        private void OnDailyPreviewDataUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                // Only update if the daily preview tab is currently selected
                if (MainTabControl.SelectedItem is TabItem selectedTab && 
                    selectedTab.Header?.ToString() == "پیش‌نمایش روزانه")
                {
                    UpdateDailyPreview();
                }
            });
        }

        private void PrintDailyPreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Create a printable document with better formatting
                    var document = new FlowDocument
                    {
                        PagePadding = new Thickness(50),
                        FontFamily = new FontFamily("Tahoma"),
                        FontSize = 12,
                        ColumnWidth = printDialog.PrintableAreaWidth - 100
                    };
                    
                    var today = Shared.Utils.GeorgianDateHelper.GetCurrentGeorgianDate();
                    var todayDisplay = Shared.Utils.GeorgianDateHelper.FormatForDisplay(today);
                    var generationTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
                    
                    // Define color scheme
                    var headerBackground = new SolidColorBrush(Color.FromRgb(227, 242, 253)); // Light blue
                    var sectionBackground = new SolidColorBrush(Color.FromRgb(245, 245, 245)); // Light gray
                    var borderColor = new SolidColorBrush(Color.FromRgb(189, 189, 189)); // Medium gray
                    var headerTextColor = new SolidColorBrush(Color.FromRgb(25, 118, 210)); // Blue
                    var darkTextColor = new SolidColorBrush(Color.FromRgb(66, 66, 66)); // Dark gray
                    
                    // Header Section
                    var headerTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        Background = headerBackground,
                        CellSpacing = 0,
                        Margin = new Thickness(0, 0, 0, 25)
                    };
                    headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var headerRow = new TableRow();
                    var headerCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(15, 20, 15, 20),
                        TextAlignment = TextAlignment.Center
                    };
                    headerCell.Blocks.Add(new Paragraph(new Run($"خلاصه روزانه - {todayDisplay}"))
                    {
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = headerTextColor
                    });
                    headerCell.Blocks.Add(new Paragraph(new Run($"تاریخ تولید: {generationTime}"))
                    {
                        FontSize = 10,
                        Foreground = darkTextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    });
                    headerRow.Cells.Add(headerCell);
                    headerTable.RowGroups.Add(new TableRowGroup());
                    headerTable.RowGroups[0].Rows.Add(headerRow);
                    document.Blocks.Add(headerTable);
                    
                    // Absence statistics
                    var sickCount = _controller.AbsenceManager.GetAbsencesByCategory("بیمار")
                        .Count(a => a.Date == today);
                    var leaveCount = _controller.AbsenceManager.GetAbsencesByCategory("مرخصی")
                        .Count(a => a.Date == today);
                    var absentCount = _controller.AbsenceManager.GetAbsencesByCategory("غایب")
                        .Count(a => a.Date == today);
                    
                    // Absence Statistics Table
                    var absenceTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        CellSpacing = 0,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    absenceTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                    absenceTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var absenceRowGroup = new TableRowGroup();
                    
                    // Section header row
                    var absenceHeaderRow = new TableRow
                    {
                        Background = sectionBackground
                    };
                    var absenceHeaderCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(12, 10, 12, 10),
                        ColumnSpan = 2
                    };
                    absenceHeaderCell.Blocks.Add(new Paragraph(new Run("آمار غیبت‌ها"))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = headerTextColor
                    });
                    absenceHeaderRow.Cells.Add(absenceHeaderCell);
                    absenceRowGroup.Rows.Add(absenceHeaderRow);
                    
                    // Data rows
                    var absenceData = new[]
                    {
                        new { Label = "کارمندان بیمار", Count = sickCount },
                        new { Label = "کارمندان مرخصی", Count = leaveCount },
                        new { Label = "کارمندان غایب", Count = absentCount }
                    };
                    
                    bool isAlternate = false;
                    foreach (var item in absenceData)
                    {
                        var dataRow = new TableRow
                        {
                            Background = isAlternate ? sectionBackground : Brushes.White
                        };
                        isAlternate = !isAlternate;
                        
                        var labelCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10)
                        };
                        labelCell.Blocks.Add(new Paragraph(new Run(item.Label))
                        {
                            FontSize = 12,
                            Foreground = darkTextColor
                        });
                        dataRow.Cells.Add(labelCell);
                        
                        var countCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10),
                            TextAlignment = TextAlignment.Center
                        };
                        countCell.Blocks.Add(new Paragraph(new Run(item.Count.ToString()))
                        {
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            Foreground = headerTextColor
                        });
                        dataRow.Cells.Add(countCell);
                        
                        absenceRowGroup.Rows.Add(dataRow);
                    }
                    
                    absenceTable.RowGroups.Add(absenceRowGroup);
                    document.Blocks.Add(absenceTable);
                    
                    // Visual Separator
                    var separator = new Paragraph(new Run(new string('─', 50)))
                    {
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 15, 0, 20),
                        Foreground = borderColor
                    };
                    document.Blocks.Add(separator);
                    
                    // Group statistics
                    var activeGroups = _controller.ShiftGroupManager.ShiftGroups.Values
                        .Where(g => g.IsActive)
                        .ToList();
                    
                    // Shift Groups Statistics Table
                    var groupsTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        CellSpacing = 0,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    groupsTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                    groupsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var groupsRowGroup = new TableRowGroup();
                    
                    // Section header row
                    var groupsHeaderRow = new TableRow
                    {
                        Background = sectionBackground
                    };
                    var groupsHeaderCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(12, 10, 12, 10),
                        ColumnSpan = 2
                    };
                    groupsHeaderCell.Blocks.Add(new Paragraph(new Run("آمار گروه‌های شیفت"))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = headerTextColor
                    });
                    groupsHeaderRow.Cells.Add(groupsHeaderCell);
                    groupsRowGroup.Rows.Add(groupsHeaderRow);
                    
                    // Group data rows
                    isAlternate = false;
                    foreach (var group in activeGroups)
                    {
                        var count = group.GetTotalAssignedEmployees();
                        var dataRow = new TableRow
                        {
                            Background = isAlternate ? sectionBackground : Brushes.White
                        };
                        isAlternate = !isAlternate;
                        
                        var nameCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10)
                        };
                        nameCell.Blocks.Add(new Paragraph(new Run(group.Name))
                        {
                            FontSize = 12,
                            Foreground = darkTextColor
                        });
                        dataRow.Cells.Add(nameCell);
                        
                        var countCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10),
                            TextAlignment = TextAlignment.Center
                        };
                        countCell.Blocks.Add(new Paragraph(new Run($"{count} کارمند"))
                        {
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Foreground = headerTextColor
                        });
                        dataRow.Cells.Add(countCell);
                        
                        groupsRowGroup.Rows.Add(dataRow);
                    }
                    
                    // If no groups, add a message row
                    if (activeGroups.Count == 0)
                    {
                        var emptyRow = new TableRow
                        {
                            Background = Brushes.White
                        };
                        var emptyCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10),
                            ColumnSpan = 2,
                            TextAlignment = TextAlignment.Center
                        };
                        emptyCell.Blocks.Add(new Paragraph(new Run("گروه فعالی وجود ندارد"))
                        {
                            FontSize = 12,
                            FontStyle = FontStyles.Italic,
                            Foreground = darkTextColor
                        });
                        emptyRow.Cells.Add(emptyCell);
                        groupsRowGroup.Rows.Add(emptyRow);
                    }
                    
                    groupsTable.RowGroups.Add(groupsRowGroup);
                    document.Blocks.Add(groupsTable);
                    
                    // Footer Section
                    var footerTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1, 0, 1, 1),
                        Background = sectionBackground,
                        CellSpacing = 0,
                        Margin = new Thickness(0, 30, 0, 0)
                    };
                    footerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var footerRow = new TableRow();
                    var footerCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(15, 12, 15, 12),
                        TextAlignment = TextAlignment.Center
                    };
                    footerCell.Blocks.Add(new Paragraph(new Run("گزارش تولید شده توسط سیستم مدیریت کارمندان"))
                    {
                        FontSize = 9,
                        Foreground = darkTextColor,
                        FontStyle = FontStyles.Italic
                    });
                    footerRow.Cells.Add(footerCell);
                    footerTable.RowGroups.Add(new TableRowGroup());
                    footerTable.RowGroups[0].Rows.Add(footerRow);
                    document.Blocks.Add(footerTable);
                    
                    // Print
                    document.PageHeight = printDialog.PrintableAreaHeight;
                    document.PageWidth = printDialog.PrintableAreaWidth;
                    
                    var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                    printDialog.PrintDocument(paginator, $"خلاصه روزانه - {todayDisplay}");
                    
                    UpdateStatus("خلاصه روزانه چاپ شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing daily preview");
                MessageBox.Show($"خطا در چاپ: {ex.Message}", "خطا", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var reportContent = ReportPreviewTextBlock.Text;
                if (string.IsNullOrWhiteSpace(reportContent))
                {
                    MessageBox.Show("لطفاً ابتدا گزارش را تولید کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Parse report content
                    var reportData = ParseReportContentForPrint(reportContent);
                    
                    // Create a printable document with better formatting
                    var document = new FlowDocument
                    {
                        PagePadding = new Thickness(50),
                        FontFamily = new FontFamily("Tahoma"),
                        FontSize = 12,
                        ColumnWidth = printDialog.PrintableAreaWidth - 100
                    };
                    
                    var generationTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
                    
                    // Define color scheme (same as PrintDailyPreview_Click)
                    var headerBackground = new SolidColorBrush(Color.FromRgb(227, 242, 253)); // Light blue
                    var sectionBackground = new SolidColorBrush(Color.FromRgb(245, 245, 245)); // Light gray
                    var borderColor = new SolidColorBrush(Color.FromRgb(189, 189, 189)); // Medium gray
                    var headerTextColor = new SolidColorBrush(Color.FromRgb(25, 118, 210)); // Blue
                    var darkTextColor = new SolidColorBrush(Color.FromRgb(66, 66, 66)); // Dark gray
                    
                    // Get report title
                    var reportTypeText = "گزارش";
                    if (ReportTypeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedReportType)
                    {
                        reportTypeText = selectedReportType.Content?.ToString() ?? "گزارش";
                    }
                    var reportTitle = !string.IsNullOrEmpty(reportData.ReportType) 
                        ? reportData.ReportType 
                        : reportTypeText;
                    
                    // Header Section
                    var headerTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        Background = headerBackground,
                        CellSpacing = 0,
                        Margin = new Thickness(0, 0, 0, 25)
                    };
                    headerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var headerRow = new TableRow();
                    var headerCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(15, 20, 15, 20),
                        TextAlignment = TextAlignment.Center
                    };
                    headerCell.Blocks.Add(new Paragraph(new Run(reportTitle))
                    {
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = headerTextColor
                    });
                    
                    if (!string.IsNullOrEmpty(reportData.StartDate) && !string.IsNullOrEmpty(reportData.EndDate))
                    {
                        headerCell.Blocks.Add(new Paragraph(new Run($"از {reportData.StartDate} تا {reportData.EndDate}"))
                        {
                            FontSize = 14,
                            Foreground = darkTextColor,
                            Margin = new Thickness(0, 8, 0, 0)
                        });
                    }
                    
                    headerCell.Blocks.Add(new Paragraph(new Run($"تاریخ تولید: {generationTime}"))
                    {
                        FontSize = 10,
                        Foreground = darkTextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    });
                    headerRow.Cells.Add(headerCell);
                    headerTable.RowGroups.Add(new TableRowGroup());
                    headerTable.RowGroups[0].Rows.Add(headerRow);
                    document.Blocks.Add(headerTable);
                    
                    // Visual Separator
                    var separator = new Paragraph(new Run(new string('─', 50)))
                    {
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 15, 0, 20),
                        Foreground = borderColor
                    };
                    document.Blocks.Add(separator);
                    
                    // Employee Statistics Table
                    var employeeTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        CellSpacing = 0,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    employeeTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                    employeeTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var employeeRowGroup = new TableRowGroup();
                    
                    // Section header row
                    var employeeHeaderRow = new TableRow
                    {
                        Background = sectionBackground
                    };
                    var employeeHeaderCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(12, 10, 12, 10),
                        ColumnSpan = 2
                    };
                    employeeHeaderCell.Blocks.Add(new Paragraph(new Run("آمار کارمندان"))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = headerTextColor
                    });
                    employeeHeaderRow.Cells.Add(employeeHeaderCell);
                    employeeRowGroup.Rows.Add(employeeHeaderRow);
                    
                    // Data rows
                    var employeeData = new[]
                    {
                        new { Label = "کل کارمندان", Value = reportData.TotalEmployees.ToString() },
                        new { Label = "کل غیبت‌ها در بازه", Value = reportData.TotalAbsences.ToString() }
                    };
                    
                    bool isAlternate = false;
                    foreach (var item in employeeData)
                    {
                        var dataRow = new TableRow
                        {
                            Background = isAlternate ? sectionBackground : Brushes.White
                        };
                        isAlternate = !isAlternate;
                        
                        var labelCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10)
                        };
                        labelCell.Blocks.Add(new Paragraph(new Run(item.Label))
                        {
                            FontSize = 12,
                            Foreground = darkTextColor
                        });
                        dataRow.Cells.Add(labelCell);
                        
                        var valueCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10),
                            TextAlignment = TextAlignment.Center
                        };
                        valueCell.Blocks.Add(new Paragraph(new Run(item.Value))
                        {
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            Foreground = headerTextColor
                        });
                        dataRow.Cells.Add(valueCell);
                        
                        employeeRowGroup.Rows.Add(dataRow);
                    }
                    
                    employeeTable.RowGroups.Add(employeeRowGroup);
                    document.Blocks.Add(employeeTable);
                    
                    // Visual Separator
                    document.Blocks.Add(separator);
                    
                    // Shift Statistics Table
                    var capacity = reportData.ShiftCapacity > 0 ? reportData.ShiftCapacity : 15;
                    var shiftTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        CellSpacing = 0,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    shiftTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                    shiftTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var shiftRowGroup = new TableRowGroup();
                    
                    // Section header row
                    var shiftHeaderRow = new TableRow
                    {
                        Background = sectionBackground
                    };
                    var shiftHeaderCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(12, 10, 12, 10),
                        ColumnSpan = 2
                    };
                    shiftHeaderCell.Blocks.Add(new Paragraph(new Run("آمار شیفت‌ها (میانگین)"))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = headerTextColor
                    });
                    shiftHeaderRow.Cells.Add(shiftHeaderCell);
                    shiftRowGroup.Rows.Add(shiftHeaderRow);
                    
                    // Data rows
                    var shiftData = new[]
                    {
                        new { Label = "شیفت صبح (میانگین)", Value = $"{reportData.AverageMorningShift:F1}/{capacity}" },
                        new { Label = "شیفت عصر (میانگین)", Value = $"{reportData.AverageAfternoonShift:F1}/{capacity}" },
                        new { Label = "شیفت شب (میانگین)", Value = $"{reportData.AverageNightShift:F1}/{capacity}" },
                        new { Label = "حداکثر شیفت صبح", Value = $"{reportData.MaxMorningShift}/{capacity}" },
                        new { Label = "حداکثر شیفت عصر", Value = $"{reportData.MaxAfternoonShift}/{capacity}" },
                        new { Label = "حداکثر شیفت شب", Value = $"{reportData.MaxNightShift}/{capacity}" }
                    };
                    
                    isAlternate = false;
                    foreach (var item in shiftData)
                    {
                        var dataRow = new TableRow
                        {
                            Background = isAlternate ? sectionBackground : Brushes.White
                        };
                        isAlternate = !isAlternate;
                        
                        var labelCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10)
                        };
                        labelCell.Blocks.Add(new Paragraph(new Run(item.Label))
                        {
                            FontSize = 12,
                            Foreground = darkTextColor
                        });
                        dataRow.Cells.Add(labelCell);
                        
                        var valueCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10),
                            TextAlignment = TextAlignment.Center
                        };
                        valueCell.Blocks.Add(new Paragraph(new Run(item.Value))
                        {
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Foreground = headerTextColor
                        });
                        dataRow.Cells.Add(valueCell);
                        
                        shiftRowGroup.Rows.Add(dataRow);
                    }
                    
                    shiftTable.RowGroups.Add(shiftRowGroup);
                    document.Blocks.Add(shiftTable);
                    
                    // Visual Separator
                    document.Blocks.Add(separator);
                    
                    // Task Statistics Table
                    var taskTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        CellSpacing = 0,
                        Margin = new Thickness(0, 0, 0, 20)
                    };
                    taskTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                    taskTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var taskRowGroup = new TableRowGroup();
                    
                    // Section header row
                    var taskHeaderRow = new TableRow
                    {
                        Background = sectionBackground
                    };
                    var taskHeaderCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(12, 10, 12, 10),
                        ColumnSpan = 2
                    };
                    taskHeaderCell.Blocks.Add(new Paragraph(new Run("آمار وظایف (کل دوره)"))
                    {
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = headerTextColor
                    });
                    taskHeaderRow.Cells.Add(taskHeaderCell);
                    taskRowGroup.Rows.Add(taskHeaderRow);
                    
                    // Data rows
                    var taskData = new[]
                    {
                        new { Label = "کل وظایف", Value = reportData.TotalTasks.ToString() },
                        new { Label = "تکمیل شده", Value = reportData.CompletedTasks.ToString() },
                        new { Label = "در حال انجام", Value = reportData.InProgressTasks.ToString() },
                        new { Label = "در انتظار", Value = reportData.PendingTasks.ToString() }
                    };
                    
                    isAlternate = false;
                    foreach (var item in taskData)
                    {
                        var dataRow = new TableRow
                        {
                            Background = isAlternate ? sectionBackground : Brushes.White
                        };
                        isAlternate = !isAlternate;
                        
                        var labelCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10)
                        };
                        labelCell.Blocks.Add(new Paragraph(new Run(item.Label))
                        {
                            FontSize = 12,
                            Foreground = darkTextColor
                        });
                        dataRow.Cells.Add(labelCell);
                        
                        var valueCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(15, 10, 15, 10),
                            TextAlignment = TextAlignment.Center
                        };
                        valueCell.Blocks.Add(new Paragraph(new Run(item.Value))
                        {
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Foreground = headerTextColor
                        });
                        dataRow.Cells.Add(valueCell);
                        
                        taskRowGroup.Rows.Add(dataRow);
                    }
                    
                    taskTable.RowGroups.Add(taskRowGroup);
                    document.Blocks.Add(taskTable);
                    
                    // Visual Separator
                    document.Blocks.Add(separator);
                    
                    // Daily Details Section
                    if (reportData.DailyDetails.Count > 0)
                    {
                        var dailyHeaderTable = new Table
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            CellSpacing = 0,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        dailyHeaderTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                        
                        var dailyHeaderRow = new TableRow
                        {
                            Background = sectionBackground
                        };
                        var dailyHeaderCell = new TableCell
                        {
                            BorderBrush = borderColor,
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(12, 10, 12, 10)
                        };
                        dailyHeaderCell.Blocks.Add(new Paragraph(new Run("جزئیات روزانه"))
                        {
                            FontSize = 16,
                            FontWeight = FontWeights.Bold,
                            Foreground = headerTextColor
                        });
                        dailyHeaderRow.Cells.Add(dailyHeaderCell);
                        dailyHeaderTable.RowGroups.Add(new TableRowGroup());
                        dailyHeaderTable.RowGroups[0].Rows.Add(dailyHeaderRow);
                        document.Blocks.Add(dailyHeaderTable);
                        
                        foreach (var detail in reportData.DailyDetails)
                        {
                            var detailParagraph = new Paragraph(new Run(detail))
                            {
                                FontSize = 11,
                                Foreground = darkTextColor,
                                Margin = new Thickness(0, 0, 0, 5),
                                Padding = new Thickness(15, 0, 0, 0)
                            };
                            document.Blocks.Add(detailParagraph);
                        }
                        
                        document.Blocks.Add(separator);
                    }
                    
                    // Footer Section
                    var footerTable = new Table
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(1, 0, 1, 1),
                        Background = sectionBackground,
                        CellSpacing = 0,
                        Margin = new Thickness(0, 30, 0, 0)
                    };
                    footerTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                    
                    var footerRow = new TableRow();
                    var footerCell = new TableCell
                    {
                        BorderBrush = borderColor,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(15, 12, 15, 12),
                        TextAlignment = TextAlignment.Center
                    };
                    footerCell.Blocks.Add(new Paragraph(new Run("گزارش تولید شده توسط سیستم مدیریت کارمندان"))
                    {
                        FontSize = 9,
                        FontStyle = FontStyles.Italic,
                        Foreground = darkTextColor
                    });
                    footerRow.Cells.Add(footerCell);
                    footerTable.RowGroups.Add(new TableRowGroup());
                    footerTable.RowGroups[0].Rows.Add(footerRow);
                    document.Blocks.Add(footerTable);
                    
                    // Print
                    document.PageHeight = printDialog.PrintableAreaHeight;
                    document.PageWidth = printDialog.PrintableAreaWidth;
                    
                    var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                    printDialog.PrintDocument(paginator, reportTitle);
                    
                    UpdateStatus("گزارش چاپ شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing report");
                MessageBox.Show($"خطا در چاپ: {ex.Message}", "خطا", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class ReportDataForPrint
        {
            public string ReportType { get; set; } = "";
            public string StartDate { get; set; } = "";
            public string EndDate { get; set; } = "";
            public int TotalEmployees { get; set; }
            public int TotalAbsences { get; set; }
            public double AverageMorningShift { get; set; }
            public double AverageAfternoonShift { get; set; }
            public double AverageNightShift { get; set; }
            public int MaxMorningShift { get; set; }
            public int MaxAfternoonShift { get; set; }
            public int MaxNightShift { get; set; }
            public int ShiftCapacity { get; set; }
            public int TotalTasks { get; set; }
            public int CompletedTasks { get; set; }
            public int InProgressTasks { get; set; }
            public int PendingTasks { get; set; }
            public List<string> DailyDetails { get; set; } = new List<string>();
        }

        private ReportDataForPrint ParseReportContentForPrint(string content)
        {
            var reportData = new ReportDataForPrint();
            
            if (string.IsNullOrEmpty(content))
                return reportData;

            var lines = content.Split('\n');
            bool inDailyDetails = false;
            
            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (string.IsNullOrWhiteSpace(cleanLine))
                {
                    inDailyDetails = false;
                    continue;
                }

                // Remove emojis and formatting characters if present
                cleanLine = System.Text.RegularExpressions.Regex.Replace(cleanLine, @"[📊📅👥❌⏰📋📈•=]", "").Trim();
                
                // Normalize whitespace
                cleanLine = System.Text.RegularExpressions.Regex.Replace(cleanLine, @"\s+", " ");

                // Parse report type
                if (cleanLine.StartsWith("گزارش") && !cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"گزارش\s+(.+)");
                    if (match.Success)
                        reportData.ReportType = match.Groups[1].Value.Trim();
                }
                // Parse dates
                else if (cleanLine.Contains("تاریخ شروع:"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"تاریخ شروع:\s*(.+)");
                    if (match.Success)
                        reportData.StartDate = match.Groups[1].Value.Trim();
                }
                else if (cleanLine.Contains("تاریخ پایان:"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"تاریخ پایان:\s*(.+)");
                    if (match.Success)
                        reportData.EndDate = match.Groups[1].Value.Trim();
                }
                // Parse employee statistics
                else if (cleanLine.Contains("کل کارمندان") && !cleanLine.Contains("مدیران"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"کل\s+کارمندان[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.TotalEmployees = count;
                }
                else if (cleanLine.Contains("کل غیبت") && (cleanLine.Contains("بازه") || cleanLine.Contains("در بازه")))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"کل\s+غیبت[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.TotalAbsences = count;
                }
                // Parse shift statistics
                else if (cleanLine.Contains("شیفت صبح") && cleanLine.Contains("/") && !cleanLine.Contains("حداکثر"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"شیفت\s+صبح[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                            reportData.AverageMorningShift = avg;
                        if (int.TryParse(match.Groups[2].Value, out int cap) && reportData.ShiftCapacity == 0)
                            reportData.ShiftCapacity = cap;
                    }
                }
                else if (cleanLine.Contains("شیفت عصر") && cleanLine.Contains("/") && !cleanLine.Contains("حداکثر"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"شیفت\s+عصر[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                        reportData.AverageAfternoonShift = avg;
                }
                else if (cleanLine.Contains("شیفت شب") && cleanLine.Contains("/") && !cleanLine.Contains("حداکثر"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"شیفت\s+شب[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                        reportData.AverageNightShift = avg;
                }
                else if (cleanLine.Contains("حداکثر") && cleanLine.Contains("شیفت صبح"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"حداکثر\s+شیفت\s+صبح[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                        reportData.MaxMorningShift = max;
                }
                else if (cleanLine.Contains("حداکثر") && cleanLine.Contains("شیفت عصر"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"حداکثر\s+شیفت\s+عصر[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                        reportData.MaxAfternoonShift = max;
                }
                else if (cleanLine.Contains("حداکثر") && cleanLine.Contains("شیفت شب"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"حداکثر\s+شیفت\s+شب[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                        reportData.MaxNightShift = max;
                }
                // Parse task statistics
                else if (cleanLine.Contains("کل وظایف") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"کل\s+وظایف[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.TotalTasks = count;
                }
                else if (cleanLine.Contains("تکمیل شده") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"تکمیل\s+شده[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.CompletedTasks = count;
                }
                else if (cleanLine.Contains("در حال انجام") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"در\s+حال\s+انجام[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.InProgressTasks = count;
                }
                else if (cleanLine.Contains("در انتظار") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"در\s+انتظار[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.PendingTasks = count;
                }
                // Check for daily details section
                else if (cleanLine.Contains("جزئیات روزانه:") || cleanLine == "جزئیات روزانه")
                {
                    inDailyDetails = true;
                }
                // Parse daily details
                else if (inDailyDetails || (cleanLine.Contains("صبح") && cleanLine.Contains("عصر") && 
                          cleanLine.Contains("غیبت") && cleanLine.Contains("وظیفه")))
                {
                    if (!string.IsNullOrWhiteSpace(cleanLine))
                        reportData.DailyDetails.Add(cleanLine);
                }
            }

            return reportData;
        }

        private void OnSettingsUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                LoadShifts();
            });
        }

        private void OnSyncTriggered()
        {
            Dispatcher.Invoke(() =>
            {
                LoadData();
                UpdateStatus("داده‌ها همگام‌سازی شدند");
            });
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Time is updated via data binding in XAML
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating timer");
            }
        }

        #endregion

        #region Utility Methods

        private void UpdateStatus(string message)
        {
            try
            {
                StatusTextBlock.Text = message;
                _logger.LogInformation("Status updated: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status");
            }
        }

        #region Settings Methods

        private void SelectDataDirectory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use WPF's OpenFolderDialog (available in .NET 8)
                var dialog = new OpenFolderDialog
                {
                    Title = "انتخاب پوشه داده‌های مشترک",
                    InitialDirectory = DataDirectoryTextBox.Text
                };

                if (dialog.ShowDialog() == true)
                {
                    DataDirectoryTextBox.Text = dialog.FolderName;
                    CurrentDataDirectoryTextBlock.Text = dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting data directory");
                MessageBox.Show($"خطا در انتخاب پوشه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newDataDirectory = DataDirectoryTextBox.Text.Trim();
                
                if (string.IsNullOrEmpty(newDataDirectory))
                {
                    MessageBox.Show("لطفاً مسیر پوشه داده‌ها را انتخاب کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!Directory.Exists(newDataDirectory))
                {
                    var result = MessageBox.Show(
                        "پوشه انتخاب شده وجود ندارد. آیا می‌خواهید آن را ایجاد کنید؟",
                        "تأیید",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        Directory.CreateDirectory(newDataDirectory);
                    }
                    else
                    {
                        return;
                    }
                }

                // Get user preference for copying existing data
                var copyExistingData = CopyExistingDataCheckBox.IsChecked ?? true;
                
                // Show progress bar if copying data
                if (copyExistingData)
                {
                    ProgressPanel.Visibility = Visibility.Visible;
                    DataCopyProgressBar.IsIndeterminate = true;
                }

                // Disable the save button to prevent multiple clicks
                var saveButton = sender as System.Windows.Controls.Button;
                if (saveButton != null)
                {
                    saveButton.IsEnabled = false;
                    saveButton.Content = "در حال پردازش...";
                }
                
                // Update configuration asynchronously
                var success = await System.Threading.Tasks.Task.Run(() => AppConfigHelper.UpdateDataDirectory(newDataDirectory, copyExistingData));
                
                // Hide progress bar
                ProgressPanel.Visibility = Visibility.Collapsed;
                
                if (success)
                {
                    var message = copyExistingData 
                        ? "تنظیمات با موفقیت ذخیره شد و داده‌های موجود منتقل شدند." 
                        : "تنظیمات با موفقیت ذخیره شد.";
                    
                    MessageBox.Show(message, "موفق", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Update UI
                    CurrentDataDirectoryTextBlock.Text = newDataDirectory;
                    UpdateSettingsDisplay();
                    
                    // The controller will automatically update through the configuration change event
                    _logger.LogInformation("Data directory changed to: {NewPath}, Copy existing data: {CopyData}", 
                        newDataDirectory, copyExistingData);
                }
                else
                {
                    MessageBox.Show("خطا در ذخیره تنظیمات. لطفاً مسیر معتبری انتخاب کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                MessageBox.Show($"خطا در ذخیره تنظیمات: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Hide progress bar and re-enable button in case of error
                ProgressPanel.Visibility = Visibility.Collapsed;
                
                // Re-enable the save button
                var saveButton = sender as System.Windows.Controls.Button;
                if (saveButton != null)
                {
                    saveButton.IsEnabled = true;
                    saveButton.Content = "ذخیره تنظیمات";
                }
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "آیا مطمئن هستید که می‌خواهید تنظیمات را به حالت پیش‌فرض بازنشانی کنید؟",
                    "تأیید",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Use relative path for default
                    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    var defaultPath = Path.Combine(baseDirectory, "..", "..", "..", "SharedData");
                    defaultPath = Path.GetFullPath(defaultPath);
                    
                    DataDirectoryTextBox.Text = defaultPath;
                    CurrentDataDirectoryTextBlock.Text = defaultPath;
                    DefaultShiftCapacityTextBox.Text = "15";
                    SyncIntervalTextBox.Text = "30";
                    
                    // Reset display background color to default
                    var displayConfigPath = GetDisplayConfigPath();
                    if (File.Exists(displayConfigPath))
                    {
                        var displayConfigHelper = new DisplayApp.Utils.ConfigHelper(displayConfigPath);
                        displayConfigHelper.SetBackgroundColor("#1a1a1a");
                        displayConfigHelper.SaveConfig();
                        UpdateDisplayColorPreview("#1a1a1a");
                    }
                    
                    MessageBox.Show("تنظیمات به حالت پیش‌فرض بازنشانی شد.", "موفق", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings");
                MessageBox.Show($"خطا در بازنشانی تنظیمات: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectDisplayColorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get current background color from config
                var displayConfigPath = GetDisplayConfigPath();
                var currentColor = "#1a1a1a"; // Default
                
                if (File.Exists(displayConfigPath))
                {
                    var displayConfigHelper = new DisplayApp.Utils.ConfigHelper(displayConfigPath);
                    currentColor = displayConfigHelper.GetBackgroundColor();
                }
                
                // Open color palette popup
                var colorPicker = new ColorPalettePopup(currentColor);
                if (colorPicker.ShowDialog() == true)
                {
                    var selectedColor = colorPicker.SelectedColor;
                    
                    // Save to display config
                    var displayConfigHelper = new DisplayApp.Utils.ConfigHelper(displayConfigPath);
                    displayConfigHelper.SetBackgroundColor(selectedColor);
                    displayConfigHelper.SaveConfig();
                    
                    // Update preview
                    UpdateDisplayColorPreview(selectedColor);
                    
                    MessageBox.Show("رنگ محیط نمایش با موفقیت تغییر کرد. برای اعمال تغییرات، برنامه نمایش را مجدداً راه‌اندازی کنید.", 
                        "موفق", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting display color");
                MessageBox.Show($"خطا در انتخاب رنگ: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetDisplayConfigPath()
        {
            // Try multiple possible locations for the display config file
            // Priority: DisplayApp's bin directory (where it actually runs from), then source directory
            // From ManagementApp\bin\Debug\net8.0-windows\ we need to go up 4 levels to project root
            var possiblePaths = new[]
            {
                // DisplayApp's bin directory (where it runs from) - 4 levels up from ManagementApp\bin\Debug\net8.0-windows\
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "DisplayApp", "bin", "Debug", "net8.0-windows", "Config", "display_config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "DisplayApp", "bin", "Debug", "net8.0-windows", "Config", "display_config.json"),
                // DisplayApp's source Config directory - 4 levels up
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "DisplayApp", "Config", "display_config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "DisplayApp", "Config", "display_config.json"),
                // Alternative: 3 levels up (if running from different location)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "DisplayApp", "bin", "Debug", "net8.0-windows", "Config", "display_config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "DisplayApp", "bin", "Debug", "net8.0-windows", "Config", "display_config.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "DisplayApp", "Config", "display_config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "DisplayApp", "Config", "display_config.json"),
                // Fallback: 2 levels up
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "DisplayApp", "Config", "display_config.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "DisplayApp", "Config", "display_config.json")
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            
            // If not found, return the most likely path (DisplayApp's bin directory) - 4 levels up
            var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "DisplayApp", "bin", "Debug", "net8.0-windows", "Config", "display_config.json");
            return Path.GetFullPath(defaultPath);
        }

        private void UpdateDisplayColorPreview(string colorHex)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                DisplayColorPreview.Background = new SolidColorBrush(color);
            }
            catch
            {
                // If color conversion fails, use default
                DisplayColorPreview.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1a1a1a"));
            }
        }

        private void UpdateSettingsDisplay()
        {
            try
            {
                var config = AppConfigHelper.Config;
                DataDirectoryTextBox.Text = config.DataDirectory;
                CurrentDataDirectoryTextBlock.Text = config.DataDirectory;
                DefaultShiftCapacityTextBox.Text = "15"; // Default value
                SyncIntervalTextBox.Text = config.SyncIntervalSeconds.ToString();
                
                // Load display background color
                var displayConfigPath = GetDisplayConfigPath();
                if (File.Exists(displayConfigPath))
                {
                    var displayConfigHelper = new DisplayApp.Utils.ConfigHelper(displayConfigPath);
                    var backgroundColor = displayConfigHelper.GetBackgroundColor();
                    UpdateDisplayColorPreview(backgroundColor);
                }
                else
                {
                    UpdateDisplayColorPreview("#1a1a1a");
                }
                
                // Update sync status
                SyncStatusTextBlock.Text = config.SyncEnabled ? "فعال" : "غیرفعال";
                SyncStatusTextBlock.Foreground = config.SyncEnabled ? Brushes.Green : Brushes.Red;
                
                // Update last update time
                LastUpdateTextBlock.Text = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                
                // Update report files list
                UpdateReportFilesList();
                
                // Update system logs
                UpdateSystemLogs();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating settings display");
            }
        }

        private void UpdateReportFilesList()
        {
            try
            {
                var config = AppConfigHelper.Config;
                var reportsDir = Path.Combine(config.DataDirectory, "Reports");
                
                if (Directory.Exists(reportsDir))
                {
                    var reportFiles = Directory.GetFiles(reportsDir, "report_*.json")
                        .Select(Path.GetFileName)
                        .OrderByDescending(f => f)
                        .ToList();
                    
                    ReportFilesListBox.ItemsSource = reportFiles;
                }
                else
                {
                    ReportFilesListBox.ItemsSource = new List<string> { "پوشه گزارش‌ها یافت نشد" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report files list");
                ReportFilesListBox.ItemsSource = new List<string> { "خطا در بارگذاری فایل‌ها" };
            }
        }

        private void UpdateSystemLogs()
        {
            try
            {
                var config = AppConfigHelper.Config;
                var logsDir = Path.Combine(config.DataDirectory, "Logs");
                
                if (Directory.Exists(logsDir))
                {
                    var logFiles = Directory.GetFiles(logsDir, "*.log")
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .Take(5)
                        .ToList();
                    
                    var logContent = new System.Text.StringBuilder();
                    
                    foreach (var logFile in logFiles)
                    {
                        logContent.AppendLine($"=== {Path.GetFileName(logFile)} ===");
                        try
                        {
                            var lines = File.ReadAllLines(logFile).TakeLast(10);
                            foreach (var line in lines)
                            {
                                logContent.AppendLine(line);
                            }
                        }
                        catch
                        {
                            logContent.AppendLine("خطا در خواندن فایل لاگ");
                        }
                        logContent.AppendLine();
                    }
                    
                    SystemLogsTextBlock.Text = logContent.ToString();
                }
                else
                {
                    SystemLogsTextBlock.Text = "پوشه لاگ‌ها یافت نشد";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system logs");
                SystemLogsTextBlock.Text = $"خطا در بارگذاری لاگ‌ها: {ex.Message}";
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _timer?.Stop();
                _controller?.Cleanup();
                _logger.LogInformation("MainWindow closed");
                
                // Clear static instance when window is closed
                if (Instance == this)
                {
                    Instance = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing MainWindow");
            }
            
            base.OnClosed(e);
        }

        #endregion

        // Daily Progress Tracking Methods
        private void LoadDailyProgressGroups()
        {
            try
            {
                var shiftGroups = _controller.GetAllShiftGroups();
                DailyProgressGroupComboBox.Items.Clear();
                WeeklyProgressGroupComboBox.Items.Clear();
                
                foreach (var group in shiftGroups)
                {
                    var item = new ComboBoxItem
                    {
                        Content = group.Name,
                        Tag = group.GroupId,
                        ToolTip = group.Description
                    };
                    DailyProgressGroupComboBox.Items.Add(item);
                    
                    var weeklyItem = new ComboBoxItem
                    {
                        Content = group.Name,
                        Tag = group.GroupId,
                        ToolTip = group.Description
                    };
                    WeeklyProgressGroupComboBox.Items.Add(weeklyItem);
                }
                
                // Select default group if available
                if (DailyProgressGroupComboBox.Items.Count > 0)
                {
                    var defaultItem = DailyProgressGroupComboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == "default")
                        ?? (ComboBoxItem)DailyProgressGroupComboBox.Items[0];
                    DailyProgressGroupComboBox.SelectedItem = defaultItem;
                }
                
                if (WeeklyProgressGroupComboBox.Items.Count > 0)
                {
                    var defaultItem = WeeklyProgressGroupComboBox.Items.Cast<ComboBoxItem>()
                        .FirstOrDefault(item => item.Tag?.ToString() == "default")
                        ?? (ComboBoxItem)WeeklyProgressGroupComboBox.Items[0];
                    WeeklyProgressGroupComboBox.SelectedItem = defaultItem;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily progress groups");
            }
        }

        private void DailyProgressGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadDailyProgress();
        }

        private void DailyProgressShiftTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadDailyProgress();
        }

        private void DailyProgressBoxesTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only numeric input
            if (!char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
            }
        }

        private void LoadDailyProgress()
        {
            try
            {
                string? groupId = null;
                if (DailyProgressGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                {
                    groupId = selectedGroupId;
                }
                
                if (string.IsNullOrEmpty(groupId))
                {
                    groupId = "default";
                }
                
                string shiftType = "morning";
                if (DailyProgressShiftTypeComboBox.SelectedItem is ComboBoxItem shiftItem && shiftItem.Tag is string shiftTag)
                {
                    shiftType = shiftTag;
                }
                
                var dateStr = DailyProgressDatePicker.SelectedDate ?? Shared.Utils.GeorgianDateHelper.GetCurrentGeorgianDate();
                var shamsiDate = Shared.Utils.ShamsiDateHelper.GregorianToShamsi(dateStr);
                
                var progress = _controller.GetDailyProgress(groupId, shiftType, shamsiDate);
                var status = _controller.CalculateProgressStatus(groupId, shiftType, shamsiDate);
                
                // Update UI
                if (progress != null)
                {
                    DailyProgressBoxesTextBox.Text = progress.CompletedBoxes.ToString();
                }
                else
                {
                    DailyProgressBoxesTextBox.Text = "0";
                }
                
                DisplayDailyProgressStatus(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily progress");
            }
        }

        private void DisplayDailyProgressStatus(ProgressStatus status)
        {
            DailyProgressCompletedTextBlock.Text = status.Completed.ToString();
            DailyProgressTargetTextBlock.Text = status.Target.ToString();
            DailyProgressPercentageTextBlock.Text = status.Percentage.ToString("F1");
            DailyProgressDifferenceTextBlock.Text = status.Difference >= 0 ? $"+{status.Difference}" : status.Difference.ToString();
            
            string statusText = $"{status.StatusText} ({status.Percentage:F1}%)";
            if (status.IsAhead)
            {
                statusText += $" - {status.Difference} جعبه جلوتر";
                DailyProgressStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            else if (status.IsBehind)
            {
                statusText += $" - {Math.Abs(status.Difference)} جعبه عقب‌تر";
                DailyProgressStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
            else
            {
                DailyProgressStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            
            DailyProgressStatusTextBlock.Text = statusText;
        }

        private void RecordDailyProgress_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? groupId = null;
                if (DailyProgressGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                {
                    groupId = selectedGroupId;
                }
                
                if (string.IsNullOrEmpty(groupId))
                {
                    MessageBox.Show("لطفاً یک گروه شیفت را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                string shiftType = "morning";
                if (DailyProgressShiftTypeComboBox.SelectedItem is ComboBoxItem shiftItem && shiftItem.Tag is string shiftTag)
                {
                    shiftType = shiftTag;
                }
                
                var dateStr = DailyProgressDatePicker.SelectedDate ?? Shared.Utils.GeorgianDateHelper.GetCurrentGeorgianDate();
                var shamsiDate = Shared.Utils.ShamsiDateHelper.GregorianToShamsi(dateStr);
                
                if (!int.TryParse(DailyProgressBoxesTextBox.Text, out int completedBoxes) || completedBoxes < 0)
                {
                    MessageBox.Show("لطفاً تعداد جعبه‌های تکمیل شده را به صورت عدد صحیح غیرمنفی وارد کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var success = _controller.RecordDailyProgress(groupId, shiftType, shamsiDate, completedBoxes);
                if (success)
                {
                    LoadDailyProgress();
                    LoadWeeklyProgress();
                    UpdateStatus($"پیشرفت روزانه ثبت شد: {completedBoxes} جعبه");
                }
                else
                {
                    MessageBox.Show("خطا در ثبت پیشرفت روزانه", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording daily progress");
                MessageBox.Show($"خطا در ثبت پیشرفت: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WeeklyProgressGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadWeeklyProgress();
        }

        private void WeeklyProgressShiftTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadWeeklyProgress();
        }

        private void LoadWeeklyProgress()
        {
            try
            {
                string? groupId = null;
                if (WeeklyProgressGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string selectedGroupId)
                {
                    groupId = selectedGroupId;
                }
                
                if (string.IsNullOrEmpty(groupId))
                {
                    groupId = "default";
                }
                
                string shiftType = "morning";
                if (WeeklyProgressShiftTypeComboBox.SelectedItem is ComboBoxItem shiftItem && shiftItem.Tag is string shiftTag)
                {
                    shiftType = shiftTag;
                }
                
                // Get current date and calculate week start
                var currentDate = DailyProgressDatePicker.SelectedDate ?? Shared.Utils.GeorgianDateHelper.GetCurrentGeorgianDate();
                var currentShamsiDate = Shared.Utils.ShamsiDateHelper.GregorianToShamsi(currentDate);
                var weekStartDate = _controller.GetWeekStartDate(currentShamsiDate);
                
                var weeklyStatus = _controller.GetWeeklyProgressStatus(groupId, shiftType, weekStartDate);
                
                // Update UI
                WeeklyProgressWeekStartTextBlock.Text = Shared.Utils.ShamsiDateHelper.FormatForDisplay(weekStartDate);
                WeeklyProgressTotalCompletedTextBlock.Text = weeklyStatus.TotalCompleted.ToString();
                WeeklyProgressPercentageTextBlock.Text = weeklyStatus.Percentage.ToString("F1");
                WeeklyProgressDifferenceTextBlock.Text = weeklyStatus.Difference >= 0 ? $"+{weeklyStatus.Difference}" : weeklyStatus.Difference.ToString();
                
                string statusText = $"{weeklyStatus.StatusText} ({weeklyStatus.Percentage:F1}%)";
                if (weeklyStatus.IsAhead)
                {
                    statusText += $" - {weeklyStatus.Difference} جعبه جلوتر";
                    WeeklyProgressStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                else if (weeklyStatus.IsBehind)
                {
                    statusText += $" - {Math.Abs(weeklyStatus.Difference)} جعبه عقب‌تر";
                    WeeklyProgressStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
                else
                {
                    WeeklyProgressStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                
                WeeklyProgressStatusTextBlock.Text = statusText;
                
                // Populate daily breakdown DataGrid
                var dailyBreakdown = weeklyStatus.DailyProgress.Select(p => new
                {
                    Date = Shared.Utils.ShamsiDateHelper.FormatForDisplay(p.Date),
                    CompletedBoxes = p.CompletedBoxes,
                    DailyTarget = p.DailyTarget,
                    Percentage = p.DailyTarget > 0 ? Math.Round((p.CompletedBoxes / (double)p.DailyTarget * 100), 1) : 0,
                    StatusText = p.CompletedBoxes > p.DailyTarget ? "در حال پیشرفت" : 
                                (p.CompletedBoxes < p.DailyTarget ? "عقب افتاده" : "در مسیر"),
                    DateDisplay = Shared.Utils.ShamsiDateHelper.FormatForDisplay(p.Date)
                }).ToList();
                
                WeeklyProgressDataGrid.ItemsSource = dailyBreakdown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading weekly progress");
            }
        }
    }
}