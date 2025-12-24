using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
            InitializeComponent();
            
            // Add EmployeePhotoConverter to resources using the property instance
            Resources["EmployeePhotoConverter"] = EmployeePhotoConverter;
            
            _controller = new MainController();
            _logger = LoggingService.CreateLogger<MainWindow>();
            _pdfService = new PdfReportService();
            
            // Set static instance for dialogs to access
            Instance = this;
            
            // Setup timer for status updates
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Subscribe to controller events
            _controller.EmployeesUpdated += OnEmployeesUpdated;
            _controller.RolesUpdated += OnRolesUpdated;
            _controller.ShiftGroupsUpdated += OnShiftGroupsUpdated;
            
            // Initialize settings display
            UpdateSettingsDisplay();
            _controller.ShiftsUpdated += OnShiftsUpdated;
            _controller.AbsencesUpdated += OnAbsencesUpdated;
            _controller.TasksUpdated += OnTasksUpdated;
            _controller.SettingsUpdated += OnSettingsUpdated;
            _controller.SyncTriggered += OnSyncTriggered;

            // Initialize UI
            InitializeUI();
            LoadData();

            // Debug data persistence issues
            _controller.DebugDataPersistence();

            _logger.LogInformation("MainWindow initialized");
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

                // Subscribe to LayoutUpdated to re-attach drag handlers when items are regenerated
                // This ensures handlers are attached after tab switches or other UI updates
                ShiftEmployeeListBox.LayoutUpdated += ShiftEmployeeListBox_LayoutUpdated;

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

        private void LoadData()
        {
            try
            {
                // #region agent log
                File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                    $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:197\",\"message\":\"LoadData called, about to call LoadEmployees\",\"data\":{{}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"E\"}}\n");
                // #endregion
                LoadEmployees();
                LoadShifts();
                LoadAbsences();
                LoadTasks();
                UpdateStatus("داده‌ها بارگذاری شدند");
                
                _logger.LogInformation("Data loaded successfully");
            }
            catch (Exception ex)
            {
                // #region agent log
                File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                    $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:209\",\"message\":\"LoadData exception\",\"data\":{{\"error\":\"{ex.Message}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"E\"}}\n");
                // #endregion
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
                    // Detect name from folder and personnel ID from filename if photo is in a worker folder
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
                        // Update employee photo path (will be copied to worker folder automatically)
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

        private void DisplayGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (DisplayGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string groupId)
                {
                    _controller.SelectedDisplayGroupId = groupId;
                    _controller.SaveData();
                    UpdateStatus($"گروه نمایش به {selectedItem.Content} تغییر کرد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling display group selection change");
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
                
                // Load absent employees
                var todayGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
                var absentEmployees = _controller.GetAllEmployees().Where(emp => 
                    _controller.AbsenceManager.HasAbsenceForEmployee(emp, todayGeorgian)).ToList();
                AbsentEmployeeListBox.ItemsSource = absentEmployees;
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
                int eveningCapacity = selectedGroup?.EveningCapacity ?? _controller.ShiftManager.Capacity;
                
                _logger.LogInformation("LoadShiftSlots: Selected group: {GroupName}, Morning: {MorningCapacity}, Evening: {EveningCapacity}", 
                    selectedGroup?.Name ?? "Default", morningCapacity, eveningCapacity);
                
                // Clear existing slots
                MorningShiftPanel.Children.Clear();
                EveningShiftPanel.Children.Clear();

                // Update capacity text box to show morning capacity (primary)
                ShiftCapacityTextBox.Text = morningCapacity.ToString();
                _logger.LogInformation("LoadShiftSlots: TextBox updated to {Capacity}", morningCapacity);

                // Create morning shift slots in a grid layout
                var morningGrid = CreateShiftGrid("morning", morningCapacity, selectedGroup);
                MorningShiftPanel.Children.Add(morningGrid);

                // Create evening shift slots in a grid layout
                var eveningGrid = CreateShiftGrid("evening", eveningCapacity, selectedGroup);
                EveningShiftPanel.Children.Add(eveningGrid);
                
                _logger.LogInformation("LoadShiftSlots: Grids created with capacities - Morning: {MorningCapacity}, Evening: {EveningCapacity}", 
                    morningCapacity, eveningCapacity);
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
                    var shift = shiftType == "morning" ? selectedGroup.MorningShift : selectedGroup.EveningShift;
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

                stackPanel.Children.Add(image);
                stackPanel.Children.Add(nameTextBlock);
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
                int morningCount, eveningCount, morningCapacity, eveningCapacity;
                
                if (selectedGroup != null)
                {
                    morningCount = selectedGroup.MorningShift.AssignedEmployees.Count(emp => emp != null);
                    eveningCount = selectedGroup.EveningShift.AssignedEmployees.Count(emp => emp != null);
                    morningCapacity = selectedGroup.MorningCapacity;
                    eveningCapacity = selectedGroup.EveningCapacity;
                }
                else
                {
                    morningCount = _controller.ShiftManager.MorningShift.AssignedEmployees.Count(emp => emp != null);
                    eveningCount = _controller.ShiftManager.EveningShift.AssignedEmployees.Count(emp => emp != null);
                    morningCapacity = _controller.ShiftManager.Capacity;
                    eveningCapacity = _controller.ShiftManager.Capacity;
                }

                MorningShiftStats.Text = $"{morningCount}/{morningCapacity}";
                EveningShiftStats.Text = $"{eveningCount}/{eveningCapacity}";
                
                _logger.LogInformation("Updated shift statistics - Morning: {MorningCount}/{MorningCapacity}, Evening: {EveningCount}/{EveningCapacity}", 
                    morningCount, morningCapacity, eveningCount, eveningCapacity);
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

        private void ClearEveningShift_Click(object sender, RoutedEventArgs e)
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
                    
                    _controller.ClearShift("evening", groupId);
                    LoadShiftSlots();
                    UpdateShiftStatistics();
                    UpdateStatus("شیفت عصر پاک شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing evening shift");
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
                _controller.Settings["auto_rotate_day"] = "Saturday"; // Default to Saturday
                _controller.NotifySettingsUpdated();
                _controller.SaveData();
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
                UpdateStatus("جابجایی خودکار شیفت‌ها غیرفعال شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling auto-rotation");
            }
        }

        #endregion

        #region Drag and Drop

        private Point _dragStartPoint;
        private Employee? _draggedEmployee;

        private void AttachDragHandlers()
        {
            try
            {
                _logger.LogInformation("AttachDragHandlers: Starting to attach handlers for {Count} items", ShiftEmployeeListBox.Items.Count);
                
                int itemsProcessed = 0;
                int bordersFound = 0;
                int handlersAttached = 0;
                
                for (int i = 0; i < ShiftEmployeeListBox.Items.Count; i++)
                {
                    if (ShiftEmployeeListBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
                    {
                        var employee = item.DataContext as Employee;
                        if (employee != null)
                        {
                            itemsProcessed++;
                            
                            // Find the Border in the template using VisualTreeHelper
                            Border? border = FindVisualChild<Border>(item);
                            
                            if (border != null && border.Name == "EmployeeCardBorder")
                            {
                                bordersFound++;
                                border.Tag = employee;
                                
                                // Set image source for employee photo
                                Image? image = FindVisualChild<Image>(border);
                                if (image != null)
                                {
                                    image.Source = EmployeePhotoConverter.Convert(employee, typeof(ImageSource), null, System.Globalization.CultureInfo.CurrentCulture) as ImageSource;
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
                                
                                handlersAttached++;
                                _logger.LogDebug("AttachDragHandlers: Attached handlers to border for employee {EmployeeName}", employee.FullName);
                            }
                            else
                            {
                                // Fallback: attach to ListBoxItem if Border not found
                                item.Tag = employee;
                                item.PreviewMouseLeftButtonDown -= ListBoxItem_PreviewMouseLeftButtonDown;
                                item.PreviewMouseLeftButtonDown += ListBoxItem_PreviewMouseLeftButtonDown;
                                item.MouseMove -= ListBoxItem_MouseMove;
                                item.MouseMove += ListBoxItem_MouseMove;
                                
                                _logger.LogDebug("AttachDragHandlers: Attached handlers to ListBoxItem (fallback) for employee {EmployeeName}", employee.FullName);
                            }
                        }
                    }
                }
                
                _logger.LogInformation("AttachDragHandlers: Completed - {ItemsProcessed} items processed, {BordersFound} borders found, {HandlersAttached} handlers attached", 
                    itemsProcessed, bordersFound, handlersAttached);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attaching drag handlers");
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
                // When shift management tab (index 1) is selected, ensure handlers are attached
                if (MainTabControl.SelectedIndex == 1) // Shift management tab
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MainTabControl_SelectionChanged");
            }
        }
        
        private void ShiftEmployeeListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // #region agent log
            _logger.LogInformation("ShiftEmployeeListBox_PreviewMouseLeftButtonDown fired");
            // #endregion
            
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
                        // #region agent log
                        _logger.LogInformation("Drag start point set for employee: {EmployeeName}", employee.FullName);
                        // #endregion
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
                        // #region agent log
                        _logger.LogInformation("Starting DoDragDrop for employee: {EmployeeName}", _draggedEmployee.FullName);
                        // #endregion
                        
                        var dragData = new DataObject(typeof(Employee), _draggedEmployee);
                        var result = DragDrop.DoDragDrop(ShiftEmployeeListBox, dragData, DragDropEffects.Move);
                        
                        // #region agent log
                        _logger.LogInformation("DoDragDrop completed with result: {Result}", result);
                        // #endregion
                        
                        _draggedEmployee = null;
                    }
                }
            }
            catch (Exception ex)
            {
                // #region agent log
                _logger.LogError(ex, "Error in ShiftEmployeeListBox_MouseMove");
                // #endregion
            }
        }
        
        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // #region agent log
            try { File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:ListBoxItem_PreviewMouseLeftButtonDown\",\"message\":\"ListBoxItem PreviewMouseLeftButtonDown fired\",\"data\":{{\"senderType\":\"{sender?.GetType().Name}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\"}}\n"); } catch {}
            // #endregion
            
            if (sender is ListBoxItem item && item.Tag is Employee employee)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedEmployee = employee;
                // #region agent log
                try { File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                    $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:ListBoxItem_PreviewMouseLeftButtonDown\",\"message\":\"ListBoxItem drag start point set\",\"data\":{{\"employeeName\":\"{employee.FullName?.Replace("\"", "\\\"") ?? "null"}\",\"x\":{_dragStartPoint.X},\"y\":{_dragStartPoint.Y}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\"}}\n"); } catch {}
                // #endregion
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

                        // #region agent log
                        try { File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                            $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:ListBoxItem_MouseMove\",\"message\":\"ListBoxItem MouseMove with button pressed\",\"data\":{{\"diffX\":{diff.X},\"diffY\":{diff.Y},\"minDistance\":{SystemParameters.MinimumHorizontalDragDistance}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n"); } catch {}
                        // #endregion

                        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                        {
                            // #region agent log
                            try { File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                                $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:ListBoxItem_MouseMove\",\"message\":\"Starting DoDragDrop from ListBoxItem\",\"data\":{{\"employeeName\":\"{employee.FullName?.Replace("\"", "\\\"") ?? "null"}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n"); } catch {}
                            // #endregion
                            
                            var dragData = new DataObject(typeof(Employee), employee);
                            var result = DragDrop.DoDragDrop(item, dragData, DragDropEffects.Move);
                            
                            // #region agent log
                            try { File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                                $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:ListBoxItem_MouseMove\",\"message\":\"DoDragDrop completed from ListBoxItem\",\"data\":{{\"result\":\"{result}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n"); } catch {}
                            // #endregion
                            
                            _draggedEmployee = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // #region agent log
                try { File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                    $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:ListBoxItem_MouseMove\",\"message\":\"ListBoxItem MouseMove exception\",\"data\":{{\"error\":\"{ex.Message?.Replace("\"", "\\\"") ?? "null"}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n"); } catch {}
                // #endregion
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
            // #region agent log
            File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:1354\",\"message\":\"PreviewMouseLeftButtonDown fired\",\"data\":{{\"senderType\":\"{sender?.GetType().Name}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\"}}\n");
            // #endregion
            
            if (sender is Border border && border.Tag is Employee employee)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedEmployee = employee;
                // #region agent log
                File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                    $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:1359\",\"message\":\"Drag start point set\",\"data\":{{\"employeeName\":\"{employee.FullName}\",\"x\":{_dragStartPoint.X},\"y\":{_dragStartPoint.Y}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\"}}\n");
                // #endregion
            }
            else
            {
                // #region agent log
                File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                    $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:1361\",\"message\":\"PreviewMouseLeftButtonDown - invalid sender or tag\",\"data\":{{\"isBorder\":{(sender is Border)},\"hasTag\":{(sender is Border border2 && border2.Tag != null)}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\"}}\n");
                // #endregion
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

                        // #region agent log
                        File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                            $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:1369\",\"message\":\"MouseMove with button pressed\",\"data\":{{\"diffX\":{diff.X},\"diffY\":{diff.Y},\"minDistance\":{SystemParameters.MinimumHorizontalDragDistance}}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                        // #endregion

                        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                        {
                            // #region agent log
                            File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                                $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:1377\",\"message\":\"Starting DoDragDrop\",\"data\":{{\"employeeName\":\"{employee.FullName}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                            // #endregion
                            
                            var dragData = new DataObject(typeof(Employee), employee);
                            var result = DragDrop.DoDragDrop(border, dragData, DragDropEffects.Move);
                            
                            // #region agent log
                            File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                                $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:1379\",\"message\":\"DoDragDrop completed\",\"data\":{{\"result\":\"{result}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                            // #endregion
                            
                            _draggedEmployee = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // #region agent log
                File.AppendAllText(@"d:\projects\employee_management_csharp\.cursor\debug.log", 
                    $"{{\"id\":\"log_{DateTime.Now.Ticks}\",\"timestamp\":{DateTimeOffset.Now.ToUnixTimeMilliseconds()},\"location\":\"MainWindow.xaml.cs:1385\",\"message\":\"MouseMove exception\",\"data\":{{\"error\":\"{ex.Message}\"}},\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\"}}\n");
                // #endregion
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
                        
                        var success = _controller.AssignEmployeeToShift(employee, shiftType, slotIndex, groupId);
                        if (success)
                        {
                            LoadShiftSlots();
                            UpdateShiftStatistics();
                            LoadEmployees(); // Refresh employee lists
                            UpdateStatus($"کارمند {employee.FullName} به شیفت {shiftType} تخصیص داده شد");
                        }
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
                            // Try to detect employee name from folder and personnel ID from filename
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
                                    
                                    var success = _controller.AssignEmployeeToShift(employee, shiftType, slotIndex, groupId);
                                    if (success)
                                    {
                                        LoadShiftSlots();
                                        UpdateShiftStatistics();
                                        LoadEmployees();
                                        UpdateStatus($"عکس کارمند {employee.FullName} به‌روزرسانی شد و به شیفت تخصیص داده شد");
                                    }
                                }
                                else
                                {
                                    // Create new employee automatically from folder name
                                    var result = MessageBox.Show(
                                        $"کارمند {detectedFirstName} {detectedLastName} یافت نشد.\nآیا می‌خواهید کارمند جدیدی با این نام ایجاد شود؟",
                                        "ایجاد کارمند جدید",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);
                                    
                                    if (result == MessageBoxResult.Yes)
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
                                                
                                                var success = _controller.AssignEmployeeToShift(createdEmployee, shiftType, slotIndex, groupId);
                                                if (success)
                                                {
                                                    LoadShiftSlots();
                                                    UpdateShiftStatistics();
                                                    LoadEmployees();
                                                    UpdateStatus($"کارمند جدید {detectedFirstName} {detectedLastName} ایجاد شد و به شیفت تخصیص داده شد");
                                                }
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
                                UpdateStatus("نام کارمند از پوشه تشخیص داده نشد. لطفاً عکس را در پوشه Workers/FirstName_LastName/ قرار دهید.");
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
                            // Detect name from folder and personnel ID from filename if possible
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
                        var shift = shiftType == "morning" ? selectedGroup.MorningShift : selectedGroup.EveningShift;
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
                        
                        var success = _controller.AssignEmployeeToShift(selectedEmployee, shiftType, slotIndex, groupId);
                        if (success)
                        {
                            LoadShiftSlots();
                            UpdateShiftStatistics();
                            LoadEmployees(); // Refresh employee lists
                            UpdateStatus($"کارمند {selectedEmployee.FullName} به شیفت {shiftType} تخصیص داده شد");
                        }
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

                var availableEmployees = _controller.GetAvailableEmployeesForTask(taskId);
                
                if (availableEmployees.Count == 0)
                {
                    MessageBox.Show("همه کارمندان به این وظیفه تخصیص داده شده‌اند", "اطلاع", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new Window
                {
                    Title = $"تخصیص کارمند به وظیفه: {taskTitle}",
                    Width = 400,
                    Height = 500,
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
                    Content = "کارمند مورد نظر را انتخاب کنید:",
                    FontSize = 12,
                    Margin = new Thickness(10, 10, 10, 5),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(label, 0);
                grid.Children.Add(label);

                var listBox = new ListBox
                {
                    DisplayMemberPath = "FullName",
                    ItemsSource = availableEmployees,
                    Height = 300,
                    Margin = new Thickness(10, 5, 10, 5)
                };
                Grid.SetRow(listBox, 1);
                grid.Children.Add(listBox);

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
                    if (listBox.SelectedItem is Employee selectedEmployee)
                    {
                        var success = _controller.AssignTaskToEmployee(taskId, selectedEmployee.EmployeeId);
                        if (success)
                        {
                            LoadTaskAssignments();
                            UpdateStatus($"{selectedEmployee.FullName} به وظیفه {taskTitle} تخصیص داده شد");
                            dialog.Close();
                        }
                    }
                    else
                    {
                        MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    Title = "ذخیره گزارش",
                    DefaultExt = "pdf"
                };

                // Get current report details for default filename
                var reportType = ReportTypeComboBox.SelectedItem?.ToString() ?? "گزارش";
                var startDate = ReportStartDatePicker.SelectedDate?.ToString() ?? "";
                var endDate = ReportEndDatePicker.SelectedDate?.ToString() ?? "";
                
                if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                {
                    var defaultFileName = _pdfService.GetDefaultFileName(reportType, startDate, endDate);
                    saveFileDialog.FileName = defaultFileName;
                }

                if (saveFileDialog.ShowDialog() == true)
                {
                    var filePath = saveFileDialog.FileName;
                    var fileExtension = Path.GetExtension(filePath).ToLower();

                    if (fileExtension == ".pdf")
                    {
                        // Export as PDF
                        var reportTypeText = "گزارش";
                        if (ReportTypeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedReportType)
                        {
                            reportTypeText = selectedReportType.Content?.ToString() ?? "گزارش";
                        }
                        var reportTitle = $"{reportTypeText} - {startDate} تا {endDate}";
                        
                        var assignedTo = "";
                        if (ReportAssignedToComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedAssignedTo)
                        {
                            assignedTo = selectedAssignedTo.Content?.ToString() ?? "";
                        }
                        else if (ReportAssignedToComboBox.SelectedItem is string assignedToString)
                        {
                            assignedTo = assignedToString;
                        }
                        
                        _logger.LogInformation("Starting PDF export to: {FilePath}", filePath);
                        _logger.LogInformation("Report title: {ReportTitle}", reportTitle);
                        _logger.LogInformation("Report content length: {Length}", ReportPreviewTextBlock.Text?.Length ?? 0);
                        _logger.LogInformation("Assigned to: {AssignedTo}", assignedTo);
                        
                        var reportContent = ReportPreviewTextBlock.Text ?? string.Empty;
                        var success = _pdfService.ExportReportToPdf(reportContent, filePath, reportTitle, assignedTo);
                        
                        if (success)
                        {
                            _logger.LogInformation("PDF export completed successfully");
                            UpdateStatus("گزارش PDF صادر شد");
                            MessageBox.Show("گزارش با موفقیت به فرمت PDF صادر شد.", "موفقیت", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            _logger.LogError("PDF export failed");
                            MessageBox.Show("خطا در صادر کردن گزارش PDF.", "خطا", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        // Export as text file
                        File.WriteAllText(filePath, ReportPreviewTextBlock.Text, System.Text.Encoding.UTF8);
                        UpdateStatus("گزارش متنی صادر شد");
                        MessageBox.Show("گزارش با موفقیت به فرمت متنی صادر شد.", "موفقیت", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report");
                MessageBox.Show($"خطا در صادر کردن گزارش: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    
                    MessageBox.Show("تنظیمات به حالت پیش‌فرض بازنشانی شد.", "موفق", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings");
                MessageBox.Show($"خطا در بازنشانی تنظیمات: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}