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

    /// <summary>
    /// Helper class for binding resource keys in the Text Customization DataGrid.
    /// </summary>
    public class ResourceOverrideItem : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _defaultValue = string.Empty;
        private string _customValue = string.Empty;

        public string Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged(nameof(Key)); }
        }

        public string DefaultValue
        {
            get => _defaultValue;
            set { _defaultValue = value; OnPropertyChanged(nameof(DefaultValue)); }
        }

        public string CustomValue
        {
            get => _customValue;
            set { _customValue = value; OnPropertyChanged(nameof(CustomValue)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
        
        // Label service for employee labels
        private LabelService? _labelService;
        /// <summary>Exposed for controls (e.g. ShiftGroupControl) to assign labels to employees.</summary>
        public LabelService? LabelService => _labelService;
        
        // Converter instance as property for XAML binding
        public ManagementApp.Converters.EmployeePhotoConverter EmployeePhotoConverter { get; } = new ManagementApp.Converters.EmployeePhotoConverter();
        
        // Collection for Table Layout binding
        public System.Collections.ObjectModel.ObservableCollection<ShiftGroup> ShiftGroups { get; } = new System.Collections.ObjectModel.ObservableCollection<ShiftGroup>();
        
        private int _lastTabIndex = 0;
        private bool _isInternalSelectionChange = false;

        public static readonly DependencyProperty BadgeSizeProperty =
            DependencyProperty.Register("BadgeSize", typeof(double), typeof(MainWindow), new PropertyMetadata(250.0));

        public double BadgeSize
        {
            get => (double)GetValue(BadgeSizeProperty);
            set => SetValue(BadgeSizeProperty, value);
        }

        public static readonly DependencyProperty GroupSizeProperty =
            DependencyProperty.Register("GroupSize", typeof(double), typeof(MainWindow), new PropertyMetadata(300.0));

        public double GroupSize
        {
            get => (double)GetValue(GroupSizeProperty);
            set => SetValue(GroupSizeProperty, value);
        }

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
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")} loading UI:\n\n{ex.Message}\n\nDetails:\n{ex}", 
                    ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                _controller.StatusCardsUpdated += OnStatusCardsUpdated;
                _logger.LogInformation("MainWindow: Controller events subscribed");
                
                // Initialize settings display
                UpdateSettingsDisplay();
                Shared.Utils.ResourceBridge.Instance.PropertyChanged += ResourceBridge_PropertyChanged;
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
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")} starting main window:\n\n{ex.Message}\n\nDetails:\n{ex}", 
                    ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    if (EmployeeSearchBox.Text == ResourceManager.GetString("label_search", "Search..."))
                    {
                        EmployeeSearchBox.Text = "";
                        EmployeeSearchBox.Foreground = Brushes.Black;
                    }
                };

                EmployeeSearchBox.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrEmpty(EmployeeSearchBox.Text))
                    {
                        EmployeeSearchBox.Text = ResourceManager.GetString("label_search", "Search...");
                        EmployeeSearchBox.Foreground = Brushes.Gray;
                    }
                };

                EmployeeSearchBox.TextChanged += (s, e) =>
                {
                    if (EmployeeSearchBox.Text != ResourceManager.GetString("label_search", "Search..."))
                    {
                        FilterEmployees(EmployeeSearchBox.Text);
                    }
                };

                // Setup shift employee search
                ShiftEmployeeSearchBox.GotFocus += (s, e) =>
                {
                    if (ShiftEmployeeSearchBox.Text == ResourceManager.GetString("label_search", "Search..."))
                    {
                        ShiftEmployeeSearchBox.Text = "";
                        ShiftEmployeeSearchBox.Foreground = Brushes.Black;
                    }
                };

                ShiftEmployeeSearchBox.LostFocus += (s, e) =>
                {
                    if (string.IsNullOrEmpty(ShiftEmployeeSearchBox.Text))
                    {
                        ShiftEmployeeSearchBox.Text = ResourceManager.GetString("label_search", "Search...");
                        ShiftEmployeeSearchBox.Foreground = Brushes.Gray;
                    }
                };

                ShiftEmployeeSearchBox.TextChanged += (s, e) =>
                {
                    if (ShiftEmployeeSearchBox.Text != ResourceManager.GetString("label_search", "Search..."))
                    {
                        FilterShiftEmployees(ShiftEmployeeSearchBox.Text);
                    }
                };

                // Subscribe to LayoutUpdated to re-attach drag handlers when items are regenerated
                // This ensures handlers are attached after tab switches or other UI updates
                ShiftEmployeeListBox.LayoutUpdated += ShiftEmployeeListBox_LayoutUpdated;

                // Initialize rotation configuration
                InitializeRotationConfiguration();

                // Initialize label service and panel
                InitializeLabelPanel();

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
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ResourceManager.GetString("role_ceo", "CEO") });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ResourceManager.GetString("role_hr_manager", "HR Manager") });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ResourceManager.GetString("role_shift_supervisor", "Shift Supervisor") });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ResourceManager.GetString("role_hr_specialist", "HR Specialist") });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ResourceManager.GetString("role_operations_manager", "Operations Manager") });
                ReportAssignedToComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = ResourceManager.GetString("role_deputy_manager", "Deputy Manager") });
                
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

        private void InitializeLabelPanel()
        {
            try
            {
                // Create label service with the same data directory as the controller
                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                _labelService = new LabelService(dataDir);
                _labelService.LoadLabelArchive();
                
                // Initialize the label panel
                LabelPanel.Initialize(_labelService);
                
                // Subscribe to label events
                LabelPanel.LabelCreated += (s, label) =>
                {
                    _logger.LogInformation("Label created: {Text}", label.Text);
                };
                
                LabelPanel.LabelDeleted += (s, labelId) =>
                {
                    _logger.LogInformation("Label deleted from archive: {LabelId}", labelId);
                };
                
                LabelPanel.LabelDragStarted += (s, label) =>
                {
                    _logger.LogDebug("Label drag started: {Text}", label.Text);
                };
                
                _logger.LogInformation("Label panel initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing label panel");
            }
        }

        private void LoadData()
        {
            try
            {
                LoadEmployees();
                LoadShifts();
                LoadStatusCards();
                LoadAbsences();
                LoadTasks();
                UpdateStatus(ResourceManager.GetString("msg_data_loaded", "Data loaded"));
                
                _logger.LogInformation("Data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data");
                UpdateStatus(ResourceManager.GetString("msg_error_loading_data", "Error loading data"));
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
                if (string.IsNullOrEmpty(query) || query == ResourceManager.GetString("label_search", "Search..."))
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
                
                if (string.IsNullOrEmpty(query) || query == ResourceManager.GetString("label_search", "Search..."))
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
                                                         dialog.ShieldColor, dialog.ShowShield, dialog.StickerPaths, dialog.MedalBadgePath, dialog.PersonnelId,
                                                         dialog.Phone, dialog.ShowPhone);
                    if (success)
                    {
                        LoadEmployees();
                        UpdateStatus(string.Format(ResourceManager.GetString("msg_employee_added", "Employee {0} {1} added"), dialog.FirstName, dialog.LastName));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding employee");
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")}: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show(ResourceManager.GetString("msg_select_employee", "Please select an employee"), ResourceManager.GetString("msg_warning", "Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new EmployeeDialog(_selectedEmployee, _controller);
                if (dialog.ShowDialog() == true)
                {
                    var success = _controller.UpdateEmployee(_selectedEmployee.EmployeeId, dialog.FirstName, dialog.LastName, dialog.RoleId, dialog.ShiftGroupId, dialog.PhotoPath, dialog.IsManager,
                                                           dialog.ShieldColor, dialog.ShowShield, dialog.StickerPaths, dialog.MedalBadgePath, dialog.PersonnelId,
                                                           dialog.Phone, dialog.ShowPhone);
                    if (success)
                    {
                        LoadEmployees();
                        LoadEmployeeDetails(_selectedEmployee);
                        UpdateStatus(string.Format(ResourceManager.GetString("msg_employee_updated", "Employee {0} {1} updated"), dialog.FirstName, dialog.LastName));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing employee");
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")}: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageRoles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new RoleDialog(_controller);
                dialog.ShowDialog();
                // Roles are automatically saved when modified in the dialog
                UpdateStatus(ResourceManager.GetString("msg_role_mgmt_completed", "Role management completed"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing roles");
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")}: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageShiftGroups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ShiftGroupDialog(_controller);
                dialog.ShowDialog();
                // Shift groups are automatically saved when modified in the dialog
                UpdateStatus(ResourceManager.GetString("msg_shift_group_mgmt_completed", "Shift group management completed"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing shift groups");
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")}: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageStatusCards_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new StatusCardDialog(_controller);
                dialog.ShowDialog();
                // Status cards are automatically saved when modified in the dialog
                UpdateStatus(ResourceManager.GetString("msg_status_card_mgmt_completed", "Status card management completed"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing status cards");
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")}: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(ResourceManager.GetString("msg_select_employee", "Please select an employee"), ResourceManager.GetString("msg_warning", "Warning"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _logger.LogInformation("DeleteEmployee_Click: Selected employee: {FullName} (ID: {EmployeeId})", 
                    _selectedEmployee.FullName, _selectedEmployee.EmployeeId);

                var result = MessageBox.Show(string.Format(ResourceManager.GetString("msg_confirm_delete_employee", "Are you sure you want to delete employee {0}?"), _selectedEmployee.FullName), 
                    ResourceManager.GetString("header_confirm_delete", "Confirm Delete"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _logger.LogInformation("DeleteEmployee_Click: User confirmed deletion, calling controller");
                    
                    if (_controller == null)
                    {
                        _logger.LogError("DeleteEmployee_Click: Controller is null!");
                        MessageBox.Show(ResourceManager.GetString("msg_controller_error", "Controller error"), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                        UpdateStatus(string.Format(ResourceManager.GetString("msg_employee_deleted", "Employee {0} deleted"), employeeName));
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
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")}: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportCSV_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    Title = ResourceManager.GetString("msg_select_csv", "Select CSV File")
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var (imported, skipped) = _controller.ImportEmployeesFromCsv(openFileDialog.FileName);
                    LoadEmployees();
                    UpdateStatus(string.Format(ResourceManager.GetString("msg_import_result", "{0} employees imported, {1} skipped"), imported, skipped));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing CSV");
                MessageBox.Show($"{ResourceManager.GetString("msg_error", "Error")}: {ex.Message}", ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("Please select an employee", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
                    Title = "Select Employee Photo"
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
                                $"Personnel ID detected from file name: {detectedPersonnelId}\nDo you want to update the employee's personnel ID?",
                                "Personnel ID Detected",
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
                                $"Name detected from folder: {detectedFirstName} {detectedLastName}\nDo you want to update the employee name?",
                                "Name Detected from Folder",
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
                    UpdateStatus("Employee photo updated");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting photo");
                MessageBox.Show($"Error selecting photo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateBadge_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("Please select an employee", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate employee has photo
                if (string.IsNullOrEmpty(_selectedEmployee.PhotoPath) || !_selectedEmployee.HasPhoto())
                {
                    MessageBox.Show("The selected employee has no photo. Please select a photo for the employee first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Generate badge
                var badgePath = _controller.GenerateEmployeeBadge(_selectedEmployee.EmployeeId);

                if (!string.IsNullOrEmpty(badgePath))
                {
                    MessageBox.Show($"ID card generated successfully:\n{badgePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStatus("ID card generated");
                    
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
                    MessageBox.Show("Error generating ID card. Please ensure the ID card template is in the correct path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating badge");
                MessageBox.Show($"Error generating ID card: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveEmployeeChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("Please select an employee", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedRoleId = (RoleComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "employee";
                var success = _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                    FirstNameTextBox.Text, LastNameTextBox.Text, selectedRoleId, _selectedEmployee.ShiftGroupId, null, IsManagerCheckBox.IsChecked);
                
                if (success)
                {
                    LoadEmployees();
                    UpdateStatus("Employee changes saved");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving employee changes");
                MessageBox.Show($"Error saving changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MarkAbsent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("Please select an employee", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedType = (AbsenceTypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (string.IsNullOrEmpty(selectedType))
                {
                    MessageBox.Show("Please select an absence type", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var customDate = AbsenceDatePicker.SelectedDate;
                var success = _controller.MarkEmployeeAbsent(_selectedEmployee, selectedType, AbsenceNotesTextBox.Text, customDate);
                if (success)
                {
                    LoadEmployeeAbsences(_selectedEmployee);
                    LoadEmployees(); // Refresh to update shift availability
                    LoadAbsenceLists(); // Refresh categorized absence lists
                    UpdateStatus($"Employee {_selectedEmployee.FullName} marked as {selectedType}");
                    AbsenceNotesTextBox.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking employee absent");
                MessageBox.Show($"Error recording absence: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveAbsence_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("Please select an employee", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var success = _controller.RemoveAbsence(_selectedEmployee);
                if (success)
                {
                    LoadEmployeeAbsences(_selectedEmployee);
                    LoadEmployees(); // Refresh to update shift availability
                    LoadAbsenceLists(); // Refresh categorized absence lists
                    UpdateStatus($"Absence for {_selectedEmployee.FullName} removed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing absence");
                MessageBox.Show($"Error removing absence: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Shift Management

        private void LoadShiftGroups()
        {
            try
            {
                var groups = _controller.GetAllShiftGroups().OrderBy(g => g.Name).ToList();
                
                // Update ShiftGroups collection
                if (ShiftGroups != null)
                {
                    ShiftGroups.Clear();
                    foreach (var group in groups)
                    {
                        ShiftGroups.Add(group);
                    }
                }

                // Update ShiftGroupComboBox
                if (ShiftGroupComboBox != null)
                {
                    var selectedId = (ShiftGroupComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
                    ShiftGroupComboBox.Items.Clear();
                    
                    var allItem = new ComboBoxItem { Content = ResourceManager.GetString("role_all_groups", "All groups"), Tag = "all" };
                    ShiftGroupComboBox.Items.Add(allItem);
                    
                    foreach (var group in groups)
                    {
                        var item = new ComboBoxItem { Content = group.Name, Tag = group.GroupId };
                        ShiftGroupComboBox.Items.Add(item);
                        if (group.GroupId == selectedId)
                        {
                            ShiftGroupComboBox.SelectedItem = item;
                        }
                    }
                    
                    if (ShiftGroupComboBox.SelectedItem == null && ShiftGroupComboBox.Items.Count > 0)
                    {
                        ShiftGroupComboBox.SelectedIndex = 0;
                    }
                }
                
                // Ensure ItemsControl is bound
                if (ShiftGroupsItemsControl != null && ShiftGroupsItemsControl.ItemsSource == null && ShiftGroups != null)
                {
                    ShiftGroupsItemsControl.ItemsSource = ShiftGroups;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shift groups");
            }
        }




        public void LoadShifts()
        {
             LoadShiftGroups();
             LoadAbsenceLists();
        }











        /// <summary>Width below which the shift toolbar action buttons are hidden (e.g. when Absence Management or left pane is expanded).</summary>
        private const double ShiftToolbarButtonsHideWidthThreshold = 560;

        private void ShiftAssignmentGroupBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ShiftToolbarButtonsPanel == null) return;
            ShiftToolbarButtonsPanel.Visibility = ShiftAssignmentGroupBox.ActualWidth >= ShiftToolbarButtonsHideWidthThreshold
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ClearShiftButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClearShiftContextMenu == null || ClearShiftButton == null) return;
            ClearShiftContextMenu.PlacementTarget = ClearShiftButton;
            ClearShiftContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ClearShiftContextMenu.IsOpen = true;
        }

        private void ClearShiftOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.Tag is not string shiftType)
                return;
            ClearShiftContextMenu.IsOpen = false;

            string confirmMessage = shiftType switch
            {
                "morning" => ResourceManager.GetString("msg_confirm_clear_morning", "Are you sure you want to clear all morning shifts?"),
                "afternoon" => ResourceManager.GetString("msg_confirm_clear_afternoon", "Are you sure you want to clear all afternoon shifts?"),
                "night" => ResourceManager.GetString("msg_confirm_clear_night", "Are you sure you want to clear all night shifts?"),
                _ => null
            };
            if (string.IsNullOrEmpty(confirmMessage)) return;

            try
            {
                if (MessageBox.Show(confirmMessage, ResourceManager.GetString("header_confirm_delete", "Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                _controller.ClearShift(shiftType, null);
                LoadShifts();
                string statusMessage = shiftType switch
                {
                    "morning" => ResourceManager.GetString("msg_morning_cleared", "Morning shift cleared"),
                    "afternoon" => ResourceManager.GetString("msg_afternoon_cleared", "Afternoon shift cleared"),
                    "night" => ResourceManager.GetString("msg_night_cleared", "Night shift cleared"),
                    _ => string.Format(ResourceManager.GetString("msg_shift_cleared", "{0} shift cleared"), shiftType)
                };
                UpdateStatus(statusMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing {ShiftType} shift", shiftType);
            }
        }

        private void SwapShifts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? groupId = null;
                if (ShiftGroupComboBox?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string tag)
                {
                    groupId = tag == "all" ? null : tag;
                }

                if (MessageBox.Show(ResourceManager.GetString("msg_confirm_rotate_shifts", "Are you sure you want to rotate morning, afternoon and night shifts?"), ResourceManager.GetString("header_confirm_delete", "Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    if (_controller.SwapShifts(groupId))
                    {
                        LoadShifts();
                        UpdateStatus(ResourceManager.GetString("msg_shifts_swapped", "Shifts swapped successfully"));
                    }
                    else
                    {
                        MessageBox.Show(ResourceManager.GetString("err_rotate_shifts", "Error rotating shifts"), ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error swapping shifts");
            }
        }

        private void OnStatusCardsUpdated()
        {
            Dispatcher.Invoke(LoadStatusCards);
        }

        private void LoadStatusCards()
        {
            try
            {
                if (_controller.StatusCards != null)
                {
                    StatusCardsListBox.ItemsSource = _controller.StatusCards.Values.Where(c => c.IsActive).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading status cards");
            }
        }

        private Point _statusCardDragStartPoint;
        private void StatusCardsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _statusCardDragStartPoint = e.GetPosition(null);
        }

        private void StatusCardsListBox_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _statusCardDragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                ListBox listBox = sender as ListBox;
                ListBoxItem listBoxItem = FindVisualParent<ListBoxItem>((DependencyObject)e.OriginalSource);

                if (listBoxItem != null)
                {
                    StatusCard card = (StatusCard)listBox.ItemContainerGenerator.
                        ItemFromContainer(listBoxItem);

                    DataObject dragData = new DataObject(typeof(StatusCard), card); // Use type as key
                    DragDrop.DoDragDrop(listBoxItem, dragData, DragDropEffects.Copy);
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            if (parentObject is T parent)
                return parent;
            else
                return FindVisualParent<T>(parentObject);
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
                
                UpdateStatus(ResourceManager.GetString("msg_rotation_enabled", "Automatic shift rotation enabled"));
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
                
                UpdateStatus(ResourceManager.GetString("msg_rotation_disabled", "Auto shift rotation disabled"));
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
                    UpdateStatus(string.Format(ResourceManager.GetString("msg_rotation_day_changed", "Rotation day changed to {0}"), selectedItem.Content));
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
                // Initialize day combo box with English day names
                RotationDayComboBox.Items.Clear();
                
                var dayNames = new[] { "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
                var dayKeys = new[] { "day_saturday", "day_sunday", "day_monday", "day_tuesday", "day_wednesday", "day_thursday", "day_friday" };
                var currentDay = _controller.Settings.GetValueOrDefault("auto_rotate_day", "Saturday").ToString() ?? "Saturday";
                
                for (int i = 0; i < dayNames.Length; i++)
                {
                    var dayName = dayNames[i];
                    var dayKey = dayKeys[i];
                    var item = new ComboBoxItem
                    {
                        Content = ResourceManager.GetString(dayKey, dayName),
                        Tag = dayName
                    };
                    RotationDayComboBox.Items.Add(item);
                    
                    if (dayName == currentDay)
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
                    { "Saturday", ResourceManager.GetString("day_saturday", "Saturday") },
                    { "Sunday", ResourceManager.GetString("day_sunday", "Sunday") },
                    { "Monday", ResourceManager.GetString("day_monday", "Monday") },
                    { "Tuesday", ResourceManager.GetString("day_tuesday", "Tuesday") },
                    { "Wednesday", ResourceManager.GetString("day_wednesday", "Wednesday") },
                    { "Thursday", ResourceManager.GetString("day_thursday", "Thursday") },
                    { "Friday", ResourceManager.GetString("day_friday", "Friday") }
                };
                
                var dayName = dayMapping.GetValueOrDefault(rotationDay, rotationDay);
                
                // Update schedule info
                RotationScheduleInfo.Text = string.Format(ResourceManager.GetString("label_shifts_rotate_automatically", "Shifts rotate automatically every week on {0}."), dayName);
                
                // Calculate and display next rotation date using controller method
                var nextRotationDate = _controller.GetNextRotationDate();
                if (nextRotationDate.HasValue)
                {
                    var georgianStr = GeorgianDateHelper.ToGeorgianString(nextRotationDate.Value);
                    var formattedDate = GeorgianDateHelper.FormatForDisplay(georgianStr);
                    NextRotationDate.Text = string.Format(ResourceManager.GetString("label_next_rotation", "Next rotation: {0} ({1})"), formattedDate, dayName);
                }
                else
                {
                    NextRotationDate.Text = ResourceManager.GetString("label_next_rotation_error", "Next rotation date could not be calculated");
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
            if (_isInternalSelectionChange) return;

            try
            {
                // When settings tab is selected, check password
                if (MainTabControl.SelectedItem == SettingsTabItem)
                {
                    var passwordDialog = new PasswordDialog();
                    // Set owner to center it over main window
                    passwordDialog.Owner = this;
                    
                    if (passwordDialog.ShowDialog() == true)
                    {
                        var expectedPassword = AppConfigHelper.Config.AdminPassword;
                        if (passwordDialog.Password != expectedPassword)
                        {
                            MessageBox.Show("Invalid password. Access denied.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                            _isInternalSelectionChange = true;
                            MainTabControl.SelectedIndex = _lastTabIndex;
                            _isInternalSelectionChange = false;
                            return;
                        }
                    }
                    else
                    {
                        // Cancelled
                        _isInternalSelectionChange = true;
                        MainTabControl.SelectedIndex = _lastTabIndex;
                        _isInternalSelectionChange = false;
                        return;
                    }
                }

                _lastTabIndex = MainTabControl.SelectedIndex;

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
                         selectedTab.Header?.ToString() == "Daily Preview")
                {
                    UpdateDailyPreview();
                }
                // When settings tab is selected, load text customization grid
                else if (MainTabControl.SelectedItem == SettingsTabItem)
                {
                    LoadTextCustomizationGrid();
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
                                UpdateStatus($"Employee {employee.FullName} removed from shift {shiftType} and returned to list");
                            }
                            else
                            {
                                UpdateStatus($"Error removing employee {employee.FullName} from shift");
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
                                    UpdateStatus($"{employee.FullName} returned to main list");
                                }
                                else
                                {
                                    UpdateStatus($"Error returning {employee.FullName}");
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
                                    UpdateStatus($"{employee.FullName} returned to main list");
                                }
                                else
                                {
                                    UpdateStatus($"Employee {employee.FullName} not found in any shift and has no absence");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShiftEmployeeListBox_Drop");
                MessageBox.Show($"Error restoring employee to list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    border.Background = Brushes.Transparent;
                    border.BorderBrush = Brushes.Gray;
                    border.BorderThickness = new Thickness(1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SupervisorArea_DragLeave");
            }
        }

        #endregion
        #region Assignment Conflict Resolution

        private MessageBoxResult ShowAssignmentConflictDialog(AssignmentConflict conflict, Employee employee, string targetGroupName, string targetShiftType)
        {
            string message;
            string title = "Confirm Assignment";

            var targetShiftName = targetShiftType == "morning" ? "Morning" : targetShiftType == "evening" ? "Afternoon" : targetShiftType;

            switch (conflict.Type)
            {
                case ConflictType.Absent:
                    message = $"Employee {employee.FullName} is marked as {conflict.AbsenceType}.\n\nDo you want to remove the absence and assign them to group {targetGroupName} (shift {targetShiftName})?";
                    break;

                case ConflictType.DifferentShift:
                    var currentShiftName = conflict.CurrentShiftType == "morning" ? "Morning" : conflict.CurrentShiftType == "evening" ? "Afternoon" : conflict.CurrentShiftType;
                    message = $"Employee {employee.FullName} is already assigned to the {currentShiftName} shift in this group.\n\nDo you want to remove them from the previous shift and assign them to the {targetShiftName} shift?";
                    break;

                case ConflictType.DifferentGroup:
                    message = $"Employee {employee.FullName} is already assigned to group {conflict.CurrentGroupName}.\n\nDo you want to remove them from the previous group and assign to group {targetGroupName} (shift {targetShiftName})?";
                    break;

                default:
                    message = $"Do you want to assign employee {employee.FullName} to group {targetGroupName} (shift {targetShiftName})?";
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
                    UpdateStatus($"Employee {employee.FullName} assigned to {shiftType} shift");
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
                    targetGroupName = "Default";
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
                                UpdateStatus($"Employee {employee.FullName} assigned to {shiftType} shift");
                            }
                        }
                        else
                        {
                            UpdateStatus($"Error assigning employee {employee.FullName} to shift");
                        }
                    }
                    else
                    {
                        UpdateStatus($"Error removing previous assignment for {employee.FullName}");
                    }
                }
                // If user clicked No, do nothing
            }
            else if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                // Error occurred
                UpdateStatus($"Error: {result.ErrorMessage}");
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

                // Load Absent employees
                var absentEmployees = GetEmployeesByAbsenceCategory("Absent", todayGeorgian);
                AbsentEmployeesListBox.ItemsSource = absentEmployees;
                AbsentEmployeesExpander.Header = string.Format(ResourceManager.GetString("expander_absent_employees", "Absent Employees ({0})"), absentEmployees.Count);

                // Load Sick employees
                var sickEmployees = GetEmployeesByAbsenceCategory("Sick", todayGeorgian);
                SickEmployeesListBox.ItemsSource = sickEmployees;
                SickEmployeesExpander.Header = string.Format(ResourceManager.GetString("expander_sick_employees", "Sick Employees ({0})"), sickEmployees.Count);

                // Load Leave employees
                var leaveEmployees = GetEmployeesByAbsenceCategory("Leave", todayGeorgian);
                LeaveEmployeesListBox.ItemsSource = leaveEmployees;
                LeaveEmployeesExpander.Header = string.Format(ResourceManager.GetString("expander_leave_employees", "Leave Employees ({0})"), leaveEmployees.Count);

                // Update Employee Management section lists
                EmployeeManagementAbsentListBox.ItemsSource = absentEmployees;
                EmployeeManagementAbsentExpander.Header = string.Format(ResourceManager.GetString("expander_absent_employees", "Absent Employees ({0})"), absentEmployees.Count);

                EmployeeManagementSickListBox.ItemsSource = sickEmployees;
                EmployeeManagementSickExpander.Header = string.Format(ResourceManager.GetString("expander_sick_employees", "Sick Employees ({0})"), sickEmployees.Count);

                EmployeeManagementLeaveListBox.ItemsSource = leaveEmployees;
                EmployeeManagementLeaveExpander.Header = string.Format(ResourceManager.GetString("expander_leave_employees", "Leave Employees ({0})"), leaveEmployees.Count);

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
                            $"Do you want to restore {clickedEmployee.FullName} to the main list?",
                            "Restore to main list",
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
                                    
                                    UpdateStatus($"{clickedEmployee.FullName} returned to main list");
                                }
                                else
                                {
                                    UpdateStatus($"Error restoring {clickedEmployee.FullName}");
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
                                    UpdateStatus($"{clickedEmployee.FullName} returned to main list");
                                }
                                else
                                {
                                    UpdateStatus($"Today's absence for {clickedEmployee.FullName} not found");
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
                MessageBox.Show($"Error returning employee: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            HandleAbsenceListDrop(e, "Absent", "Absent");
        }

        private void SickEmployeesListBox_Drop(object sender, DragEventArgs e)
        {
            HandleAbsenceListDrop(e, "Sick", "Sick");
        }

        private void LeaveEmployeesListBox_Drop(object sender, DragEventArgs e)
        {
            HandleAbsenceListDrop(e, "Leave", "Leave");
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
                            UpdateStatus($"{employee.FullName} is already registered as {todayAbsence.Category}");
                            return;
                        }

                        var success = _controller.MarkEmployeeAbsent(employee, category);
                        if (success)
                        {
                            LoadAbsenceLists();
                            LoadEmployees();
                            LoadShiftSlots();
                            UpdateShiftStatistics();
                            UpdateStatus($"{employee.FullName} registered as {categoryDisplay}");
                        }
                        else
                        {
                            UpdateStatus($"Error registering {employee.FullName} as {categoryDisplay}");
                        }
                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HandleAbsenceListDrop for category {Category}", category);
                MessageBox.Show($"Error recording absence: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            UpdateStatus(string.Format(ResourceManager.GetString("msg_employee_assigned", "Employee {0} assigned to {1} shift"), employee.FullName, shiftType));
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
                            // Try to detect employee name from folder (format: FirstName_LastName_PersonnelId.ext)
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
                                        UpdateStatus(string.Format(ResourceManager.GetString("msg_employee_photo_updated_assigned", "Employee {0} photo updated and assigned to shift"), employee.FullName));
                                    });
                                }
                                else
                                {
                                    // Create new employee automatically from folder name
                                    var dialogResult = MessageBox.Show(
                                        string.Format(ResourceManager.GetString("msg_employee_not_found_create", "Employee {0} {1} not found.\nDo you want to create a new employee with this name?"), detectedFirstName, detectedLastName),
                                        ResourceManager.GetString("header_create_new_employee", "Create new employee"),
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
                                                    UpdateStatus(string.Format(ResourceManager.GetString("msg_new_employee_created_assigned", "New employee {0} {1} created and assigned to shift"), detectedFirstName, detectedLastName));
                                                });
                                            }
                                        }
                                    }
                                    else
                                    {
                                        UpdateStatus($"Employee {detectedFirstName} {detectedLastName} not found.");
                                    }
                                }
                            }
                            else
                            {
                                UpdateStatus("Employee name could not be detected from file name. Please rename the file to FirstName_LastName_PersonnelId.ext");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in slot drop");
                    MessageBox.Show($"Error assigning to shift: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Please select an employee", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                                        $"Personnel ID detected from file name: {detectedPersonnelId}\nDo you want to update the employee's personnel ID?",
                                        "Personnel ID detected",
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
                                        $"Name detected from folder: {detectedFirstName} {detectedLastName}\nDo you want to update the employee's name?",
                                        "Name detected from folder",
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
                            UpdateStatus("Employee photo updated");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in employee photo drop");
                MessageBox.Show($"Error adding photo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        $"Employee {employee.FullName} is already in this slot.\n\nDo you want to replace them with another employee?",
                        "Replace employee",
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
                MessageBox.Show($"Error assigning to shift: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        $"Do you want to remove employee {employee.FullName} from the {shiftType} shift?",
                        "Remove employee from shift",
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
                            UpdateStatus($"Employee {employee.FullName} removed from {shiftType} shift");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slot right click");
                MessageBox.Show($"Error deleting employee: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("No employees available for assignment", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create a simple selection dialog
                var dialog = new Window
                {
                    Title = $"Assign employee to {shiftType}",
                    Width = 300,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    FlowDirection = FlowDirection.RightToLeft
                };

                var stackPanel = new StackPanel { Margin = new Thickness(10) };
                
                var label = new Label
                {
                    Content = "Select the employee:",
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
                    Content = "Confirm",
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
                            UpdateStatus($"Employee {selectedEmployee.FullName} assigned to {shiftType} shift");
                        });
                    }
                    dialog.Close();
                };

                var cancelButton = new Button
                {
                    Content = "Cancel",
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
                MessageBox.Show($"Error showing selection dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        UpdateStatus($"Task {dialog.Title} added");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding task");
                MessageBox.Show($"Error adding task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        UpdateStatus($"Task {dialog.Title} updated");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing task");
                MessageBox.Show($"Error editing task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                var result = MessageBox.Show($"Are you sure you want to delete task {taskTitle}?", 
                    "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    var success = _controller.DeleteTask(taskId);
                    if (success)
                    {
                        LoadTasks();
                        UpdateStatus($"Task {taskTitle} deleted");
                        _selectedTask = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task");
                MessageBox.Show($"Error deleting task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveTaskChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var priority = ((ComboBoxItem)TaskPriorityComboBox.SelectedItem)?.Content?.ToString() ?? "Medium";
                var status = ((ComboBoxItem)TaskStatusComboBox.SelectedItem)?.Content?.ToString() ?? "Pending";
                
                double.TryParse(TaskEstimatedHoursTextBox.Text, out double estimatedHours);
                double.TryParse(TaskActualHoursTextBox.Text, out double actualHours);
                
                var targetDate = TaskTargetDatePicker.SelectedDate; // Already in Shamsi format

                var success = _controller.UpdateTask(_selectedTask.TaskId, 
                    TaskTitleTextBox.Text, TaskDescriptionTextBox.Text, priority, estimatedHours, targetDate, status, actualHours, TaskNotesTextBox.Text);
                
                if (success)
                {
                    LoadTasks();
                    UpdateStatus("Task changes saved");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving task changes");
                MessageBox.Show($"Error saving changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                _logger.LogInformation("StartTask_Click: Selected task: {TaskId} - {TaskTitle}", _selectedTask.TaskId, _selectedTask.Title);

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                _logger.LogInformation("StartTask_Click: Calling UpdateTask with status 'In Progress'");
                var success = _controller.UpdateTask(taskId, status: "In Progress");
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
                    UpdateStatus($"Task {taskTitle} started");
                    _logger.LogInformation("StartTask_Click: Status updated and method completed successfully");
                    
                    // Debug: Show message to confirm the operation
                    MessageBox.Show($"Task {taskTitle} started successfully!\nStatus: In Progress", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogError("StartTask_Click: UpdateTask failed for task: {TaskId}", taskId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting task");
                MessageBox.Show($"Error starting task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CompleteTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                double.TryParse(TaskActualHoursTextBox.Text, out double actualHours);
                var success = _controller.UpdateTask(taskId, status: "Completed", actualHours: actualHours, notes: TaskNotesTextBox.Text);
                if (success)
                {
                    LoadTasks();
                    // Reload task details to show updated status
                    var updatedTask = _controller.GetTask(taskId);
                    if (updatedTask != null)
                    {
                        LoadTaskDetails(updatedTask);
                    }
                    UpdateStatus($"Task {taskTitle} completed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing task");
                MessageBox.Show($"Error completing task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void AddEmployeeToTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ShowEmployeeAssignmentDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing employee assignment dialog");
                MessageBox.Show($"Error showing assignment dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveEmployeeFromTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        var result = MessageBox.Show($"Do you want to remove {employee.FullName} from this task?", 
                            "Confirm remove", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            var success = _controller.RemoveTaskFromEmployee(taskId, employeeId);
                            if (success)
                            {
                                LoadTaskAssignments();
                                UpdateStatus($"{employee.FullName} removed from task {taskTitle}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing employee from task");
                MessageBox.Show($"Error removing employee from task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewEmployeeTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ShowEmployeeTasksDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing employee tasks dialog");
                MessageBox.Show($"Error showing employee tasks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowEmployeeAssignmentDialog()
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("Please select a task", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Capture task information to avoid null reference issues
                var taskId = _selectedTask.TaskId;
                var taskTitle = _selectedTask.Title;

                var availableGroups = _controller.GetActiveShiftGroups();
                
                if (availableGroups.Count == 0)
                {
                    MessageBox.Show("No active shift groups found", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new Window
                {
                    Title = $"Assign shift group to task: {taskTitle}",
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
                    Content = "Select the shift group:",
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
                    Content = "Assign",
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
                            MessageBox.Show($"Group {selectedGroup.Name} has no employees", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var success = _controller.AssignTaskToShiftGroup(taskId, selectedGroup.GroupId);
                        if (success)
                        {
                            LoadTaskAssignments();
                            UpdateStatus($"All employees in group {selectedGroup.Name} ({employees.Count}) assigned to task {taskTitle}");
                            dialog.Close();
                        }
                        else
                        {
                            MessageBox.Show($"Error assigning group {selectedGroup.Name} employees to task", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please select a shift group", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };

                var cancelButton = new Button
                {
                    Content = "Cancel",
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
                MessageBox.Show($"Error in assignment dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowEmployeeTasksDialog()
        {
            try
            {
                var dialog = new Window
                {
                    Title = "Employee tasks",
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
                    Content = "Tasks assigned to employees:",
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
                    Header = "Employee",
                    Binding = new Binding("EmployeeName"),
                    Width = 150
                });

                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Task",
                    Binding = new Binding("Title"),
                    Width = 200
                });

                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Status",
                    Binding = new Binding("Status"),
                    Width = 100
                });

                dataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Priority",
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
                    Content = "Close",
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
                MessageBox.Show($"Error showing employee tasks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Please select start and end dates", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var report = GenerateReport(reportType, startDateGeorgian, endDateGeorgian);
                ReportPreviewTextBlock.Text = report;
                UpdateStatus("Report generated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                MessageBox.Show($"Error generating report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateReport(string? reportType, string startDateGeorgian, string endDateGeorgian)
        {
            try
            {
                _logger.LogInformation("Generating report: Type={ReportType}, StartDate={StartDate}, EndDate={EndDate}", 
                    reportType, startDateGeorgian, endDateGeorgian);
                
                var report = $"Report {reportType}\n";
                report += $"Start date: {GeorgianDateHelper.FormatForDisplay(startDateGeorgian)}\n";
                report += $"End date: {GeorgianDateHelper.FormatForDisplay(endDateGeorgian)}\n\n";

                // Load historical data for the date range
                var historicalData = LoadHistoricalData(startDateGeorgian, endDateGeorgian);
                
                _logger.LogInformation("Historical data loaded: {Count} days", historicalData.Count);
                
                if (historicalData.Count == 0)
                {
                    report += "No data found for this period.\n";
                    report += $"Requested dates: {startDateGeorgian} to {endDateGeorgian}\n";
                    return report;
                }

                // Employee statistics (from the most recent day)
                var latestData = historicalData.OrderByDescending(kvp => kvp.Key).First().Value;
                var totalEmployees = GetEmployeeCount(latestData);
                var totalAbsences = GetTotalAbsences(historicalData);
                
                report += "Employee statistics:\n";
                report += $"Total employees: {totalEmployees}\n";
                report += $"Total absences in period: {totalAbsences}\n\n";

                // Shift statistics (average across the period)
                var shiftStats = GetShiftStatistics(historicalData);
                
                report += "Shift statistics (average):\n";
                report += $"Morning shift: {shiftStats.AverageMorning:F1}/{shiftStats.Capacity}\n";
                report += $"Afternoon shift: {shiftStats.AverageEvening:F1}/{shiftStats.Capacity}\n";
                report += $"Max morning shift: {shiftStats.MaxMorning}/{shiftStats.Capacity}\n";
                report += $"Max afternoon shift: {shiftStats.MaxEvening}/{shiftStats.Capacity}\n\n";

                // Task statistics (total across the period)
                var taskStats = GetTaskStatistics(historicalData);
                
                report += "Task statistics (full period):\n";
                report += $"Total tasks: {taskStats.TotalTasks}\n";
                report += $"Completed: {taskStats.CompletedTasks}\n";
                report += $"In progress: {taskStats.InProgressTasks}\n";
                report += $"Pending: {taskStats.PendingTasks}\n\n";

                // Daily breakdown
                report += "Daily details:\n";
                foreach (var dayData in historicalData.OrderBy(kvp => kvp.Key))
                {
                    var date = dayData.Key;
                    var data = dayData.Value;
                    var morningCount = GetShiftCount(data, "morning");
                    var eveningCount = GetShiftCount(data, "evening");
                    var absenceCount = GetAbsenceCount(data);
                    var taskCount = GetTaskCount(data);
                    
                    report += $"{GeorgianDateHelper.FormatForDisplay(date)}: Morning({morningCount}) Afternoon({eveningCount}) Absence({absenceCount}) Task({taskCount})\n";
                }

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report content");
                return $"Error generating report: {ex.Message}";
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
                UpdateStatus("Shift groups updated");
                
                _logger.LogInformation("Shift groups updated - UI refreshed");
            });
        }

        private void UpdateDailyPreview()
        {
            try
            {
                var today = Shared.Utils.GeorgianDateHelper.GetCurrentGeorgianDate();
                
                // Count sick employees for today
                var sickAbsences = _controller.AbsenceManager.GetAbsencesByCategory("Sick")
                    .Where(a => a.Date == today)
                    .ToList();
                SickCountText.Text = sickAbsences.Count.ToString();
                
                // Count employees on leave for today
                var leaveAbsences = _controller.AbsenceManager.GetAbsencesByCategory("Leave")
                    .Where(a => a.Date == today)
                    .ToList();
                LeaveCountText.Text = leaveAbsences.Count.ToString();
                
                // Count absent employees for today
                var absentAbsences = _controller.AbsenceManager.GetAbsencesByCategory("Absent")
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
                    selectedTab.Header?.ToString() == "Daily Preview")
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
                    headerCell.Blocks.Add(new Paragraph(new Run($"Daily summary - {todayDisplay}"))
                    {
                        FontSize = 22,
                        FontWeight = FontWeights.Bold,
                        Foreground = headerTextColor
                    });
                    headerCell.Blocks.Add(new Paragraph(new Run($"Generated: {generationTime}"))
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
                    var sickCount = _controller.AbsenceManager.GetAbsencesByCategory("Sick")
                        .Count(a => a.Date == today);
                    var leaveCount = _controller.AbsenceManager.GetAbsencesByCategory("Leave")
                        .Count(a => a.Date == today);
                    var absentCount = _controller.AbsenceManager.GetAbsencesByCategory("Absent")
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
                    absenceHeaderCell.Blocks.Add(new Paragraph(new Run("Absence statistics"))
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
                        new { Label = "Sick Employees", Count = sickCount },
                        new { Label = "Leave Employees", Count = leaveCount },
                        new { Label = "Absent Employees", Count = absentCount }
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
                    groupsHeaderCell.Blocks.Add(new Paragraph(new Run("Shift Group Statistics"))
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
                        countCell.Blocks.Add(new Paragraph(new Run($"{count} employees"))
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
                        var noGroupParagraph = new Paragraph(new Run("No active group"))
                        {
                            FontSize = 12,
                            FontStyle = FontStyles.Italic,
                            Foreground = darkTextColor
                        };
                        emptyCell.Blocks.Add(noGroupParagraph);
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
                    footerCell.Blocks.Add(new Paragraph(new Run("Report generated by Employee Management System"))
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
                    printDialog.PrintDocument(paginator, $"Daily summary - {todayDisplay}");
                    
                    UpdateStatus("Daily summary printed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing daily preview");
                MessageBox.Show($"Error printing: {ex.Message}", "Error", 
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
                    MessageBox.Show("Please generate the report first", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    var reportTypeText = "Report";
                    if (ReportTypeComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedReportType)
                    {
                        reportTypeText = selectedReportType.Content?.ToString() ?? "Report";
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
                        headerCell.Blocks.Add(new Paragraph(new Run($"From {reportData.StartDate} to {reportData.EndDate}"))
                        {
                            FontSize = 14,
                            Foreground = darkTextColor,
                            Margin = new Thickness(0, 8, 0, 0)
                        });
                    }
                    
                    headerCell.Blocks.Add(new Paragraph(new Run($"Generated: {generationTime}"))
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
                    employeeHeaderCell.Blocks.Add(new Paragraph(new Run("Employee statistics"))
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
                        new { Label = "Total employees", Value = reportData.TotalEmployees.ToString() },
                        new { Label = "Total absences in period", Value = reportData.TotalAbsences.ToString() }
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
                    shiftHeaderCell.Blocks.Add(new Paragraph(new Run("Shift Statistics (average)"))
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
                        new { Label = "Morning shift (avg)", Value = $"{reportData.AverageMorningShift:F1}/{capacity}" },
                        new { Label = "Afternoon shift (avg)", Value = $"{reportData.AverageAfternoonShift:F1}/{capacity}" },
                        new { Label = "Night shift (avg)", Value = $"{reportData.AverageNightShift:F1}/{capacity}" },
                        new { Label = "Max morning shift", Value = $"{reportData.MaxMorningShift}/{capacity}" },
                        new { Label = "Max afternoon shift", Value = $"{reportData.MaxAfternoonShift}/{capacity}" },
                        new { Label = "Max night shift", Value = $"{reportData.MaxNightShift}/{capacity}" }
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
                    taskHeaderCell.Blocks.Add(new Paragraph(new Run("Task statistics (full period)"))
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
                        new { Label = "Total tasks", Value = reportData.TotalTasks.ToString() },
                        new { Label = "Completed", Value = reportData.CompletedTasks.ToString() },
                        new { Label = "In progress", Value = reportData.InProgressTasks.ToString() },
                        new { Label = "Pending", Value = reportData.PendingTasks.ToString() }
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
                        dailyHeaderCell.Blocks.Add(new Paragraph(new Run("Daily details"))
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
                    footerCell.Blocks.Add(new Paragraph(new Run("Report generated by Employee Management System"))
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
                    
                    UpdateStatus("Report printed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing report");
                MessageBox.Show($"Error printing: {ex.Message}", "Error", 
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
                if (cleanLine.StartsWith("Report") && !cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Report\s+(.+)");
                    if (match.Success)
                        reportData.ReportType = match.Groups[1].Value.Trim();
                }
                // Parse dates
                else if (cleanLine.Contains("Start date:"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Start date:\s*(.+)");
                    if (match.Success)
                        reportData.StartDate = match.Groups[1].Value.Trim();
                }
                else if (cleanLine.Contains("End date:"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"End date:\s*(.+)");
                    if (match.Success)
                        reportData.EndDate = match.Groups[1].Value.Trim();
                }
                // Parse employee statistics
                else if (cleanLine.Contains("Total employees") && !cleanLine.Contains("managers"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Total\s+employees[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.TotalEmployees = count;
                }
                else if (cleanLine.Contains("Total absences") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Total\s+absences[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.TotalAbsences = count;
                }
                // Parse shift statistics
                else if (cleanLine.Contains("Morning shift") && cleanLine.Contains("/") && !cleanLine.Contains("Max"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Morning\s+shift[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                            reportData.AverageMorningShift = avg;
                        if (int.TryParse(match.Groups[2].Value, out int cap) && reportData.ShiftCapacity == 0)
                            reportData.ShiftCapacity = cap;
                    }
                }
                else if (cleanLine.Contains("Afternoon shift") && cleanLine.Contains("/") && !cleanLine.Contains("Max"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Afternoon\s+shift[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                        reportData.AverageAfternoonShift = avg;
                }
                else if (cleanLine.Contains("Night shift") && cleanLine.Contains("/") && !cleanLine.Contains("Max"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Night\s+shift[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                        reportData.AverageNightShift = avg;
                }
                else if (cleanLine.Contains("Max") && cleanLine.Contains("Morning shift"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Max\s+morning\s+shift[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                        reportData.MaxMorningShift = max;
                }
                else if (cleanLine.Contains("Max") && cleanLine.Contains("Afternoon shift"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Max\s+afternoon\s+shift[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                        reportData.MaxAfternoonShift = max;
                }
                else if (cleanLine.Contains("Max") && cleanLine.Contains("Night shift"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Max\s+night\s+shift[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                        reportData.MaxNightShift = max;
                }
                // Parse task statistics
                else if (cleanLine.Contains("Total tasks") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Total\s+tasks[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.TotalTasks = count;
                }
                else if (cleanLine.Contains("Completed") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Completed[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.CompletedTasks = count;
                }
                else if (cleanLine.Contains("In progress") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"In\s+progress[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.InProgressTasks = count;
                }
                else if (cleanLine.Contains("Pending") && cleanLine.Contains(":"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanLine, @"Pending[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                        reportData.PendingTasks = count;
                }
                // Check for daily details section
                else if (cleanLine.Contains("Daily details:") || cleanLine == "Daily details")
                {
                    inDailyDetails = true;
                }
                // Parse daily details (format: Date: Morning(n) Afternoon(n) Absence(n) Task(n))
                else if (inDailyDetails || (cleanLine.Contains("Morning") && cleanLine.Contains("Afternoon") && 
                          cleanLine.Contains("Absence") && cleanLine.Contains("Task")))
                {
                    if (!string.IsNullOrWhiteSpace(cleanLine))
                        reportData.DailyDetails.Add(cleanLine);
                }
            }

            return reportData;
        }

        private void ResourceBridge_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Shared.Utils.ResourceBridge.CurrentLanguage))
                return;
            Dispatcher.Invoke(() =>
            {
                var lang = Shared.Utils.ResourceBridge.Instance.CurrentLanguage;
                LanguageComboBox.SelectedItem = lang == Shared.Utils.LanguageConfigHelper.LanguageFa ? LanguagePersianItem : LanguageEnglishItem;
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
                UpdateStatus("Data synced");
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
                    Title = "Select shared data folder",
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
                MessageBox.Show($"Error selecting folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newDataDirectory = DataDirectoryTextBox.Text.Trim();
                
                if (string.IsNullOrEmpty(newDataDirectory))
                {
                    MessageBox.Show("Please select the data folder path.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!Directory.Exists(newDataDirectory))
                {
                    var result = MessageBox.Show(
                        "The selected folder does not exist. Do you want to create it?",
                        "Confirm",
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
                    saveButton.Content = "Processing...";
                }
                
                // Update configuration asynchronously
                var success = await System.Threading.Tasks.Task.Run(() => AppConfigHelper.UpdateDataDirectory(newDataDirectory, copyExistingData));
                
                // Hide progress bar
                ProgressPanel.Visibility = Visibility.Collapsed;
                
                if (success)
                {
                    // Save display visibility settings to display config
                    var displayConfigPath = GetDisplayConfigPath();
                    var displayConfigHelper = new DisplayApp.Utils.ConfigHelper(displayConfigPath);
                    var showChart = ShowPerformanceChartCheckBox.IsChecked ?? true;
                    var showAi = ShowAiRecommendationCheckBox.IsChecked ?? true;
                    var selectedProfile = (DisplayProfileComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Standard";
                    displayConfigHelper.SetShowPerformanceChart(showChart);
                    displayConfigHelper.SetShowAiRecommendation(showAi);
                    displayConfigHelper.SetDisplayProfile(selectedProfile);
                    displayConfigHelper.SaveConfig();

                    // Save Admin Password to AppConfig
                    var config = AppConfigHelper.Config;
                    config.AdminPassword = AdminPasswordTextBox.Text;
                    AppConfigHelper.SaveConfig(config);

                    var message = copyExistingData 
                        ? "Settings saved successfully and existing data was moved." 
                        : "Settings saved successfully.";
                    
                    MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Update UI
                    CurrentDataDirectoryTextBlock.Text = newDataDirectory;
                    UpdateSettingsDisplay();
                    
                    // The controller will automatically update through the configuration change event
                    _logger.LogInformation("Data directory changed to: {NewPath}, Copy existing data: {CopyData}. Display chart: {ShowChart}, AI: {ShowAi}", 
                        newDataDirectory, copyExistingData, showChart, showAi);
                }
                else
                {
                    MessageBox.Show("Error saving settings. Please select a valid path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    saveButton.Content = "Save settings";
                }
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is not System.Windows.Controls.ComboBoxItem item ||
                item.Tag is not string tag)
                return;
            var sharedData = App.SharedDataDirectory;
            if (string.IsNullOrEmpty(sharedData))
                return;
            var lang = tag.Trim().ToLowerInvariant() == "fa" ? Shared.Utils.LanguageConfigHelper.LanguageFa : Shared.Utils.LanguageConfigHelper.LanguageEn;
            if (lang == Shared.Utils.ResourceBridge.Instance.CurrentLanguage)
                return;
            try
            {
                Shared.Utils.LanguageConfigHelper.SetCurrentLanguage(sharedData, lang);
                Shared.Utils.ResourceManager.LoadResourcesForLanguage(sharedData, lang);
                Shared.Utils.ResourceBridge.Instance.CurrentLanguage = lang;
                Shared.Utils.ResourceBridge.Instance.NotifyLanguageChanged();
                App.ApplyFlowDirection();
                UpdateStatus(Shared.Utils.ResourceManager.GetString("msg_data_loaded", "Data loaded"));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error switching language");
                MessageBox.Show(Shared.Utils.ResourceManager.GetString("msg_error", "Error") + ": " + ex.Message,
                    Shared.Utils.ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to reset settings to default?",
                    "Confirm",
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
                    AdminPasswordTextBox.Text = "admin123";
                    
                    // Reset display background color to default
                    var displayConfigPath = GetDisplayConfigPath();
                    if (File.Exists(displayConfigPath))
                    {
                        var displayConfigHelper = new DisplayApp.Utils.ConfigHelper(displayConfigPath);
                        displayConfigHelper.SetBackgroundColor("#1a1a1a");
                        displayConfigHelper.SetShowPerformanceChart(true);
                        displayConfigHelper.SetShowAiRecommendation(true);
                        displayConfigHelper.SetDisplayProfile("Standard");
                        displayConfigHelper.SaveConfig();
                        UpdateDisplayColorPreview("#1a1a1a");
                        ShowPerformanceChartCheckBox.IsChecked = true;
                        ShowAiRecommendationCheckBox.IsChecked = true;
                        foreach (ComboBoxItem item in DisplayProfileComboBox.Items)
                        {
                            if (item.Tag?.ToString() == "Standard")
                            {
                                DisplayProfileComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    
                    MessageBox.Show("Settings have been reset to default.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting settings");
                MessageBox.Show($"Error resetting settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Text Customization

        private List<ResourceOverrideItem> _allResourceItems = new List<ResourceOverrideItem>();

        /// <summary>
        /// Loads all resource keys into the Text Customization DataGrid.
        /// </summary>
        private void LoadTextCustomizationGrid()
        {
            try
            {
                var baseResources = ResourceManager.GetAllResources();
                var overrides = CustomOverrideManager.GetAllOverrides();

                _allResourceItems = baseResources
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => new ResourceOverrideItem
                    {
                        Key = kvp.Key,
                        DefaultValue = kvp.Value,
                        CustomValue = overrides.TryGetValue(kvp.Key, out var customVal) ? customVal : string.Empty
                    })
                    .ToList();

                FilterTextCustomizationGrid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading text customization grid");
            }
        }

        private void FilterTextCustomizationGrid()
        {
            var searchText = TextCustomizationSearchBox?.Text?.Trim().ToLowerInvariant() ?? string.Empty;
            
            var filtered = string.IsNullOrEmpty(searchText)
                ? _allResourceItems
                : _allResourceItems.Where(item => 
                    item.Key.ToLowerInvariant().Contains(searchText) ||
                    item.DefaultValue.ToLowerInvariant().Contains(searchText) ||
                    item.CustomValue.ToLowerInvariant().Contains(searchText)).ToList();

            TextCustomizationDataGrid.ItemsSource = filtered;
        }

        private void TextCustomizationSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTextCustomizationGrid();
        }

        private void ApplyOverride_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = TextCustomizationDataGrid.SelectedItem as ResourceOverrideItem;
                
                if (selectedItem == null && TextCustomizationDataGrid.SelectedCells.Count > 0)
                {
                    selectedItem = TextCustomizationDataGrid.SelectedCells[0].Item as ResourceOverrideItem;
                }

                if (selectedItem == null && TextCustomizationDataGrid.CurrentCell.IsValid)
                {
                    selectedItem = TextCustomizationDataGrid.CurrentCell.Item as ResourceOverrideItem;
                }

                if (selectedItem == null)
                {
                    MessageBox.Show("Please select a resource key to override.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (string.IsNullOrWhiteSpace(selectedItem.CustomValue))
                {
                    MessageBox.Show("Please enter a custom value for the selected key.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                CustomOverrideManager.SetOverride(selectedItem.Key, selectedItem.CustomValue);
                SaveOverridesAndRefreshUI();

                MessageBox.Show(
                    ResourceManager.GetString("msg_override_applied", "Custom text applied. Changes take effect immediately."),
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying override");
                MessageBox.Show($"Error applying override: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearOverride_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = TextCustomizationDataGrid.SelectedItem as ResourceOverrideItem;
                
                if (selectedItem == null && TextCustomizationDataGrid.SelectedCells.Count > 0)
                {
                    selectedItem = TextCustomizationDataGrid.SelectedCells[0].Item as ResourceOverrideItem;
                }

                if (selectedItem == null && TextCustomizationDataGrid.CurrentCell.IsValid)
                {
                    selectedItem = TextCustomizationDataGrid.CurrentCell.Item as ResourceOverrideItem;
                }

                if (selectedItem == null)
                {
                    MessageBox.Show("Please select a resource key to clear.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                CustomOverrideManager.RemoveOverride(selectedItem.Key);
                selectedItem.CustomValue = string.Empty;
                SaveOverridesAndRefreshUI();
                LoadTextCustomizationGrid();

                UpdateStatus($"Override for '{selectedItem.Key}' cleared.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing override");
                MessageBox.Show($"Error clearing override: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetAllOverrides_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to reset all text customizations to defaults?",
                    "Confirm Reset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;

                CustomOverrideManager.ClearAllOverrides();
                SaveOverridesAndRefreshUI();
                LoadTextCustomizationGrid();

                MessageBox.Show(
                    ResourceManager.GetString("msg_overrides_reset", "All customizations have been reset to defaults."),
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting all overrides");
                MessageBox.Show($"Error resetting overrides: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplySupervisorPreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "This will change all 'Supervisor' references to 'Foreman'. Continue?",
                    "Apply Preset",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                CustomOverrideManager.ApplySupervisorToForemanPreset();
                SaveOverridesAndRefreshUI();
                LoadTextCustomizationGrid();

                MessageBox.Show(
                    ResourceManager.GetString("msg_preset_applied", "Preset applied successfully. All supervisor references changed to foreman."),
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying supervisor preset");
                MessageBox.Show($"Error applying preset: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveOverridesAndRefreshUI()
        {
            try
            {
                var overridesPath = Path.Combine(App.SharedDataDirectory, "custom_overrides.xml");
                CustomOverrideManager.SaveOverrides(overridesPath);
                
                // Notify UI to refresh with new values
                ResourceBridge.Instance.NotifyLanguageChanged();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving overrides");
            }
        }

        #endregion

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
                    
                    MessageBox.Show("Display background color changed successfully. Restart the display app to apply changes.", 
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting display color");
                MessageBox.Show($"Error selecting color: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetDisplayConfigPath()
        {
            // Use the shared helper so ManagementApp and DisplayApp always point
            // to the same display configuration file under SharedData.
            return Shared.Utils.DisplayConfigPathHelper.GetDisplayConfigPath();
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
                // Sync language combo with current language
                var lang = Shared.Utils.ResourceBridge.Instance.CurrentLanguage;
                LanguageComboBox.SelectedItem = lang == Shared.Utils.LanguageConfigHelper.LanguageFa ? LanguagePersianItem : LanguageEnglishItem;

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

                    // Load display visibility settings
                    ShowPerformanceChartCheckBox.IsChecked = displayConfigHelper.GetShowPerformanceChart();
                    ShowAiRecommendationCheckBox.IsChecked = displayConfigHelper.GetShowAiRecommendation();

                    var profile = displayConfigHelper.GetDisplayProfile();
                    foreach (ComboBoxItem item in DisplayProfileComboBox.Items)
                    {
                        if (item.Tag?.ToString() == profile)
                        {
                            DisplayProfileComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }
                else
                {
                    UpdateDisplayColorPreview("#1a1a1a");
                    ShowPerformanceChartCheckBox.IsChecked = true;
                    ShowAiRecommendationCheckBox.IsChecked = true;
                    DisplayProfileComboBox.SelectedIndex = 0;
                }

                AdminPasswordTextBox.Text = config.AdminPassword;
                
                // Update sync status
                SyncStatusTextBlock.Text = config.SyncEnabled ? "On" : "Off";
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
                    ReportFilesListBox.ItemsSource = new List<string> { "Reports folder not found" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating report files list");
                ReportFilesListBox.ItemsSource = new List<string> { "Error loading files" };
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
                            logContent.AppendLine("Error reading log file");
                        }
                        logContent.AppendLine();
                    }
                    
                    SystemLogsTextBlock.Text = logContent.ToString();
                }
                else
                {
                    SystemLogsTextBlock.Text = "Logs folder not found";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system logs");
                SystemLogsTextBlock.Text = $"Error loading logs: {ex.Message}";
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
                statusText += $" - {status.Difference} boxes ahead";
                DailyProgressStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            else if (status.IsBehind)
            {
                statusText += $" - {Math.Abs(status.Difference)} boxes behind";
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
                    MessageBox.Show("Please select a shift group", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("Please enter a non-negative integer for completed boxes", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var success = _controller.RecordDailyProgress(groupId, shiftType, shamsiDate, completedBoxes);
                if (success)
                {
                    LoadDailyProgress();
                    LoadWeeklyProgress();
                    UpdateStatus($"Daily progress recorded: {completedBoxes} boxes");
                }
                else
                {
                    MessageBox.Show("Error recording daily progress", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording daily progress");
                MessageBox.Show($"Error recording progress: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                WeeklyProgressWeekStartTextBlock.Text = Shared.Utils.GeorgianDateHelper.FormatShamsiAsGeorgianForDisplay(weekStartDate);
                WeeklyProgressTotalCompletedTextBlock.Text = weeklyStatus.TotalCompleted.ToString();
                WeeklyProgressPercentageTextBlock.Text = weeklyStatus.Percentage.ToString("F1");
                WeeklyProgressDifferenceTextBlock.Text = weeklyStatus.Difference >= 0 ? $"+{weeklyStatus.Difference}" : weeklyStatus.Difference.ToString();
                
                string statusText = $"{weeklyStatus.StatusText} ({weeklyStatus.Percentage:F1}%)";
                if (weeklyStatus.IsAhead)
                {
                    statusText += $" - {weeklyStatus.Difference} boxes ahead";
                    WeeklyProgressStatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                else if (weeklyStatus.IsBehind)
                {
                    statusText += $" - {Math.Abs(weeklyStatus.Difference)} boxes behind";
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
                    Date = Shared.Utils.GeorgianDateHelper.FormatShamsiAsGeorgianForDisplay(p.Date),
                    CompletedBoxes = p.CompletedBoxes,
                    DailyTarget = p.DailyTarget,
                    Percentage = p.DailyTarget > 0 ? Math.Round((p.CompletedBoxes / (double)p.DailyTarget * 100), 1) : 0,
                    StatusText = p.CompletedBoxes > p.DailyTarget ? "Ahead" : 
                                (p.CompletedBoxes < p.DailyTarget ? "Behind" : "On track"),
                    DateDisplay = Shared.Utils.GeorgianDateHelper.FormatShamsiAsGeorgianForDisplay(p.Date)
                }).ToList();
                
                WeeklyProgressDataGrid.ItemsSource = dailyBreakdown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading weekly progress");
            }

        }

        private void LoadShiftSlots()
        {
            LoadShiftGroups();
        }

        /// <summary>Public for controls (e.g. ShiftGroupControl) to refresh shift view after label assign/remove.</summary>
        public void RefreshShiftSlots()
        {
            LoadShiftSlots();
            UpdateShiftStatistics();
        }

        private void UpdateShiftStatistics()
        {
            UpdateDailyPreview();
        }

        private void ShiftGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ShiftGroupComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string groupId && ShiftGroupsItemsControl != null && ShiftGroups != null)
                {
                    if (groupId == "all")
                    {
                        ShiftGroupsItemsControl.ItemsSource = ShiftGroups;
                    }
                    else
                    {
                        ShiftGroupsItemsControl.ItemsSource = ShiftGroups.Where(g => g.GroupId == groupId).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ShiftGroupComboBox_SelectionChanged");
            }
        }

        private void BadgeSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Binding in XAML handles live updates
        }

        private void GroupSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Binding in XAML handles live updates
        }
    }
}