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
using Newtonsoft.Json.Linq;

namespace DisplayApp
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly DataService _dataService;
        private readonly ChartService _chartService;
        private readonly AIService _aiService;
        private readonly SyncManager _syncManager;
        
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
            // Use shared data directory directly
            _syncManager = new SyncManager(@"D:\projects\New folder (8)\SharedData");
            
            // Setup timers
            SetupTimers();
            
            // Setup sync callbacks
            _syncManager.AddSyncCallback(OnDataChanged);
            
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
            
            // Add sample data for now
            var values = new ChartValues<double> { 10, 15, 20, 18, 25, 30, 28 };
            SeriesCollection.Add(new LineSeries
            {
                Title = "عملکرد",
                Values = values,
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 8,
                Stroke = Brushes.LightBlue,
                Fill = Brushes.LightBlue,
                StrokeThickness = 2
            });
            
            Labels = new[] { "هفته 1", "هفته 2", "هفته 3", "هفته 4", "هفته 5", "هفته 6", "هفته 7" };
            
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
                
                if (managers != null)
                {
                    for (int i = 0; i < Math.Min(managers.Count, 3); i++)
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
                            Grid.SetColumn(managerCard, i);
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
                CornerRadius = new CornerRadius(8),
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
                Stretch = Stretch.UniformToFill
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
            _logger.LogInformation("Updating shifts panel");
            
            // Check if panels exist
            if (MorningShiftPanel == null)
            {
                _logger.LogError("MorningShiftPanel not found in UI");
                return;
            }
            if (EveningShiftPanel == null)
            {
                _logger.LogError("EveningShiftPanel not found in UI");
                return;
            }
            
            MorningShiftPanel.Items.Clear();
            EveningShiftPanel.Items.Clear();
            
            // Debug: Check if shifts key exists
            if (reportData.ContainsKey("shifts"))
            {
                _logger.LogInformation("Shifts key exists in report data");
                var shiftsObj = reportData["shifts"];
                _logger.LogInformation("Shifts object type: {Type}", shiftsObj?.GetType().Name ?? "null");
                
                // Handle both Dictionary and JObject types
                if (shiftsObj is Dictionary<string, object> shifts)
                {
                    _logger.LogInformation("Found shifts data with {Count} shift types", shifts.Count);
                    _logger.LogInformation("Shift keys: {Keys}", string.Join(", ", shifts.Keys));
                    
                    // Morning shift
                    if (shifts.TryGetValue("morning", out var morningShiftObj))
                    {
                        _logger.LogInformation("Morning shift object type: {Type}", morningShiftObj?.GetType().Name ?? "null");
                        if (morningShiftObj is Dictionary<string, object> morningShift)
                        {
                            _logger.LogInformation("Found morning shift data");
                            UpdateShiftPanel(MorningShiftPanel, morningShift, "صبح");
                        }
                        else
                        {
                            _logger.LogWarning("Morning shift data is not a Dictionary: {Type}", morningShiftObj?.GetType().Name ?? "null");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Morning shift key not found in shifts data");
                    }
                    
                    // Evening shift
                    if (shifts.TryGetValue("evening", out var eveningShiftObj))
                    {
                        _logger.LogInformation("Evening shift object type: {Type}", eveningShiftObj?.GetType().Name ?? "null");
                        if (eveningShiftObj is Dictionary<string, object> eveningShift)
                        {
                            _logger.LogInformation("Found evening shift data");
                            UpdateShiftPanel(EveningShiftPanel, eveningShift, "عصر");
                        }
                        else
                        {
                            _logger.LogWarning("Evening shift data is not a Dictionary: {Type}", eveningShiftObj?.GetType().Name ?? "null");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Evening shift key not found in shifts data");
                    }
                }
                else if (shiftsObj is JObject shiftsJObject)
                {
                    _logger.LogInformation("Found shifts data as JObject with {Count} shift types", shiftsJObject.Count);
                    _logger.LogInformation("Shift keys: {Keys}", string.Join(", ", shiftsJObject.Properties().Select(p => p.Name)));
                    
                    // Morning shift
                    if (shiftsJObject.TryGetValue("morning", out var morningShiftToken))
                    {
                        _logger.LogInformation("Morning shift token type: {Type}", morningShiftToken?.GetType().Name ?? "null");
                        if (morningShiftToken is JObject morningShiftJObject)
                        {
                            _logger.LogInformation("Found morning shift data as JObject");
                            var morningShift = ConvertJObjectToDictionary(morningShiftJObject);
                            UpdateShiftPanel(MorningShiftPanel, morningShift, "صبح");
                        }
                        else
                        {
                            _logger.LogWarning("Morning shift data is not a JObject: {Type}", morningShiftToken?.GetType().Name ?? "null");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Morning shift key not found in shifts JObject");
                    }
                    
                    // Evening shift
                    if (shiftsJObject.TryGetValue("evening", out var eveningShiftToken))
                    {
                        _logger.LogInformation("Evening shift token type: {Type}", eveningShiftToken?.GetType().Name ?? "null");
                        if (eveningShiftToken is JObject eveningShiftJObject)
                        {
                            _logger.LogInformation("Found evening shift data as JObject");
                            var eveningShift = ConvertJObjectToDictionary(eveningShiftJObject);
                            UpdateShiftPanel(EveningShiftPanel, eveningShift, "عصر");
                        }
                        else
                        {
                            _logger.LogWarning("Evening shift data is not a JObject: {Type}", eveningShiftToken?.GetType().Name ?? "null");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Evening shift key not found in shifts JObject");
                    }
                }
                else
                {
                    _logger.LogWarning("Shifts data is neither Dictionary nor JObject: {Type}", shiftsObj?.GetType().Name ?? "null");
                }
            }
            else
            {
                _logger.LogWarning("No shifts data found in report");
            }
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
            // Update absence counts
            if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
            {
                LeaveCount.Text = GetAbsenceCount(absences, "مرخصی").ToString();
                SickCount.Text = GetAbsenceCount(absences, "بیمار").ToString();
                AbsentCount.Text = GetAbsenceCount(absences, "غایب").ToString();
                
                // Update absence cards
                UpdateAbsenceCards(absences);
            }
        }
        
        private void UpdateAbsenceCards(Dictionary<string, object> absences)
        {
            _logger.LogInformation("Updating absence cards");
            
            // Check if panel exists
            if (AbsencePanel == null)
            {
                _logger.LogError("AbsencePanel not found in UI");
                return;
            }
            
            AbsencePanel.Items.Clear();
            
            // Process all absence categories
            foreach (var category in new[] { "مرخصی", "بیمار", "غایب" })
            {
                if (absences.TryGetValue(category, out var categoryObj) && categoryObj is List<object> categoryList)
                {
                    _logger.LogInformation("Found {Count} {Category} absences", categoryList.Count, category);
                    
                    foreach (var absenceObj in categoryList)
                    {
                        if (absenceObj is Dictionary<string, object> absenceData)
                        {
                            _logger.LogInformation("Creating absence card for {EmployeeId}", 
                                absenceData.GetValueOrDefault("employee_id", "Unknown"));
                            var absenceCard = CreateAbsenceCard(absenceData, category);
                            AbsencePanel.Items.Add(absenceCard);
                        }
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
            
            // Create the card (compact with reduced margins)
            var card = new Border
            {
                Width = 70,
                Height = 90,
                Background = GetAbsenceCategoryColor(category),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5),
                Margin = new Thickness(1),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            
            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Add image (larger for better visibility)
            var image = new Image
            {
                Width = 35,
                Height = 35,
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
                    image.Source = CreateEmployeePlaceholderImage(35);
                }
            }
            else
            {
                image.Source = CreateEmployeePlaceholderImage(35);
            }
            
            stackPanel.Children.Add(image);
            
            // Add name (compact margins)
            var nameText = new TextBlock
            {
                Text = fullName,
                Foreground = Brushes.White,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };
            stackPanel.Children.Add(nameText);
            
            // Add category label (compact margins)
            var categoryText = new TextBlock
            {
                Text = category,
                Foreground = Brushes.White,
                FontSize = 7,
                FontWeight = FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 0)
            };
            stackPanel.Children.Add(categoryText);
            
            card.Child = stackPanel;
            return card;
        }
        
        private Brush GetAbsenceCategoryColor(string category)
        {
            return category switch
            {
                "مرخصی" => new SolidColorBrush(Color.FromRgb(52, 152, 219)), // Blue
                "بیمار" => new SolidColorBrush(Color.FromRgb(231, 76, 60)),  // Red
                "غایب" => new SolidColorBrush(Color.FromRgb(241, 196, 15)),  // Yellow
                _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))       // Gray
            };
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
                    
                    // Update chart title with current week
                    var currentWeek = DateTime.Now.DayOfYear / 7 + 1;
                    ChartTitle.Text = $"افزایش عملکرد — هفتهٔ {currentWeek}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chart");
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

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            _countdownTimer?.Stop();
            _syncManager?.Dispose();
            base.OnClosed(e);
        }
    }
}