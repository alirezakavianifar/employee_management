using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Services;
using Shared.Utils;
using DisplayApp.Services;
using DisplayApp.Models;
using Newtonsoft.Json.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace DisplayApp
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly DataService _dataService;
        private readonly ChartService _chartService;
        private readonly AIService _aiService;
        private SyncManager _syncManager;
        
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _countdownTimer;
        private int _countdownSeconds = 30;
        private DateTime _lastUpdateTime;
        
        // Chart data
        public SeriesCollection SeriesCollection { get; set; }
        public string[] Labels { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize logging
            _logger = LoggingService.CreateLogger<MainWindow>();
            _logger.LogInformation("DisplayApp MainWindow initialized");
            
            // Initialize services
            _dataService = new DataService();
            _chartService = new ChartService();
            _aiService = new AIService();
            // Use configuration system to get data directory
            var config = Shared.Utils.AppConfigHelper.Config;
            _syncManager = new SyncManager(config.DataDirectory);
            
            // Setup timers
            SetupTimers();
            
            // Setup sync callbacks
            _syncManager.AddSyncCallback(OnDataChanged);
            
            // Subscribe to configuration changes
            Shared.Utils.AppConfigHelper.ConfigurationChanged += OnConfigurationChanged;
            
            // Initialize chart
            InitializeChart();
            
            // Load initial data
            LoadData();
            
            // Start sync
            _syncManager.StartSync();
            
            _logger.LogInformation("DisplayApp started successfully");
        }

        private void SetupTimers()
        {
            try
            {
                _logger.LogInformation("Setting up timers...");
                
                // Refresh timer - every 30 seconds
                _refreshTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(30)
                };
                _refreshTimer.Tick += RefreshTimer_Tick;
                _refreshTimer.Start();
                _logger.LogInformation("Refresh timer started successfully");
                
                // Countdown timer - every second
                _countdownTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _countdownTimer.Tick += CountdownTimer_Tick;
                _countdownTimer.Start();
                _logger.LogInformation("Countdown timer started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up timers");
            }
        }

        private void InitializeChart()
        {
            SeriesCollection = new SeriesCollection();
            
            // Initialize with empty data - will be populated when real data loads
            SeriesCollection.Add(new LineSeries
            {
                Title = "عملکرد",
                Values = new ChartValues<double> { 80.0 },
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 8,
                Stroke = Brushes.LightBlue,
                Fill = Brushes.LightBlue,
                StrokeThickness = 2
            });
            
            // Initialize with dynamic labels
            Labels = _chartService.GetWeekLabels();
            
            DataContext = this;
        }

        private async void LoadData()
        {
            try
            {
                // StatusText removed - no longer needed
                _logger.LogInformation("LoadData method called - starting data load...");
                
                // Data reading and transformation is working correctly
                
                // StatusText removed - no longer needed
                
                var reportData = await _dataService.GetLatestReportAsync();
                if (reportData != null)
                {
                    _logger.LogInformation("Report data loaded successfully. Keys: {Keys}", 
                        string.Join(", ", reportData.Keys));
                    
                    // Debug: Show data keys in status
                    // StatusText removed - no longer needed
                    
                    UpdateUI(reportData);
                    _lastUpdateTime = DateTime.Now;
                    LastUpdateText.Text = $"آخرین بروزرسانی: {_lastUpdateTime:HH:mm:ss}";
                    // StatusText removed - no longer needed
                }
                else
                {
                    // StatusText removed - no longer needed
                    _logger.LogWarning("No report data found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data");
                // StatusText removed - no longer needed
                ShowErrorDialog("خطا در بارگذاری داده‌ها", ex.Message);
            }
        }

        private void UpdateUI(Dictionary<string, object> reportData)
        {
            try
            {
                // Debug: Show that UpdateUI is being called
                _logger.LogInformation("UpdateUI method called with {Count} data keys", reportData.Count);
                // StatusText removed - no longer needed
                
                // Update managers
                UpdateManagersPanel(reportData);
                
                // Update shifts
                UpdateShiftsPanel(reportData);
                
                // Update absence counts
                UpdateAbsencePanel(reportData);
                
                // Update AI recommendation
                UpdateAIRecommendation(reportData);
                
                // Update chart
                UpdateChart(reportData);
                
                _logger.LogInformation("UI updated successfully");
                // StatusText removed - no longer needed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating UI");
                // StatusText removed - no longer needed
            }
        }

        private void UpdateManagersPanel(Dictionary<string, object> reportData)
        {
            _logger.LogInformation("UpdateManagersPanel called");
            ManagersGrid.Children.Clear();
            ManagersGrid.RowDefinitions.Clear();
            
            if (reportData.TryGetValue("managers", out var managersObj))
            {
                _logger.LogInformation("Managers object found: {Type}", managersObj?.GetType().Name ?? "null");
                
                List<object> managers = null;
                
                if (managersObj is List<object> managersList)
                {
                    managers = managersList;
                    _logger.LogInformation("Found {Count} managers to display (List<object>)", managers.Count);
                }
                else if (managersObj is Newtonsoft.Json.Linq.JArray managersJArray)
                {
                    managers = managersJArray.ToObject<List<object>>() ?? new List<object>();
                    _logger.LogInformation("Found {Count} managers to display (JArray converted)", managers.Count);
                }
                else
                {
                    _logger.LogWarning("Managers object is not a List<object> or JArray: {Type}", managersObj?.GetType().Name ?? "null");
                }
                
                if (managers != null && managers.Count > 0)
                {
                    const int managersPerRow = 8;
                    int totalRows = (int)Math.Ceiling((double)managers.Count / managersPerRow);
                    
                    // Create row definitions
                    for (int row = 0; row < totalRows; row++)
                    {
                        ManagersGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(106) }); // 100 + 6 for margin
                    }
                    
                    for (int i = 0; i < managers.Count; i++)
                    {
                        Dictionary<string, object> managerData = null;
                        
                        if (managers[i] is Dictionary<string, object> managerDict)
                        {
                            managerData = managerDict;
                        }
                        else if (managers[i] is Newtonsoft.Json.Linq.JObject managerJObject)
                        {
                            // Convert JObject to Dictionary
                            managerData = ConvertJObjectToDictionary(managerJObject);
                            _logger.LogInformation("Converted JObject manager to Dictionary");
                        }
                        
                        if (managerData != null)
                        {
                            var name = $"{managerData.GetValueOrDefault("first_name", "")} {managerData.GetValueOrDefault("last_name", "")}".Trim();
                            _logger.LogInformation("Creating manager card for: {Name}", name);
                            var managerCard = CreateManagerCard(managerData);
                            
                            int row = i / managersPerRow;
                            int column = i % managersPerRow;
                            
                            Grid.SetRow(managerCard, row);
                            Grid.SetColumn(managerCard, column);
                            ManagersGrid.Children.Add(managerCard);
                        }
                        else
                        {
                            _logger.LogWarning("Manager item {Index} is not a Dictionary or JObject: {Type}", i, managers[i]?.GetType().Name ?? "null");
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("No managers key found in report data");
            }
        }

        private Border CreateManagerCard(Dictionary<string, object> managerData)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Margin = new Thickness(3),
                Width = 80,
                Height = 100
            };
            
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // Manager photo
            var image = new Image
            {
                Width = 40,
                Height = 40,
                Stretch = Stretch.Uniform
            };
            
            if (managerData.TryGetValue("photo_path", out var photoPath) && !string.IsNullOrEmpty(photoPath.ToString()))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(photoPath.ToString(), UriKind.Absolute));
                    image.Source = bitmap;
                }
                catch
                {
                    // Use placeholder if image fails to load
                    image.Source = CreatePlaceholderImage();
                }
            }
            else
            {
                image.Source = CreatePlaceholderImage();
            }
            
            // Manager name
            var nameText = new TextBlock
            {
                Text = $"{managerData.GetValueOrDefault("first_name", "")} {managerData.GetValueOrDefault("last_name", "")}".Trim(),
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            };
            
            // Manager role
            var roleText = new TextBlock
            {
                Text = managerData.GetValueOrDefault("role", "مدیر").ToString(),
                Foreground = Brushes.LightGray,
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            
            stackPanel.Children.Add(image);
            stackPanel.Children.Add(nameText);
            stackPanel.Children.Add(roleText);
            
            card.Child = stackPanel;
            return card;
        }

        private BitmapImage CreatePlaceholderImage()
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.Gray, null, new System.Windows.Rect(0, 0, 40, 40));
                drawingContext.DrawText(
                    new FormattedText("مدیر", 
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.RightToLeft,
                        new Typeface("Tahoma"),
                        12,
                        Brushes.White,
                        96),
                    new System.Windows.Point(10, 13));
            }
            
            var renderTargetBitmap = new RenderTargetBitmap(40, 40, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            renderTargetBitmap.Freeze();
            
            return ConvertToBitmapImage(renderTargetBitmap);
        }

        private BitmapImage ConvertToBitmapImage(RenderTargetBitmap renderTarget)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            
            using var stream = new MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }

        private void UpdateShiftsPanel(Dictionary<string, object> reportData)
        {
            _logger.LogInformation("Updating shifts panel for separate group display");
            
            // Check if the groups container exists
            if (GroupsContainer == null)
            {
                _logger.LogError("Groups container not found in UI");
                return;
            }
            
            // Debug: Log all keys in report data
            _logger.LogInformation("Report data keys: {Keys}", string.Join(", ", reportData.Keys));
            
            // Get all groups data
            var groups = GetAllGroupsData(reportData);
            _logger.LogInformation("Found {Count} groups from GetAllGroupsData", groups.Count);
            
            // Use Dispatcher to update UI from background thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Clear existing groups
                    GroupsContainer.Children.Clear();
                    
                    if (groups.Count == 0)
                    {
                        _logger.LogWarning("No groups found to display");
                        return;
                    }
                    
                    // Create UI for each group
                    foreach (var group in groups)
                    {
                        var groupPanel = CreateGroupPanel(group);
                        GroupsContainer.Children.Add(groupPanel);
                    }
                    
                    _logger.LogInformation("Successfully updated {Count} group displays", groups.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating shifts UI");
                }
            });
        }

        private Border CreateGroupPanel(DisplayApp.Models.GroupDisplayModel group)
        {
            // Create the main border for the group
            var groupBorder = new Border
            {
                Style = Application.Current.FindResource("CardStyle") as Style,
                Margin = new Thickness(5),
                MinWidth = 400,
                MaxWidth = 500
            };
            
            var groupGrid = new Grid();
            groupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            groupGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            // Group header
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(3)
            };
            
            var headerText = new TextBlock
            {
                Text = group.GroupName,
                Style = Application.Current.FindResource("SubtitleTextStyle") as Style,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold
            };
            
            headerBorder.Child = headerText;
            Grid.SetRow(headerBorder, 0);
            groupGrid.Children.Add(headerBorder);
            
            // Shifts container
            var shiftsGrid = new Grid
            {
                Margin = new Thickness(5)
            };
            shiftsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            shiftsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Evening Shift (Right side in RTL)
            var eveningBorder = CreateShiftPanel("شیفت عصر", group.EveningShiftEmployees, "#FF6B9BD1");
            Grid.SetColumn(eveningBorder, 0);
            shiftsGrid.Children.Add(eveningBorder);
            
            // Morning Shift (Left side in RTL)
            var morningBorder = CreateShiftPanel("شیفت صبح", group.MorningShiftEmployees, "#FF81C784");
            Grid.SetColumn(morningBorder, 1);
            shiftsGrid.Children.Add(morningBorder);
            
            Grid.SetRow(shiftsGrid, 1);
            groupGrid.Children.Add(shiftsGrid);
            
            groupBorder.Child = groupGrid;
            return groupBorder;
        }
        
        private Border CreateShiftPanel(string shiftTitle, List<DisplayApp.Models.EmployeeDisplayModel> employees, string color)
        {
            var shiftBorder = new Border
            {
                Style = Application.Current.FindResource("CardStyle") as Style,
                Margin = new Thickness(2),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                BorderThickness = new Thickness(2)
            };
            
            var shiftGrid = new Grid();
            shiftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shiftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            // Shift title
            var titleText = new TextBlock
            {
                Text = shiftTitle,
                Style = Application.Current.FindResource("SubtitleTextStyle") as Style,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            Grid.SetRow(titleText, 0);
            shiftGrid.Children.Add(titleText);
            
            // Employees scroll viewer
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 500
            };
            
            var itemsControl = new ItemsControl();
            
            // Set items panel to UniformGrid
            var itemsPanel = new ItemsPanelTemplate();
            var uniformGridFactory = new FrameworkElementFactory(typeof(UniformGrid));
            uniformGridFactory.SetValue(UniformGrid.ColumnsProperty, 3);
            itemsPanel.VisualTree = uniformGridFactory;
            itemsControl.ItemsPanel = itemsPanel;
            
            // Set data template
            var dataTemplate = new DataTemplate();
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(60, 60, 60)));
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(102, 102, 102)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(5));
            borderFactory.SetValue(Border.MarginProperty, new Thickness(2));
            borderFactory.SetValue(Border.WidthProperty, 70.0);
            borderFactory.SetValue(Border.HeightProperty, 90.0);
            
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            
            var imageFactory = new FrameworkElementFactory(typeof(Image));
            imageFactory.SetValue(Image.WidthProperty, 35.0);
            imageFactory.SetValue(Image.HeightProperty, 35.0);
            imageFactory.SetValue(Image.StretchProperty, Stretch.Uniform);
            imageFactory.SetBinding(Image.SourceProperty, new Binding("PhotoPath") { Converter = Application.Current.FindResource("ImagePathConverter") as IValueConverter });
            
            var nameTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameTextFactory.SetBinding(TextBlock.TextProperty, new Binding("FullName"));
            nameTextFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            nameTextFactory.SetValue(TextBlock.FontSizeProperty, 9.0);
            nameTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            nameTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            nameTextFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            nameTextFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));
            
            stackPanelFactory.AppendChild(imageFactory);
            stackPanelFactory.AppendChild(nameTextFactory);
            borderFactory.AppendChild(stackPanelFactory);
            dataTemplate.VisualTree = borderFactory;
            itemsControl.ItemTemplate = dataTemplate;
            
            // Set items source
            itemsControl.ItemsSource = employees;
            
            scrollViewer.Content = itemsControl;
            Grid.SetRow(scrollViewer, 1);
            shiftGrid.Children.Add(scrollViewer);
            
            shiftBorder.Child = shiftGrid;
            return shiftBorder;
        }

        private Dictionary<string, object> ConvertJObjectToDictionary(JObject jObject)
        {
            var dictionary = new Dictionary<string, object>();
            
            foreach (var property in jObject.Properties())
            {
                if (property.Value is JObject nestedJObject)
                {
                    dictionary[property.Name] = ConvertJObjectToDictionary(nestedJObject);
                }
                else if (property.Value is JArray jArray)
                {
                    var list = new List<object>();
                    foreach (var item in jArray)
                    {
                        if (item is JObject itemJObject)
                        {
                            list.Add(ConvertJObjectToDictionary(itemJObject));
                        }
                        else
                        {
                            list.Add(item.ToObject<object>());
                        }
                    }
                    dictionary[property.Name] = list;
                }
                else
                {
                    dictionary[property.Name] = property.Value.ToObject<object>();
                }
            }
            
            return dictionary;
        }

        private void UpdateShiftPanel(ItemsControl panel, Dictionary<string, object> shiftData, string shiftType)
        {
            _logger.LogInformation("Updating {ShiftType} shift panel", shiftType);
            
            // Check if we have assigned employees
            if (shiftData.TryGetValue("assigned_employees", out var employeesObj) && employeesObj is List<object> employees)
            {
                _logger.LogInformation("Found {Count} assigned employees for {ShiftType} shift", employees.Count, shiftType);
                
                foreach (var employeeObj in employees)
                {
                    if (employeeObj is Dictionary<string, object> employeeData)
                    {
                        _logger.LogInformation("Creating employee card for {EmployeeId}", 
                            employeeData.GetValueOrDefault("employee_id", "Unknown"));
                        var employeeCard = CreateEmployeeCard(employeeData, shiftType);
                        panel.Items.Add(employeeCard);
                    }
                }
            }
            else
            {
                _logger.LogInformation("No assigned employees found for {ShiftType} shift", shiftType);
            }
        }

        private Border CreateEmployeeCard(Dictionary<string, object> employeeData, string shiftType = "")
        {
            // Debug logging to see what's in the employee data
            _logger.LogInformation("Creating employee card with data keys: {Keys}", string.Join(", ", employeeData.Keys));
            _logger.LogInformation("Employee first_name: '{FirstName}', employee_id: '{EmployeeId}'", 
                employeeData.GetValueOrDefault("first_name", "").ToString(),
                employeeData.GetValueOrDefault("employee_id", "").ToString());
            
            // Both shifts use the same size
            double sizeMultiplier = 1.0;
            
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8 * sizeMultiplier),
                Margin = new Thickness(3 * sizeMultiplier),
                Width = 80 * sizeMultiplier,
                Height = 100 * sizeMultiplier
            };
            
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // Employee photo
            var image = new Image
            {
                Width = 40 * sizeMultiplier,
                Height = 40 * sizeMultiplier,
                Stretch = Stretch.Uniform
            };
            
            if (employeeData.TryGetValue("photo_path", out var photoPath) && !string.IsNullOrEmpty(photoPath.ToString()))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(photoPath.ToString(), UriKind.Absolute));
                    image.Source = bitmap;
                }
                catch
                {
                    image.Source = CreateEmployeePlaceholderImage(40 * sizeMultiplier);
                }
            }
            else
            {
                image.Source = CreateEmployeePlaceholderImage(40 * sizeMultiplier);
            }
            
            // Employee name (first and last name)
            var firstName = employeeData.GetValueOrDefault("first_name", "").ToString();
            var lastName = employeeData.GetValueOrDefault("last_name", "").ToString();
            var fullName = $"{firstName} {lastName}".Trim();
            _logger.LogInformation("Using full name: '{FullName}'", fullName);
            
            var nameText = new TextBlock
            {
                Text = fullName,
                Foreground = Brushes.White,
                FontSize = 10 * sizeMultiplier,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3 * sizeMultiplier, 0, 0)
            };
            
            stackPanel.Children.Add(image);
            stackPanel.Children.Add(nameText);
            
            card.Child = stackPanel;
            return card;
        }

        private BitmapImage CreateEmployeePlaceholderImage(double size = 50)
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.DarkGray, null, new System.Windows.Rect(0, 0, size, size));
                drawingContext.DrawText(
                    new FormattedText("کارگر", 
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.RightToLeft,
                        new Typeface("Tahoma"),
                        size * 0.16, // Scale font size with image size
                        Brushes.White,
                        96),
                    new System.Windows.Point(size * 0.16, size * 0.3)); // Scale text position
            }
            
            var renderTargetBitmap = new RenderTargetBitmap((int)size, (int)size, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            renderTargetBitmap.Freeze();
            
            return ConvertToBitmapImage(renderTargetBitmap);
        }

        private void UpdateAbsencePanel(Dictionary<string, object> reportData)
        {
            // Update absence counts and cards
            if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
            {
                LeaveCount.Text = GetAbsenceCount(absences, "مرخصی").ToString();
                SickCount.Text = GetAbsenceCount(absences, "بیمار").ToString();
                AbsentCount.Text = GetAbsenceCount(absences, "غایب").ToString();
                
                // Update absence cards in separate panels
                UpdateAbsenceCards(absences);
            }
        }
        
        private void UpdateAbsenceCards(Dictionary<string, object> absences)
        {
            _logger.LogInformation("Updating absence cards");
            
            // Check if panels exist
            if (LeavePanel == null || SickPanel == null || AbsentPanel == null)
            {
                _logger.LogError("One or more absence panels not found in UI");
                return;
            }
            
            // Clear all panels
            LeavePanel.Items.Clear();
            SickPanel.Items.Clear();
            AbsentPanel.Items.Clear();
            
            // Process Leave (مرخصی) category
            if (absences.TryGetValue("مرخصی", out var leaveObj) && leaveObj is List<object> leaveList)
            {
                _logger.LogInformation("Found {Count} leave absences", leaveList.Count);
                foreach (var absenceObj in leaveList)
                {
                    if (absenceObj is Dictionary<string, object> absenceData)
                    {
                        var absenceCard = CreateAbsenceCard(absenceData, "مرخصی");
                        LeavePanel.Items.Add(absenceCard);
                    }
                }
            }
            
            // Process Sick (بیمار) category
            if (absences.TryGetValue("بیمار", out var sickObj) && sickObj is List<object> sickList)
            {
                _logger.LogInformation("Found {Count} sick absences", sickList.Count);
                foreach (var absenceObj in sickList)
                {
                    if (absenceObj is Dictionary<string, object> absenceData)
                    {
                        var absenceCard = CreateAbsenceCard(absenceData, "بیمار");
                        SickPanel.Items.Add(absenceCard);
                    }
                }
            }
            
            // Process Absent (غایب) category
            if (absences.TryGetValue("غایب", out var absentObj) && absentObj is List<object> absentList)
            {
                _logger.LogInformation("Found {Count} absent absences", absentList.Count);
                foreach (var absenceObj in absentList)
                {
                    if (absenceObj is Dictionary<string, object> absenceData)
                    {
                        var absenceCard = CreateAbsenceCard(absenceData, "غایب");
                        AbsentPanel.Items.Add(absenceCard);
                    }
                }
            }
        }
        
        private Border CreateAbsenceCard(Dictionary<string, object> absenceData, string category)
        {
            _logger.LogInformation("Creating absence card with data keys: {Keys}", string.Join(", ", absenceData.Keys));
            
            // Get employee information
            var firstName = absenceData.GetValueOrDefault("first_name", "").ToString();
            var lastName = absenceData.GetValueOrDefault("last_name", "").ToString();
            var fullName = $"{firstName} {lastName}".Trim();
            var employeeId = absenceData.GetValueOrDefault("employee_id", "").ToString();
            var photoPath = absenceData.GetValueOrDefault("photo_path", "").ToString();
            
            _logger.LogInformation("Employee first_name: '{FirstName}', employee_id: '{EmployeeId}'", firstName, employeeId);
            _logger.LogInformation("Using full name: '{FullName}'", fullName);
            
            // Create a simple card with photo and name
            var card = new Border
            {
                Width = 60,
                Height = 80,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(3),
                Margin = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Add image
            var image = new Image
            {
                Width = 45,
                Height = 45,
                Stretch = Stretch.Uniform
            };
            
            if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
            {
                try
                {
                    image.Source = new BitmapImage(new Uri(photoPath));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load image from {PhotoPath}", photoPath);
                    image.Source = CreateEmployeePlaceholderImage(45);
                }
            }
            else
            {
                image.Source = CreateEmployeePlaceholderImage(45);
            }
            
            stackPanel.Children.Add(image);
            
            // Add name below photo
            var nameText = new TextBlock
            {
                Text = fullName,
                Foreground = Brushes.White,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            };
            stackPanel.Children.Add(nameText);
            
            card.Child = stackPanel;
            return card;
        }
        
        private int GetAbsenceCount(Dictionary<string, object> absences, string category)
        {
            if (absences.TryGetValue(category, out var categoryObj) && categoryObj is List<object> categoryList)
            {
                return categoryList.Count;
            }
            return 0;
        }

        private void UpdateAIRecommendation(Dictionary<string, object> reportData)
        {
            try
            {
                var recommendation = _aiService.GetRecommendation(reportData);
                AIRecommendation.Text = recommendation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting AI recommendation");
                AIRecommendation.Text = "خطا در دریافت توصیه";
            }
        }

        private void UpdateChart(Dictionary<string, object> reportData)
        {
            try
            {
                var chartData = _chartService.GeneratePerformanceChart(reportData);
                if (chartData != null)
                {
                    SeriesCollection.Clear();
                    SeriesCollection.Add(chartData);
                    
                    // Update labels dynamically based on available data
                    Labels = _chartService.GetWeekLabels();
                    
                    // Update chart title based on data availability
                    var dataPoints = chartData.Values.Count;
                    if (dataPoints == 0)
                    {
                        ChartTitle.Text = "نمودار عملکرد - داده‌ای موجود نیست";
                        _logger.LogInformation("Chart has no data - no employees found");
                    }
                    else if (dataPoints == 1)
                    {
                        ChartTitle.Text = "عملکرد امروز";
                    }
                    else
                    {
                        ChartTitle.Text = $"عملکرد {dataPoints} روز اخیر";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chart");
                ChartTitle.Text = "نمودار عملکرد - خطا در بارگذاری";
            }
        }

        private void OnDataChanged()
        {
            Dispatcher.Invoke(() =>
            {
                LoadData();
            });
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            _logger.LogInformation("RefreshTimer_Tick called");
            LoadData();
            _countdownSeconds = 30; // Reset countdown
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _countdownSeconds--;
            CountdownText.Text = $"بروزرسانی بعدی: {_countdownSeconds}";
            
            if (_countdownSeconds <= 0)
            {
                _countdownSeconds = 30;
            }
        }

        private void ExitFullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
                    {
                        WindowState = WindowState.Normal;
                        WindowStyle = WindowStyle.SingleBorderWindow;
                    }
                    break;
                case Key.F11:
                    if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
                    {
                        WindowState = WindowState.Normal;
                        WindowStyle = WindowStyle.SingleBorderWindow;
                    }
                    else
                    {
                        WindowState = WindowState.Maximized;
                        WindowStyle = WindowStyle.None;
                    }
                    break;
            }
            base.OnKeyDown(e);
        }

        private void ShowErrorDialog(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void OnConfigurationChanged(Shared.Utils.AppConfig newConfig)
        {
            try
            {
                _logger.LogInformation("Configuration changed, updating sync manager with new data path: {DataPath}", 
                    newConfig.DataDirectory);
                
                // Stop current sync manager
                _syncManager?.Dispose();
                
                // Create new sync manager with new path
                _syncManager = new SyncManager(newConfig.DataDirectory);
                _syncManager.AddSyncCallback(OnDataChanged);
                _syncManager.StartSync();
                
                // Reload data with new path
                LoadData();
                
                _logger.LogInformation("DisplayApp updated with new configuration");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DisplayApp configuration");
            }
        }


        private List<DisplayApp.Models.GroupDisplayModel> GetAllGroupsData(Dictionary<string, object> reportData)
        {
            var groups = new List<DisplayApp.Models.GroupDisplayModel>();
            
            try
            {
                _logger.LogInformation("Getting all groups data from report");
                
                // Check if we have shift groups data
                if (reportData.ContainsKey("shift_groups"))
                {
                    _logger.LogInformation("Found shift_groups data");
                    var shiftGroupsObj = reportData["shift_groups"];
                    
                    if (shiftGroupsObj is Dictionary<string, object> shiftGroupsDict)
                    {
                        _logger.LogInformation("Processing shift groups from Dictionary");
                        
                        // Check if we have ShiftGroups data
                        if (shiftGroupsDict.TryGetValue("ShiftGroups", out var shiftGroupsData) && shiftGroupsData is Dictionary<string, object> shiftGroups)
                        {
                            _logger.LogInformation("Found {Count} shift groups in ShiftGroups", shiftGroups.Count);
                            
                            foreach (var groupEntry in shiftGroups)
                            {
                                var groupId = groupEntry.Key;
                                var groupDataStr = groupEntry.Value?.ToString();
                                
                                if (!string.IsNullOrEmpty(groupDataStr))
                                {
                                    try
                                    {
                                        // Parse the JSON string
                                        var groupData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(groupDataStr);
                                        if (groupData != null)
                                        {
                                            var groupModel = CreateGroupDisplayModelFromParsedData(groupData, groupId, reportData);
                                            if (groupModel != null)
                                            {
                                                groups.Add(groupModel);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to parse shift group data for group {GroupId}", groupId);
                                    }
                                }
                            }
                        }
                    }
                    else if (shiftGroupsObj is List<object> shiftGroupsList)
                    {
                        _logger.LogInformation("Processing {Count} shift groups", shiftGroupsList.Count);
                        
                        foreach (var groupObj in shiftGroupsList)
                        {
                            if (groupObj is Dictionary<string, object> groupData)
                            {
                                var groupModel = CreateGroupDisplayModel(groupData);
                                if (groupModel != null)
                                {
                                    groups.Add(groupModel);
                                }
                            }
                            else if (groupObj is JObject groupJObject)
                            {
                                var groupDict = ConvertJObjectToDictionary(groupJObject);
                                var groupModel = CreateGroupDisplayModel(groupDict);
                                if (groupModel != null)
                                {
                                    groups.Add(groupModel);
                                }
                            }
                        }
                    }
                    else if (shiftGroupsObj is JArray shiftGroupsJArray)
                    {
                        _logger.LogInformation("Processing {Count} shift groups from JArray", shiftGroupsJArray.Count);
                        
                        foreach (var groupItem in shiftGroupsJArray)
                        {
                            if (groupItem is JObject groupJObject)
                            {
                                var groupDict = ConvertJObjectToDictionary(groupJObject);
                                var groupModel = CreateGroupDisplayModel(groupDict);
                                if (groupModel != null)
                                {
                                    groups.Add(groupModel);
                                }
                            }
                        }
                    }
                }
                
                // If no shift groups found, try to get from shifts data (fallback)
                if (groups.Count == 0 && reportData.ContainsKey("shifts"))
                {
                    _logger.LogInformation("No shift groups found, checking shifts data for fallback");
                    var shiftsObj = reportData["shifts"];
                    
                    if (shiftsObj is Dictionary<string, object> shifts)
                    {
                        // Check if we have selected_group data
                        if (shifts.ContainsKey("selected_group") && shifts["selected_group"] is Dictionary<string, object> selectedGroupData)
                        {
                            _logger.LogInformation("Found selected_group data, creating fallback group");
                            var groupModel = CreateGroupDisplayModel(selectedGroupData);
                            if (groupModel != null)
                            {
                                groups.Add(groupModel);
                            }
                    }
                    else
                    {
                            // Create a default group from morning/evening shifts
                            _logger.LogInformation("Creating default group from morning/evening shifts");
                            var defaultGroup = new DisplayApp.Models.GroupDisplayModel
                            {
                                GroupName = "گروه پیش‌فرض",
                                GroupDescription = "گروه پیش‌فرض سیستم"
                            };
                            
                            // Get morning shift employees
                            if (shifts.TryGetValue("morning", out var morningShiftObj) && morningShiftObj is Dictionary<string, object> morningShift)
                            {
                                if (morningShift.TryGetValue("assigned_employees", out var morningEmployees) && morningEmployees is List<object> morningEmpList)
                                {
                                    defaultGroup.MorningShiftEmployees = ConvertToEmployeeDisplayModels(morningEmpList, defaultGroup.GroupName);
                                }
                            }
                            
                            // Get evening shift employees
                            if (shifts.TryGetValue("evening", out var eveningShiftObj) && eveningShiftObj is Dictionary<string, object> eveningShift)
                            {
                                if (eveningShift.TryGetValue("assigned_employees", out var eveningEmployees) && eveningEmployees is List<object> eveningEmpList)
                                {
                                    defaultGroup.EveningShiftEmployees = ConvertToEmployeeDisplayModels(eveningEmpList, defaultGroup.GroupName);
                                }
                            }
                            
                            groups.Add(defaultGroup);
                        }
                    }
                }
                
                _logger.LogInformation("Created {Count} group display models", groups.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all groups data");
            }
            
            return groups;
        }

        private DisplayApp.Models.CombinedDisplayModel? CreateCombinedDisplayModel(List<DisplayApp.Models.GroupDisplayModel> groups)
        {
            try
            {
                _logger.LogInformation("Creating combined display model from {Count} groups", groups.Count);
                
                var combinedModel = new DisplayApp.Models.CombinedDisplayModel
                {
                    TotalGroups = groups.Count,
                    DisplayTitle = "همه کارکنان"
                };
                
                // Debug: Log details about each group
                foreach (var group in groups)
                {
                    _logger.LogInformation("Group '{GroupName}': {MorningCount} morning, {EveningCount} evening employees", 
                        group.GroupName, group.MorningShiftEmployees.Count, group.EveningShiftEmployees.Count);
                }
                
                // Combine all morning shift employees
                foreach (var group in groups)
                {
                    _logger.LogInformation("Processing {Count} morning employees from group '{GroupName}'", 
                        group.MorningShiftEmployees.Count, group.GroupName);
                    foreach (var employee in group.MorningShiftEmployees)
                    {
                        // Set the group name for each employee
                        employee.GroupName = group.GroupName;
                        combinedModel.AllMorningShiftEmployees.Add(employee);
                        _logger.LogInformation("Added morning employee: {EmployeeName} from group {GroupName}", 
                            employee.FullName, group.GroupName);
                    }
                }
                
                // Combine all evening shift employees
                foreach (var group in groups)
                {
                    _logger.LogInformation("Processing {Count} evening employees from group '{GroupName}'", 
                        group.EveningShiftEmployees.Count, group.GroupName);
                    foreach (var employee in group.EveningShiftEmployees)
                    {
                        // Set the group name for each employee
                        employee.GroupName = group.GroupName;
                        combinedModel.AllEveningShiftEmployees.Add(employee);
                        _logger.LogInformation("Added evening employee: {EmployeeName} from group {GroupName}", 
                            employee.FullName, group.GroupName);
                    }
                }
                
                _logger.LogInformation("Combined {MorningCount} morning and {EveningCount} evening employees", 
                    combinedModel.AllMorningShiftEmployees.Count, 
                    combinedModel.AllEveningShiftEmployees.Count);
                
                return combinedModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating combined display model");
                return null;
            }
        }

        private DisplayApp.Models.GroupDisplayModel? CreateGroupDisplayModel(Dictionary<string, object> groupData)
        {
            try
            {
                var groupName = groupData.GetValueOrDefault("name", "گروه نامشخص")?.ToString() ?? "گروه نامشخص";
                var groupDescription = groupData.GetValueOrDefault("description", "")?.ToString() ?? "";
                
                _logger.LogInformation("Creating group display model for: {GroupName}", groupName);
                
                var groupModel = new DisplayApp.Models.GroupDisplayModel
                {
                    GroupName = groupName,
                    GroupDescription = groupDescription
                };
                
                // Get morning shift employees
                if (groupData.TryGetValue("morning_shift", out var morningShiftObj))
                {
                    if (morningShiftObj is Dictionary<string, object> morningShift)
                    {
                        if (morningShift.TryGetValue("assigned_employees", out var morningEmployees) && morningEmployees is List<object> morningEmpList)
                        {
                            groupModel.MorningShiftEmployees = ConvertToEmployeeDisplayModels(morningEmpList, groupName);
                            _logger.LogInformation("Found {Count} morning shift employees for group {GroupName}", morningEmpList.Count, groupName);
                        }
                    }
                    else if (morningShiftObj is JObject morningShiftJObject)
                    {
                        var morningShiftDict = ConvertJObjectToDictionary(morningShiftJObject);
                        if (morningShiftDict.TryGetValue("assigned_employees", out var morningEmployees) && morningEmployees is List<object> morningEmpList)
                        {
                            groupModel.MorningShiftEmployees = ConvertToEmployeeDisplayModels(morningEmpList, groupName);
                            _logger.LogInformation("Found {Count} morning shift employees for group {GroupName}", morningEmpList.Count, groupName);
                        }
                    }
                }
                
                // Get evening shift employees
                if (groupData.TryGetValue("evening_shift", out var eveningShiftObj))
                {
                    if (eveningShiftObj is Dictionary<string, object> eveningShift)
                    {
                        if (eveningShift.TryGetValue("assigned_employees", out var eveningEmployees) && eveningEmployees is List<object> eveningEmpList)
                        {
                            groupModel.EveningShiftEmployees = ConvertToEmployeeDisplayModels(eveningEmpList, groupName);
                            _logger.LogInformation("Found {Count} evening shift employees for group {GroupName}", eveningEmpList.Count, groupName);
                        }
                    }
                    else if (eveningShiftObj is JObject eveningShiftJObject)
                    {
                        var eveningShiftDict = ConvertJObjectToDictionary(eveningShiftJObject);
                        if (eveningShiftDict.TryGetValue("assigned_employees", out var eveningEmployees) && eveningEmployees is List<object> eveningEmpList)
                        {
                            groupModel.EveningShiftEmployees = ConvertToEmployeeDisplayModels(eveningEmpList, groupName);
                            _logger.LogInformation("Found {Count} evening shift employees for group {GroupName}", eveningEmpList.Count, groupName);
                        }
                    }
                }
                
                return groupModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group display model");
                return null;
            }
        }

        private DisplayApp.Models.GroupDisplayModel? CreateGroupDisplayModelFromParsedData(Dictionary<string, object> groupData, string groupId, Dictionary<string, object> reportData)
        {
            try
            {
                var groupName = groupData.GetValueOrDefault("Name", "گروه نامشخص")?.ToString() ?? "گروه نامشخص";
                var groupDescription = groupData.GetValueOrDefault("Description", "")?.ToString() ?? "";
                
                _logger.LogInformation("Creating group display model from parsed data for: {GroupName} (ID: {GroupId})", groupName, groupId);
                _logger.LogInformation("Group data keys: {Keys}", string.Join(", ", groupData.Keys));
                
                var groupModel = new DisplayApp.Models.GroupDisplayModel
                {
                    GroupName = groupName,
                    GroupDescription = groupDescription
                };
                
                // Parse morning shift from JSON string
                if (groupData.TryGetValue("MorningShift", out var morningShiftObj))
                {
                    // Handle both string and JsonElement types
                    string? morningShiftJson = null;
                    if (morningShiftObj is string str)
                    {
                        morningShiftJson = str;
                    }
                    else if (morningShiftObj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        morningShiftJson = jsonElement.GetString();
                    }
                    
                    if (!string.IsNullOrEmpty(morningShiftJson))
                    {
                        _logger.LogInformation("Found MorningShift JSON string for group {GroupName}: {Length} characters", groupName, morningShiftJson.Length);
                        try
                        {
                        // The JSON is double-escaped, so we need to unescape it first
                        var unescapedJson = morningShiftJson.Replace("\\\"", "\"").Replace("\\r\\n", "").Replace("\\n", "");
                        _logger.LogInformation("Unescaped morning shift JSON for group {GroupName}: {Length} characters", groupName, unescapedJson.Length);
                        _logger.LogInformation("Unescaped morning shift JSON content: {Json}", unescapedJson);
                        
                        var morningShiftData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(unescapedJson);
                        if (morningShiftData != null)
                        {
                            _logger.LogInformation("Successfully parsed morning shift data for group {GroupName}, keys: {Keys}", groupName, string.Join(", ", morningShiftData.Keys));
                            
                            if (morningShiftData.TryGetValue("AssignedEmployeeIds", out var assignedIds))
                            {
                                List<object> assignedIdsList = new List<object>();
                                
                                // Handle both List<object> and JsonElement array types
                                if (assignedIds is List<object> list)
                                {
                                    assignedIdsList = list;
                                }
                                else if (assignedIds is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var item in jsonElement.EnumerateArray())
                                    {
                                        if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                                        {
                                            assignedIdsList.Add(item.GetString() ?? "");
                                        }
                                        else if (item.ValueKind == System.Text.Json.JsonValueKind.Null)
                                        {
                                            assignedIdsList.Add(null!);
                                        }
                                    }
                                }
                                
                                _logger.LogInformation("Found {Count} assigned employee IDs for morning shift in group {GroupName}", assignedIdsList.Count, groupName);
                                
                                // Filter out null values from the assigned IDs
                                var validIds = assignedIdsList.Where(id => id != null && !string.IsNullOrEmpty(id.ToString())).ToList();
                                _logger.LogInformation("Found {Count} valid employee IDs for morning shift in group {GroupName}", validIds.Count, groupName);
                                
                                // Convert employee IDs to employee objects
                                var morningEmployees = GetEmployeesByIds(validIds, reportData);
                                groupModel.MorningShiftEmployees = ConvertToEmployeeDisplayModels(morningEmployees, groupName);
                                _logger.LogInformation("Found {Count} morning shift employees for group {GroupName}", morningEmployees.Count, groupName);
                            }
                            else
                            {
                                _logger.LogWarning("No AssignedEmployeeIds found in morning shift data for group {GroupName}", groupName);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize morning shift JSON for group {GroupName}", groupName);
                        }
                    }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse morning shift data for group {GroupName}", groupName);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No morning shift found in group data for group {GroupName}", groupName);
                }
                
                // Parse evening shift from JSON string
                if (groupData.TryGetValue("EveningShift", out var eveningShiftObj))
                {
                    // Handle both string and JsonElement types
                    string? eveningShiftJson = null;
                    if (eveningShiftObj is string str)
                    {
                        eveningShiftJson = str;
                    }
                    else if (eveningShiftObj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        eveningShiftJson = jsonElement.GetString();
                    }
                    
                    if (!string.IsNullOrEmpty(eveningShiftJson))
                    {
                        _logger.LogInformation("Found EveningShift JSON string for group {GroupName}: {Length} characters", groupName, eveningShiftJson.Length);
                        try
                        {
                        // The JSON is double-escaped, so we need to unescape it first
                        var unescapedJson = eveningShiftJson.Replace("\\\"", "\"").Replace("\\r\\n", "").Replace("\\n", "");
                        _logger.LogInformation("Unescaped evening shift JSON for group {GroupName}: {Length} characters", groupName, unescapedJson.Length);
                        _logger.LogInformation("Unescaped evening shift JSON content: {Json}", unescapedJson);
                        
                        var eveningShiftData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(unescapedJson);
                        if (eveningShiftData != null)
                        {
                            _logger.LogInformation("Successfully parsed evening shift data for group {GroupName}, keys: {Keys}", groupName, string.Join(", ", eveningShiftData.Keys));
                            
                            if (eveningShiftData.TryGetValue("AssignedEmployeeIds", out var assignedIds))
                            {
                                List<object> assignedIdsList = new List<object>();
                                
                                // Handle both List<object> and JsonElement array types
                                if (assignedIds is List<object> list)
                                {
                                    assignedIdsList = list;
                                }
                                else if (assignedIds is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var item in jsonElement.EnumerateArray())
                                    {
                                        if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                                        {
                                            assignedIdsList.Add(item.GetString() ?? "");
                                        }
                                        else if (item.ValueKind == System.Text.Json.JsonValueKind.Null)
                                        {
                                            assignedIdsList.Add(null!);
                                        }
                                    }
                                }
                                
                                _logger.LogInformation("Found {Count} assigned employee IDs for evening shift in group {GroupName}", assignedIdsList.Count, groupName);
                                
                                // Filter out null values from the assigned IDs
                                var validIds = assignedIdsList.Where(id => id != null && !string.IsNullOrEmpty(id.ToString())).ToList();
                                _logger.LogInformation("Found {Count} valid employee IDs for evening shift in group {GroupName}", validIds.Count, groupName);
                                
                                // Convert employee IDs to employee objects
                                var eveningEmployees = GetEmployeesByIds(validIds, reportData);
                                groupModel.EveningShiftEmployees = ConvertToEmployeeDisplayModels(eveningEmployees, groupName);
                                _logger.LogInformation("Found {Count} evening shift employees for group {GroupName}", eveningEmployees.Count, groupName);
                            }
                            else
                            {
                                _logger.LogWarning("No AssignedEmployeeIds found in evening shift data for group {GroupName}", groupName);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize evening shift JSON for group {GroupName}", groupName);
                        }
                    }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse evening shift data for group {GroupName}", groupName);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No evening shift found in group data for group {GroupName}", groupName);
                }
                
                return groupModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group display model from parsed data");
                return null;
            }
        }

        private List<object> GetEmployeesByIds(List<object> employeeIds, Dictionary<string, object> reportData)
        {
            var employees = new List<object>();
            
            try
            {
                _logger.LogInformation("Looking for {Count} employee IDs", employeeIds.Count);
                
                if (reportData.TryGetValue("employees", out var employeesObj) && employeesObj is List<object> allEmployees)
                {
                    _logger.LogInformation("Found {Count} total employees in report data", allEmployees.Count);
                    
                    foreach (var employeeId in employeeIds)
                    {
                        if (employeeId != null && !string.IsNullOrEmpty(employeeId.ToString()))
                        {
                            _logger.LogInformation("Looking for employee ID: {EmployeeId}", employeeId);
                            
                            var employee = allEmployees.FirstOrDefault(e => 
                                e is Dictionary<string, object> emp && 
                                emp.GetValueOrDefault("employee_id", "").ToString() == employeeId.ToString());
                            if (employee != null)
                            {
                                employees.Add(employee);
                                _logger.LogInformation("Found employee: {EmployeeId}", employeeId);
                            }
                            else
                            {
                                _logger.LogWarning("Employee not found: {EmployeeId}", employeeId);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Skipping null or empty employee ID");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No employees data found in report data");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employees by IDs");
            }
            
            _logger.LogInformation("Returning {Count} employees", employees.Count);
            return employees;
        }

        private List<EmployeeDisplayModel> ConvertToEmployeeDisplayModels(List<object> employeeObjects, string groupName = "")
        {
            var employeeModels = new List<EmployeeDisplayModel>();
            
            try
            {
                foreach (var employeeObj in employeeObjects)
                {
                    if (employeeObj is Dictionary<string, object> employeeDict)
                    {
                        var employeeModel = new EmployeeDisplayModel
                        {
                            EmployeeId = employeeDict.GetValueOrDefault("employee_id", "").ToString() ?? "",
                            FirstName = employeeDict.GetValueOrDefault("first_name", "").ToString() ?? "",
                            LastName = employeeDict.GetValueOrDefault("last_name", "").ToString() ?? "",
                            PhotoPath = employeeDict.GetValueOrDefault("photo_path", "").ToString() ?? "",
                            Role = employeeDict.GetValueOrDefault("role", "").ToString() ?? "",
                            GroupName = groupName
                        };
                        employeeModels.Add(employeeModel);
                    }
                    else if (employeeObj is JObject employeeJObject)
                    {
                        var employeeDictFromJObject = ConvertJObjectToDictionary(employeeJObject);
                        var employeeModel = new EmployeeDisplayModel
                        {
                            EmployeeId = employeeDictFromJObject.GetValueOrDefault("employee_id", "").ToString() ?? "",
                            FirstName = employeeDictFromJObject.GetValueOrDefault("first_name", "").ToString() ?? "",
                            LastName = employeeDictFromJObject.GetValueOrDefault("last_name", "").ToString() ?? "",
                            PhotoPath = employeeDictFromJObject.GetValueOrDefault("photo_path", "").ToString() ?? "",
                            Role = employeeDictFromJObject.GetValueOrDefault("role", "").ToString() ?? "",
                            GroupName = groupName
                        };
                        employeeModels.Add(employeeModel);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting employee objects to display models");
            }
            
            return employeeModels;
        }


        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            _countdownTimer?.Stop();
            _syncManager?.Dispose();
            base.OnClosed(e);
        }
    }
}