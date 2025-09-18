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
using Shared.Models;
using Shared.Services;
using Shared.Utils;

namespace ManagementApp.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainController _controller;
        private readonly ILogger<MainWindow> _logger;
        private readonly DispatcherTimer _timer;
        private Employee? _selectedEmployee;
        private Shared.Models.Task? _selectedTask;

        public MainWindow()
        {
            InitializeComponent();
            
            _controller = new MainController();
            _logger = LoggingService.CreateLogger<MainWindow>();
            
            // Setup timer for status updates
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Subscribe to controller events
            _controller.EmployeesUpdated += OnEmployeesUpdated;
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
                TaskTargetDatePicker.SelectedDate = ShamsiDateHelper.GetCurrentShamsiDate();
                
                // Initialize report dates
                ReportStartDatePicker.SelectedDate = ShamsiDateHelper.GetCurrentShamsiDate();
                ReportEndDatePicker.SelectedDate = ShamsiDateHelper.GetCurrentShamsiDate();

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

                _logger.LogInformation("UI initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing UI");
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
                var employees = _controller.GetAllEmployees();
                EmployeeListBox.ItemsSource = employees;
                
                // Filter out absent employees for shift assignment
                var todayShamsi = ShamsiDateHelper.GetCurrentShamsiDate();
                var availableEmployees = employees.Where(emp => 
                    !_controller.AbsenceManager.HasAbsenceForEmployee(emp, todayShamsi)).ToList();
                ShiftEmployeeListBox.ItemsSource = availableEmployees;
                
                // Set up drag functionality for each employee item
                foreach (var employee in availableEmployees)
                {
                    if (ShiftEmployeeListBox.ItemContainerGenerator.ContainerFromItem(employee) is ListBoxItem item)
                    {
                        item.PreviewMouseLeftButtonDown += (s, e) => Employee_PreviewMouseLeftButtonDown(s, e, employee);
                        item.MouseMove += (s, e) => Employee_MouseMove(s, e, employee);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employees");
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
                RoleTextBox.Text = employee.Role;
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
                var dialog = new EmployeeDialog();
                if (dialog.ShowDialog() == true)
                {
                    var success = _controller.AddEmployee(dialog.FirstName, dialog.LastName, dialog.Role, dialog.PhotoPath, dialog.IsManager);
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

                var dialog = new EmployeeDialog(_selectedEmployee);
                if (dialog.ShowDialog() == true)
                {
                    var success = _controller.UpdateEmployee(_selectedEmployee.EmployeeId, dialog.FirstName, dialog.LastName, dialog.Role, dialog.PhotoPath, dialog.IsManager);
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

        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"آیا از حذف کارمند {_selectedEmployee.FullName} اطمینان دارید؟", 
                    "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    var success = _controller.DeleteEmployee(_selectedEmployee.EmployeeId);
                    if (success)
                    {
                        LoadEmployees();
                        UpdateStatus($"کارمند {_selectedEmployee.FullName} حذف شد");
                        _selectedEmployee = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee");
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
                    // Update employee photo path
                    _controller.UpdateEmployee(_selectedEmployee.EmployeeId, photoPath: openFileDialog.FileName);
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

        private void SaveEmployeeChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedEmployee == null)
                {
                    MessageBox.Show("لطفاً یک کارمند را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var success = _controller.UpdateEmployee(_selectedEmployee.EmployeeId, 
                    FirstNameTextBox.Text, LastNameTextBox.Text, RoleTextBox.Text, null, IsManagerCheckBox.IsChecked);
                
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

        private void LoadShifts()
        {
            try
            {
                LoadShiftSlots();
                UpdateShiftStatistics();
                
                // Load absent employees
                var todayShamsi = ShamsiDateHelper.GetCurrentShamsiDate();
                var absentEmployees = _controller.GetAllEmployees().Where(emp => 
                    _controller.AbsenceManager.HasAbsenceForEmployee(emp, todayShamsi)).ToList();
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
                var capacity = _controller.ShiftManager.Capacity;
                _logger.LogInformation("LoadShiftSlots: Current capacity is {Capacity}", capacity);
                
                // Clear existing slots
                MorningShiftPanel.Children.Clear();
                EveningShiftPanel.Children.Clear();

                ShiftCapacityTextBox.Text = capacity.ToString();
                _logger.LogInformation("LoadShiftSlots: TextBox updated to {Capacity}", capacity);

                // Create morning shift slots in a grid layout
                var morningGrid = CreateShiftGrid("morning", capacity);
                MorningShiftPanel.Children.Add(morningGrid);

                // Create evening shift slots in a grid layout
                var eveningGrid = CreateShiftGrid("evening", capacity);
                EveningShiftPanel.Children.Add(eveningGrid);
                
                _logger.LogInformation("LoadShiftSlots: Grids created with capacity {Capacity}", capacity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shift slots");
            }
        }

        private Grid CreateShiftGrid(string shiftType, int capacity)
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

            var employee = _controller.ShiftManager.GetShift(shiftType)?.GetEmployeeAtSlot(slotIndex);
            
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
                var morningCount = _controller.ShiftManager.MorningShift.AssignedEmployees.Count(emp => emp != null);
                var eveningCount = _controller.ShiftManager.EveningShift.AssignedEmployees.Count(emp => emp != null);
                var capacity = _controller.ShiftManager.Capacity;

                MorningShiftStats.Text = $"{morningCount}/{capacity}";
                EveningShiftStats.Text = $"{eveningCount}/{capacity}";
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
                    _controller.ClearShift("morning");
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
                    _controller.ClearShift("evening");
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

        #endregion

        #region Drag and Drop

        private Point _dragStartPoint;
        private Employee? _draggedEmployee;

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
                if (e.Data.GetDataPresent(typeof(Employee)))
                {
                    e.Effects = DragDropEffects.Move;
                    (sender as Border)!.Background = Brushes.LightBlue;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in slot drag over");
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
                        var success = _controller.AssignEmployeeToShift(employee, shiftType, slotIndex);
                        if (success)
                        {
                            LoadShiftSlots();
                            UpdateShiftStatistics();
                            LoadEmployees(); // Refresh employee lists
                            UpdateStatus($"کارمند {employee.FullName} به شیفت {shiftType} تخصیص داده شد");
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

        private void Slot_Click(object sender, MouseButtonEventArgs e, string shiftType, int slotIndex)
        {
            try
            {
                var employee = _controller.ShiftManager.GetShift(shiftType)?.GetEmployeeAtSlot(slotIndex);
                
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
                        var success = _controller.RemoveEmployeeFromShift(employee, shiftType);
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
                var todayShamsi = ShamsiDateHelper.GetCurrentShamsiDate();
                var availableEmployees = _controller.GetAllEmployees().Where(emp => 
                    !_controller.AbsenceManager.HasAbsenceForEmployee(emp, todayShamsi)).ToList();

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
                        var success = _controller.AssignEmployeeToShift(selectedEmployee, shiftType, slotIndex);
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
                var tasks = _controller.GetAllTasks();
                TaskListBox.ItemsSource = tasks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading tasks");
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
                TaskTitleTextBox.Text = task.Title;
                TaskDescriptionTextBox.Text = task.Description;
                
                // Set priority
                TaskPriorityComboBox.SelectedIndex = (int)task.Priority;
                
                TaskEstimatedHoursTextBox.Text = task.EstimatedHours.ToString();
                
                // Set the target date (already in Shamsi format)
                TaskTargetDatePicker.SelectedDate = task.TargetDate;
                
                // Set status
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

                var result = MessageBox.Show($"آیا از حذف وظیفه {_selectedTask.Title} اطمینان دارید؟", 
                    "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    var success = _controller.DeleteTask(_selectedTask.TaskId);
                    if (success)
                    {
                        LoadTasks();
                        UpdateStatus($"وظیفه {_selectedTask.Title} حذف شد");
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
                if (_selectedTask == null)
                {
                    MessageBox.Show("لطفاً یک وظیفه را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var success = _controller.UpdateTask(_selectedTask.TaskId, status: "در حال انجام");
                if (success)
                {
                    LoadTasks();
                    UpdateStatus($"وظیفه {_selectedTask.Title} شروع شد");
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

                double.TryParse(TaskActualHoursTextBox.Text, out double actualHours);
                var success = _controller.UpdateTask(_selectedTask.TaskId, status: "تکمیل شده", actualHours: actualHours, notes: TaskNotesTextBox.Text);
                if (success)
                {
                    LoadTasks();
                    UpdateStatus($"وظیفه {_selectedTask.Title} تکمیل شد");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing task");
                MessageBox.Show($"خطا در تکمیل وظیفه: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AssignTaskToEmployee_Click(object sender, RoutedEventArgs e)
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

                if (sender is Button button && button.Tag is string employeeId)
                {
                    var employee = _controller.GetAllEmployees().FirstOrDefault(emp => emp.EmployeeId == employeeId);
                    if (employee != null)
                    {
                        var result = MessageBox.Show($"آیا می‌خواهید {employee.FullName} را از این وظیفه حذف کنید؟", 
                            "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            var success = _controller.RemoveTaskFromEmployee(_selectedTask.TaskId, employeeId);
                            if (success)
                            {
                                LoadTaskAssignments();
                                UpdateStatus($"{employee.FullName} از وظیفه {_selectedTask.Title} حذف شد");
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
                var availableEmployees = _controller.GetAvailableEmployeesForTask(_selectedTask.TaskId);
                
                if (availableEmployees.Count == 0)
                {
                    MessageBox.Show("همه کارمندان به این وظیفه تخصیص داده شده‌اند", "اطلاع", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new Window
                {
                    Title = $"تخصیص کارمند به وظیفه: {_selectedTask.Title}",
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
                        var success = _controller.AssignTaskToEmployee(_selectedTask.TaskId, selectedEmployee.EmployeeId);
                        if (success)
                        {
                            LoadTaskAssignments();
                            UpdateStatus($"{selectedEmployee.FullName} به وظیفه {_selectedTask.Title} تخصیص داده شد");
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
                    // Get employee names for assigned employee IDs
                    var assignedEmployeeNames = new List<string>();
                    foreach (var employeeId in _selectedTask.AssignedEmployees)
                    {
                        var employee = _controller.GetAllEmployees().FirstOrDefault(emp => emp.EmployeeId == employeeId);
                        if (employee != null)
                        {
                            assignedEmployeeNames.Add($"{employee.FullName} ({employeeId})");
                        }
                    }
                    AssignedEmployeesListBox.ItemsSource = assignedEmployeeNames;
                }
                else
                {
                    AssignedEmployeesListBox.ItemsSource = new List<string>();
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
                var startDateShamsi = ReportStartDatePicker.SelectedDate;
                var endDateShamsi = ReportEndDatePicker.SelectedDate;

                if (string.IsNullOrEmpty(startDateShamsi) || string.IsNullOrEmpty(endDateShamsi))
                {
                    MessageBox.Show("لطفاً تاریخ شروع و پایان را انتخاب کنید", "هشدار", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var report = GenerateReport(reportType, startDateShamsi, endDateShamsi);
                ReportPreviewTextBlock.Text = report;
                UpdateStatus("گزارش تولید شد");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report");
                MessageBox.Show($"خطا در تولید گزارش: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateReport(string? reportType, string startDateShamsi, string endDateShamsi)
        {
            try
            {
                var report = $"گزارش {reportType}\n";
                report += $"تاریخ شروع: {ShamsiDateHelper.FormatForDisplay(startDateShamsi)}\n";
                report += $"تاریخ پایان: {ShamsiDateHelper.FormatForDisplay(endDateShamsi)}\n\n";

                // Employee statistics
                var totalEmployees = _controller.GetAllEmployees().Count;
                var absentEmployees = _controller.GetAllAbsences().Count;
                
                report += "آمار کارمندان:\n";
                report += $"کل کارمندان: {totalEmployees}\n";
                report += $"کارمندان غایب: {absentEmployees}\n\n";

                // Shift statistics
                var morningCount = _controller.ShiftManager.MorningShift.AssignedEmployees.Count(emp => emp != null);
                var eveningCount = _controller.ShiftManager.EveningShift.AssignedEmployees.Count(emp => emp != null);
                
                report += "آمار شیفت‌ها:\n";
                report += $"شیفت صبح: {morningCount}/{_controller.ShiftManager.Capacity}\n";
                report += $"شیفت عصر: {eveningCount}/{_controller.ShiftManager.Capacity}\n\n";

                // Task statistics
                var tasks = _controller.GetAllTasks();
                var completedTasks = tasks.Count(t => t.Status == Shared.Models.TaskStatus.Completed);
                var inProgressTasks = tasks.Count(t => t.Status == Shared.Models.TaskStatus.InProgress);
                var pendingTasks = tasks.Count(t => t.Status == Shared.Models.TaskStatus.Pending);
                
                report += "آمار وظایف:\n";
                report += $"کل وظایف: {tasks.Count}\n";
                report += $"تکمیل شده: {completedTasks}\n";
                report += $"در حال انجام: {inProgressTasks}\n";
                report += $"در انتظار: {pendingTasks}\n\n";

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report content");
                return $"خطا در تولید گزارش: {ex.Message}";
            }
        }

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    Title = "ذخیره گزارش",
                    DefaultExt = "txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, ReportPreviewTextBlock.Text, System.Text.Encoding.UTF8);
                    UpdateStatus("گزارش صادر شد");
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
            });
        }

        private void OnTasksUpdated()
        {
            Dispatcher.Invoke(() =>
            {
                LoadTasks();
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

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _timer?.Stop();
                _controller?.Cleanup();
                _logger.LogInformation("MainWindow closed");
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