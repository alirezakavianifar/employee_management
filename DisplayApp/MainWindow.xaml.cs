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
using System.Text.Json;

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
        
        // Config file watching
        private FileSystemWatcher _configWatcher;
        private Utils.ConfigHelper _configHelper;
        private string _configFilePath;
        private DispatcherTimer _configReloadTimer;
        
        // Dynamic UI sizing
        private double _badgeWidth = 55;
        private double _badgeHeight = 70;
        private double _fontSizeMultiplier = 1.0;
        private double _groupWidthScale = 1.0;
        
        // Caching last report data
        private Dictionary<string, object> _lastReportData;
        
        // Chart data
        public SeriesCollection SeriesCollection { get; set; }
        public string[] Labels { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            
            // Set initial managers section height based on default badge height (55 -> 70 ratio)
            ManagersScrollViewer.MaxHeight = _badgeHeight + 10;
            
            // Initialize logging
            _logger = LoggingService.CreateLogger<MainWindow>();
            _logger.LogInformation("DisplayApp MainWindow initialized");
            
            // Get display config path and initialize config helper
            _configFilePath = GetDisplayConfigPath();
            _configHelper = new Utils.ConfigHelper(_configFilePath);
            
            // Load and apply visual settings from config
            ApplyBackgroundColor();
            ApplyDisplayProfile();
            ApplyVisibilitySettings();
            
            // Setup config file watcher
            SetupConfigWatcher();
            
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
            
            // Sync language combo with current language
            var lang = ResourceBridge.Instance.CurrentLanguage;
            LanguageComboBox.SelectedItem = lang == LanguageConfigHelper.LanguageFa ? LanguagePersianItem : LanguageEnglishItem;
            
            ResourceBridge.Instance.PropertyChanged += ResourceBridge_PropertyChanged;
            
            _logger.LogInformation("DisplayApp started successfully");
        }

        private void ResourceBridge_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ResourceBridge.CurrentLanguage))
                return;
            Dispatcher.Invoke(RefreshCodeBehindText);
        }

        private void RefreshCodeBehindText()
        {
            LastUpdateText.Text = string.Format(ResourceManager.GetString("display_last_update", "Last update: {0}"), _lastUpdateTime.ToString("HH:mm:ss"));
            CountdownText.Text = string.Format(ResourceManager.GetString("display_next_update", "Next update: {0}"), _countdownSeconds);
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
                return;
            var sharedData = App.SharedDataDirectory;
            if (string.IsNullOrEmpty(sharedData))
                return;
            var lang = tag.Trim().ToLowerInvariant() == "fa" ? LanguageConfigHelper.LanguageFa : LanguageConfigHelper.LanguageEn;
            if (lang == ResourceBridge.Instance.CurrentLanguage)
                return;
            try
            {
                LanguageConfigHelper.SetCurrentLanguage(sharedData, lang);
                ResourceManager.LoadResourcesForLanguage(sharedData, lang);
                ResourceBridge.Instance.CurrentLanguage = lang;
                ResourceBridge.Instance.NotifyLanguageChanged();
                App.ApplyFlowDirection();
                RefreshCodeBehindText();
                _logger?.LogInformation("Language switched to {Lang}", lang);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error switching language");
                MessageBox.Show(ResourceManager.GetString("msg_error", "Error") + ": " + ex.Message,
                    ResourceManager.GetString("msg_error", "Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BadgeSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            
            // Re-calculate proportional height depending on slider value (55 -> 70 ratio approx)
            _badgeWidth = e.NewValue;
            _badgeHeight = e.NewValue * (70.0 / 55.0);
            
            // Elastically adjust the managers section height to fit exactly one row of badges
            ManagersScrollViewer.MaxHeight = _badgeHeight + 10;
            
            if (_lastReportData != null)
                UpdateUI(_lastReportData);
        }

        private void GroupSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            
            _groupWidthScale = e.NewValue;
            
            if (_lastReportData != null)
                UpdateUI(_lastReportData);
        }



        private string GetDisplayConfigPath()
        {
            // Use the shared helper so both apps point to the same config
            var canonicalPath = Shared.Utils.DisplayConfigPathHelper.GetDisplayConfigPath();

            // If the canonical file does not exist yet, try to migrate from any legacy locations
            if (!File.Exists(canonicalPath))
            {
                foreach (var legacyPath in Shared.Utils.DisplayConfigPathHelper.GetLegacyCandidatePaths())
                {
                    var fullLegacyPath = Path.GetFullPath(legacyPath);
                    if (File.Exists(fullLegacyPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(canonicalPath)!);
                            File.Copy(fullLegacyPath, canonicalPath, overwrite: true);
                            _logger.LogInformation("Migrated display config from legacy path {LegacyPath} to {CanonicalPath}", fullLegacyPath, canonicalPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to migrate display config from legacy path {LegacyPath}", fullLegacyPath);
                        }
                        break;
                    }
                }
            }

            _logger.LogInformation("Using display config path: {ConfigPath}", canonicalPath);
            return canonicalPath;
        }

        private void ApplyBackgroundColor()
        {
            try
            {
                // Reload config to get latest values
                _configHelper.LoadConfig();
                var backgroundColor = _configHelper.GetBackgroundColor();
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(backgroundColor);
                this.Background = new SolidColorBrush(color);
                _logger.LogInformation("Background color applied: {Color}", backgroundColor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying background color");
                // Use default color if there's an error
                this.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1a1a1a"));
            }
        }

        private void ApplyDisplayProfile()
        {
            try
            {
                var profile = _configHelper.GetDisplayProfile();
                _logger.LogInformation("Applying display profile: {Profile}", profile);

                switch (profile)
                {
                    case "UltraWide34":
                        _badgeWidth = 70;
                        _badgeHeight = 85;
                        _fontSizeMultiplier = 1.0;
                        break;
                    case "UltraWide38":
                        _badgeWidth = 80;
                        _badgeHeight = 100;
                        _fontSizeMultiplier = 1.15;
                        break;
                    case "UltraWide43":
                        _badgeWidth = 95;
                        _badgeHeight = 115;
                        _fontSizeMultiplier = 1.3;
                        break;
                    case "Standard":
                    default:
                        _badgeWidth = 55;
                        _badgeHeight = 70;
                        _fontSizeMultiplier = 1.0;
                        break;
                }

                _logger.LogInformation("Profile settings: Width={Width}, Height={Height}, FontScale={Scale}", 
                    _badgeWidth, _badgeHeight, _fontSizeMultiplier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying display profile");
            }
        }

        private void SetupConfigWatcher()
        {
            try
            {
                var configDirectory = Path.GetDirectoryName(_configFilePath);
                var configFileName = Path.GetFileName(_configFilePath);

                if (string.IsNullOrEmpty(configDirectory) || !Directory.Exists(configDirectory))
                {
                    _logger.LogWarning("Config directory does not exist: {ConfigDirectory}. File watching disabled.", configDirectory);
                    return;
                }

                _configWatcher = new FileSystemWatcher(configDirectory)
                {
                    Filter = configFileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                // Debounce timer to handle rapid file writes
                _configReloadTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500) // Wait 500ms after last change
                };
                _configReloadTimer.Tick += ConfigReloadTimer_Tick;

                _configWatcher.Changed += ConfigWatcher_Changed;
                _logger.LogInformation("Config file watcher initialized for: {ConfigPath}", _configFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up config file watcher");
            }
        }

        private void ConfigWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Restart the debounce timer
                _configReloadTimer.Stop();
                _configReloadTimer.Start();
                _logger.LogDebug("Config file change detected, waiting for write completion...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling config file change event");
            }
        }

        private void ConfigReloadTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                _configReloadTimer.Stop();
                _logger.LogInformation("Reloading config file and applying background color...");
                
                // Retry logic to handle file locking during write operations
                int retries = 3;
                int delay = 100; // milliseconds
                bool success = false;

                for (int i = 0; i < retries && !success; i++)
                {
                    try
                    {
                        // Use Dispatcher to update UI on the UI thread
                        Dispatcher.Invoke(() =>
                        {
                            ApplyBackgroundColor();
                            ApplyVisibilitySettings();
                        });
                        success = true;
                    }
                    catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
                    {
                        if (i < retries - 1)
                        {
                            _logger.LogDebug("Config file is locked, retrying in {Delay}ms... (attempt {Attempt}/{Retries})", delay, i + 1, retries);
                            System.Threading.Thread.Sleep(delay);
                            delay *= 2; // Exponential backoff
                        }
                        else
                        {
                            _logger.LogWarning("Config file is still locked after {Retries} attempts, will retry on next change", retries);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading config file");
            }
        }

        private void ApplyVisibilitySettings()
        {
            try
            {
                // Ensure we are working with the latest configuration
                _configHelper.LoadConfig();

                var showChart = _configHelper.GetShowPerformanceChart();
                var showAi = _configHelper.GetShowAiRecommendation();

                var chartVisibility = showChart ? Visibility.Visible : Visibility.Collapsed;
                ChartTitle.Visibility = chartVisibility;
                PerformanceChart.Visibility = chartVisibility;
                ChartFormulaText.Visibility = chartVisibility;

                var aiVisibility = showAi ? Visibility.Visible : Visibility.Collapsed;
                AIRecommendationTitle.Visibility = aiVisibility;
                AIRecommendation.Visibility = aiVisibility;

                _logger.LogInformation("Applied visibility settings. Chart: {Chart}, AI: {Ai}", showChart, showAi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying visibility settings");
            }
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
                Title = "Performance",
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
                    _lastReportData = reportData;
                    _logger.LogInformation("Report data loaded successfully. Keys: {Keys}", 
                        string.Join(", ", reportData.Keys));
                    
                    // Debug: Show data keys in status
                    // StatusText removed - no longer needed
                    
                    UpdateUI(reportData);
                    _lastUpdateTime = DateTime.Now;
                    LastUpdateText.Text = string.Format(ResourceManager.GetString("display_last_update", "Last update: {0}"), _lastUpdateTime.ToString("HH:mm:ss"));
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
                ShowErrorDialog("Error loading data", ex.Message);
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
                    
                    // Calculate row height based on badge height plus some margin for borders and padding
                    double rowHeight = _badgeHeight + 8; 
                    
                    // Create row definitions
                    for (int row = 0; row < totalRows; row++)
                    {
                        ManagersGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) }); 
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
            // Badge dimensions (same as regular employees)
            // Badge dimensions 
            double badgeWidth = _badgeWidth;
            double badgeHeight = _badgeHeight;
            double borderThickness = 1; // Thick blue border
            double largeRectHeight = badgeHeight * 2.0 / 3.0; // Top 2/3 for photo
            double smallRectHeight = badgeHeight / 3.0; // Bottom 1/3 for name
            
            // Create main badge container with blue border (same as regular employees)
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 100, 200)), // Blue border
                BorderThickness = new Thickness(borderThickness),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(1), // Reduced margin
                Width = badgeWidth,
                Height = badgeHeight,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Create grid to hold photo and name areas (two-section layout)
            var badgeGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            badgeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(largeRectHeight, GridUnitType.Pixel) });
            badgeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(smallRectHeight, GridUnitType.Pixel) });
            
            // Large rectangle (top 2/3) - Photo area
            var photoContainer = new Grid
            {
                Background = Brushes.White,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            
            // Manager photo - properly centered (same as regular employees)
            var image = new Image
            {
                Stretch = Stretch.UniformToFill, // Fill and crop to center
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0) // No margin to ensure proper centering
            };
            
            var managerPhotoPath = managerData.TryGetValue("photo_path", out var photoPath) ? photoPath?.ToString() : null;
            var resolvedManagerPhoto = Employee.ResolvePhotoPath(managerPhotoPath);
            if (!string.IsNullOrEmpty(resolvedManagerPhoto))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(resolvedManagerPhoto, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    image.Source = bitmap;
                }
                catch
                {
                    image.Source = CreateEmployeePlaceholderImage(largeRectHeight);
                }
            }
            else
            {
                image.Source = CreateEmployeePlaceholderImage(largeRectHeight);
            }
            
            photoContainer.Children.Add(image);

            // Phone overlay at bottom of photo (inside image area)
            var managerPhone = managerData.GetValueOrDefault("phone", "").ToString() ?? "";
            var managerShowPhone = ParseShowPhone(managerData, true);
            if (managerShowPhone && !string.IsNullOrWhiteSpace(managerPhone))
            {
                var phoneOverlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), // semi-transparent black
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Padding = new Thickness(2, 0, 2, 1)
                };

                var phoneTextBlock = new TextBlock
                {
                    Text = managerPhone,
                    Foreground = Brushes.White,
                    FontSize = Math.Max(7, largeRectHeight * 0.10) * _fontSizeMultiplier,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Tahoma"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.None
                };

                var viewBox = new Viewbox
                {
                    Child = phoneTextBlock,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxHeight = largeRectHeight * 0.15,
                    MaxWidth = badgeWidth - 4
                };

                phoneOverlay.Child = viewBox;
                photoContainer.Children.Add(phoneOverlay);
                Panel.SetZIndex(phoneOverlay, 80);
            }

            Grid.SetRow(photoContainer, 0);
            badgeGrid.Children.Add(photoContainer);
            
            // Small rectangle (bottom 1/3) - Name area (white background)
            var nameContainer = new Grid
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            
            // Get personnel ID and name
            var personnelId = managerData.GetValueOrDefault("personnel_id", "").ToString() ?? "";
            var firstName = managerData.GetValueOrDefault("first_name", "").ToString();
            var lastName = managerData.GetValueOrDefault("last_name", "").ToString();
            var fullName = $"{firstName} {lastName}".Trim();
            
            var fontSize = Math.Max(8, Math.Min(12, smallRectHeight * 0.25)) * _fontSizeMultiplier;
            var nameColor = new SolidColorBrush(Color.FromRgb(0, 100, 200)); // Same blue as employee cards
            
            // Build vertical StackPanel: name on top line, personnel ID strictly below
            var nameStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var nameText = new TextBlock
            {
                Text = fullName,
                Foreground = nameColor,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Tahoma"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
            nameStack.Children.Add(nameText);
            
            if (!string.IsNullOrEmpty(personnelId))
            {
                var idText = new TextBlock
                {
                    Text = personnelId,
                    Foreground = nameColor,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Tahoma"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap
                };
                nameStack.Children.Add(idText);
            }
            
            // Wrap in Viewbox so the stack always fits the name area, shrinking automatically
            var nameViewbox = new Viewbox
            {
                Child = nameStack,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = badgeWidth - 4,
                Height = smallRectHeight - 2
            };
            
            nameContainer.Children.Add(nameViewbox);
            Grid.SetRow(nameContainer, 1);
            badgeGrid.Children.Add(nameContainer);
            
            card.Child = badgeGrid;
            return card;
        }

        private BitmapImage CreatePlaceholderImage()
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.Gray, null, new System.Windows.Rect(0, 0, 40, 40));
                drawingContext.DrawText(
                    new FormattedText("Manager", 
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
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
            double minW = 160 * _groupWidthScale;
            double maxW = 220 * _groupWidthScale;
            
            var groupBorder = new Border
            {
                Style = Application.Current.FindResource("CardStyle") as Style,
                Margin = new Thickness(1), // Reduced margin
                MinWidth = minW, 
                MaxWidth = maxW,
                VerticalAlignment = VerticalAlignment.Top // Prevent stretching to fill height
            };
            
            var groupGrid = new Grid();
            groupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            groupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Changed from Star to Auto
            
            // Group header: head worker (photo + name) above group number/name; team can exist without head worker
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Padding = new Thickness(5, 2, 5, 2),
                CornerRadius = new CornerRadius(3)
            };
            
            var headerStackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // 1) Head worker (supervisor) photo and name above group number
            if (!string.IsNullOrEmpty(group.SupervisorName) || !string.IsNullOrEmpty(group.SupervisorPhotoPath))
            {
                var supervisorStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                // Always show an image (photo or placeholder) when supervisor row is visible
                var supervisorImage = new Image
                {
                    Width = 40,
                    Height = 40,
                    Stretch = Stretch.UniformToFill
                };
                var resolvedSupervisorPhoto = Employee.ResolvePhotoPath(group.SupervisorPhotoPath);
                if (!string.IsNullOrEmpty(resolvedSupervisorPhoto))
                {
                    try
                    {
                        supervisorImage.Source = new BitmapImage(new Uri(resolvedSupervisorPhoto, UriKind.Absolute));
                    }
                    catch
                    {
                        supervisorImage.Source = CreateEmployeePlaceholderImage(40);
                    }
                }
                else
                {
                    supervisorImage.Source = CreateEmployeePlaceholderImage(40);
                }
                supervisorStack.Children.Add(supervisorImage);
                var supervisorText = new TextBlock
                {
                    Text = !string.IsNullOrEmpty(group.SupervisorName)
                        ? string.Format(ResourceManager.GetString("display_supervisor", "Supervisor: {0}"), group.SupervisorName)
                        : ResourceManager.GetString("display_no_supervisor", "No Supervisor"),
                    Style = Application.Current.FindResource("BodyTextStyle") as Style,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                supervisorStack.Children.Add(supervisorText);
                headerStackPanel.Children.Add(supervisorStack);
            }
            else
            {
                var noSupervisorText = new TextBlock
                {
                    Text = ResourceManager.GetString("display_no_supervisor", "No Supervisor"),
                    Style = Application.Current.FindResource("BodyTextStyle") as Style,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    Margin = new Thickness(0, 0, 0, 2)
                };
                headerStackPanel.Children.Add(noSupervisorText);
            }
            
            // 2) Group name (group number) below head worker
            var groupNameText = new TextBlock
            {
                Text = group.GroupName,
                Style = Application.Current.FindResource("SubtitleTextStyle") as Style,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            };
            headerStackPanel.Children.Add(groupNameText);
            headerBorder.Child = headerStackPanel;
            Grid.SetRow(headerBorder, 0);
            groupGrid.Children.Add(headerBorder);
            
            // Shifts container (Vertical stack: Morning, Afternoon, Night)
            var shiftsGrid = new Grid
            {
                Margin = new Thickness(1)
            };
            
            // Convert group color to WPF format (add FF prefix for alpha if not present)
            var groupColor = group.Color ?? "#4CAF50";
            if (!groupColor.StartsWith("#"))
                groupColor = "#" + groupColor;
            // Ensure color has alpha channel (8 characters including #)
            // If color is 6 hex digits (#RRGGBB), add FF for full opacity (#FFRRGGBB)
            if (groupColor.Length == 7)
                groupColor = "#FF" + groupColor.Substring(1);
            // If color already has 8 hex digits, use as-is
            
            int currentRow = 0;

            // Morning Shift (Top)
            if (group.MorningCapacity > 0)
            {
                shiftsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Changed from Star to Auto
                var morningBorder = CreateShiftPanel(ResourceManager.GetString("shift_morning", "Morning"), group.MorningShiftEmployees, groupColor, group.MorningForemanName, group.MorningForemanPhotoPath, group.MorningShiftStatusCards);
                Grid.SetRow(morningBorder, currentRow);
                shiftsGrid.Children.Add(morningBorder);
                currentRow++;
            }

            // Afternoon Shift (Middle)
            if (group.AfternoonCapacity > 0)
            {
                shiftsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Changed from Star to Auto
                var afternoonBorder = CreateShiftPanel(ResourceManager.GetString("shift_afternoon", "Afternoon"), group.AfternoonShiftEmployees, groupColor, group.AfternoonForemanName, group.AfternoonForemanPhotoPath, group.AfternoonShiftStatusCards);
                Grid.SetRow(afternoonBorder, currentRow);
                shiftsGrid.Children.Add(afternoonBorder);
                currentRow++;
            }
            
            // Night Shift (Bottom)
            if (group.NightCapacity > 0)
            {
                shiftsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Changed from Star to Auto
                var nightBorder = CreateShiftPanel(ResourceManager.GetString("shift_night", "Night"), group.NightShiftEmployees, groupColor, group.NightForemanName, group.NightForemanPhotoPath, group.NightShiftStatusCards);
                Grid.SetRow(nightBorder, currentRow);
                shiftsGrid.Children.Add(nightBorder);
                currentRow++;
            }
            
            Grid.SetRow(shiftsGrid, 1);
            groupGrid.Children.Add(shiftsGrid);
            
            groupBorder.Child = groupGrid;
            return groupBorder;
        }
        
        private Border CreateShiftPanel(string shiftTitle, List<DisplayApp.Models.EmployeeDisplayModel> employees, string color, string foremanName = "", string foremanPhotoPath = "", List<DisplayApp.Models.StatusCardDisplayModel>? statusCards = null)
        {
            var shiftBorder = new Border
            {
                Style = Application.Current.FindResource("CardStyle") as Style,
                Margin = new Thickness(1), // Reduced margin
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                BorderThickness = new Thickness(1)
            };
            
            var shiftGrid = new Grid();
            shiftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shiftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Changed from Star to Auto
            
            // Shift title with foreman
            var titleStackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            
            var titleText = new TextBlock
            {
                Text = shiftTitle,
                Style = Application.Current.FindResource("SubtitleTextStyle") as Style,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            // Foreman info (Photo + Name)
            if (!string.IsNullOrEmpty(foremanName) || !string.IsNullOrEmpty(foremanPhotoPath))
            {
                var foremanStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                // Foreman photo
                var foremanImage = new Image
                {
                    Width = 45,
                    Height = 45,
                    Stretch = Stretch.UniformToFill,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var resolvedForemanPhoto = Employee.ResolvePhotoPath(foremanPhotoPath);
                if (!string.IsNullOrEmpty(resolvedForemanPhoto))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(resolvedForemanPhoto, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        foremanImage.Source = bitmap;
                    }
                    catch
                    {
                        foremanImage.Source = CreateEmployeePlaceholderImage(45);
                    }
                }
                else
                {
                    foremanImage.Source = CreateEmployeePlaceholderImage(45);
                }

                foremanStack.Children.Add(foremanImage);

                if (!string.IsNullOrEmpty(foremanName))
                {
                    var foremanText = new TextBlock
                    {
                        Text = string.Format(ResourceManager.GetString("display_foreman", "Foreman: {0}"), foremanName),
                        Style = Application.Current.FindResource("BodyTextStyle") as Style,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    foremanStack.Children.Add(foremanText);
                }
                
                titleStackPanel.Children.Add(foremanStack);
            }
            
            titleStackPanel.Children.Add(titleText);
            Grid.SetRow(titleStackPanel, 0);
            shiftGrid.Children.Add(titleStackPanel);
            
            // Employees scroll viewer
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 500
            };
            
            var itemsControl = new ItemsControl();
            
            // Set items panel to WrapPanel for smart, responsive layout
            var itemsPanel = new ItemsPanelTemplate();
            var wrapPanelFactory = new FrameworkElementFactory(typeof(WrapPanel));
            wrapPanelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            wrapPanelFactory.SetValue(WrapPanel.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            itemsPanel.VisualTree = wrapPanelFactory;
            itemsControl.ItemsPanel = itemsPanel;
            
            // Create employee cards programmatically
            foreach (var employee in employees)
            {
                // Convert EmployeeDisplayModel to dictionary format expected by CreateEmployeeCard
                var employeeData = new Dictionary<string, object>
                {
                    { "employee_id", employee.EmployeeId },
                    { "first_name", employee.FirstName },
                    { "last_name", employee.LastName },
                    { "photo_path", employee.PhotoPath },
                    { "role", employee.Role },
                    { "shield_color", employee.ShieldColor },
                    { "show_shield", employee.ShowShield },
                    { "phone", employee.Phone },
                    { "show_phone", employee.ShowPhone },
                    { "sticker_paths", employee.StickerPaths ?? new List<string>() },
                    { "medal_badge_path", employee.MedalBadgePath },
                    { "personnel_id", employee.PersonnelId },
                    { "labels", employee.Labels }
                };
                
                // Create card using the same method as other cards
                var employeeCard = CreateEmployeeCard(employeeData, "");
                itemsControl.Items.Add(employeeCard);
            }
            
            // Create status card cards (Phase 2 feature)
            if (statusCards != null && statusCards.Count > 0)
            {
                foreach (var statusCard in statusCards)
                {
                    var statusCardCard = CreateStatusCardCard(statusCard);
                    itemsControl.Items.Add(statusCardCard);
                }
            }
            
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
            
            // Badge dimensions
            // Badge dimensions
            double badgeWidth = _badgeWidth;
            double badgeHeight = _badgeHeight;
            double borderThickness = 1; // Thick border
            double largeRectHeight = badgeHeight * 2.0 / 3.0; // Top 2/3 for photo
            double smallRectHeight = badgeHeight / 3.0; // Bottom 1/3 for name

            var showShield = ParseShowShield(employeeData);
            var shieldColorName = employeeData.GetValueOrDefault("shield_color", "Blue")?.ToString() ?? "Blue";
            var borderBrush = showShield ? GetShieldColorBrush(shieldColorName) : new SolidColorBrush(Color.FromRgb(0, 100, 200));

            // Create main badge container
            var card = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(borderThickness),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(0),
                Margin = new Thickness(1), // Reduced margin
                Width = badgeWidth,
                Height = badgeHeight,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Create grid to hold photo and name areas
            var badgeGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            badgeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(largeRectHeight, GridUnitType.Pixel) });
            badgeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(smallRectHeight, GridUnitType.Pixel) });
            
            // Large rectangle (top 2/3) - Photo area
            var photoContainer = new Grid
            {
                Background = Brushes.White,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            
            // Employee photo - properly centered
            var image = new Image
            {
                Stretch = Stretch.UniformToFill, // Fill and crop to center
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0) // No margin to ensure proper centering
            };
            
            var rawPhotoPath = employeeData.TryGetValue("photo_path", out var photoPath) ? photoPath?.ToString() : null;
            var resolvedPhoto = Employee.ResolvePhotoPath(rawPhotoPath);
            if (!string.IsNullOrEmpty(resolvedPhoto))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(resolvedPhoto, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    image.Source = bitmap;
                }
                catch
                {
                    image.Source = CreateEmployeePlaceholderImage(largeRectHeight);
                }
            }
            else
            {
                image.Source = CreateEmployeePlaceholderImage(largeRectHeight);
            }
            
            photoContainer.Children.Add(image);

            // Phone overlay at bottom of photo (inside image area)
            var phone = employeeData.GetValueOrDefault("phone", "").ToString() ?? "";
            var showPhone = ParseShowPhone(employeeData, true);
            if (showPhone && !string.IsNullOrWhiteSpace(phone))
            {
                var phoneOverlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), // semi-transparent black
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Padding = new Thickness(2, 0, 2, 1)
                };

                var phoneTextBlock = new TextBlock
                {
                    Text = phone,
                    Foreground = Brushes.White,
                    FontSize = Math.Max(7, largeRectHeight * 0.10) * _fontSizeMultiplier,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Tahoma"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.None
                };

                var viewBox = new Viewbox
                {
                    Child = phoneTextBlock,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxHeight = largeRectHeight * 0.15,
                    MaxWidth = badgeWidth - 4
                };

                phoneOverlay.Child = viewBox;
                photoContainer.Children.Add(phoneOverlay);
                // Above the photo, but below stickers / medals / labels (which use higher Z-indices)
                Panel.SetZIndex(phoneOverlay, 80);
            }

            Grid.SetRow(photoContainer, 0);
            badgeGrid.Children.Add(photoContainer);
            
            // Small rectangle (bottom 1/3) - Name area (white background)
            var nameContainer = new Grid
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            
            // Employee name
            var firstName = employeeData.GetValueOrDefault("first_name", "").ToString();
            var lastName = employeeData.GetValueOrDefault("last_name", "").ToString();
            var fullName = $"{firstName} {lastName}".Trim();
            var personnelId = employeeData.GetValueOrDefault("personnel_id", "").ToString() ?? "";
            
            var fontSize = Math.Max(8, Math.Min(12, smallRectHeight * 0.25)) * _fontSizeMultiplier;
            var nameColor = showShield ? GetShieldColorBrush(shieldColorName) : new SolidColorBrush(Color.FromRgb(0, 100, 200));
            
            // Build a vertical StackPanel: name on top line, personnel ID on line below
            var nameStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var nameText = new TextBlock
            {
                Text = fullName,
                Foreground = nameColor,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Tahoma"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
            nameStack.Children.Add(nameText);
            
            if (!string.IsNullOrEmpty(personnelId))
            {
                var idText = new TextBlock
                {
                    Text = personnelId,
                    Foreground = nameColor,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Tahoma"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.NoWrap
                };
                nameStack.Children.Add(idText);
            }
            
            // Wrap in Viewbox so the stack always fits the name area, shrinking automatically
            var nameViewbox = new Viewbox
            {
                Child = nameStack,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = badgeWidth - 2,
                Height = smallRectHeight
            };
            
            nameContainer.Children.Add(nameViewbox);
            Grid.SetRow(nameContainer, 1);
            badgeGrid.Children.Add(nameContainer);
            
            // Add stickers on the right side of the photo (inside the image frame, displayed vertically)
            if (employeeData.TryGetValue("sticker_paths", out var stickerPathsObj) && stickerPathsObj != null)
            {
                List<string> stickerPaths = new List<string>();
                if (stickerPathsObj is List<object> stickerList)
                {
                    stickerPaths = stickerList.Select(s => s?.ToString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s) && File.Exists(s))
                        .ToList();
                }
                else if (stickerPathsObj is List<string> stringList)
                {
                    stickerPaths = stringList.Where(s => !string.IsNullOrEmpty(s) && File.Exists(s)).ToList();
                }
                
                if (stickerPaths.Count > 0)
                {
                    // Create a vertical StackPanel for stickers on the right side
                    var stickersPanel = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 2, 0), // Small margin from right edge
                        Background = Brushes.Transparent
                    };
                    
                    // Add each sticker image
                    double stickerSize = badgeWidth * 0.30; // Increased sticker size
                    foreach (var stickerPath in stickerPaths)
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(stickerPath, UriKind.Absolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            
                            var stickerImage = new Image
                            {
                                Source = bitmap,
                                Width = stickerSize,
                                Height = stickerSize,
                                Stretch = Stretch.Uniform,
                                Margin = new Thickness(0, 1, 0, 1), // Small vertical spacing between stickers
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            
                            stickersPanel.Children.Add(stickerImage);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to load sticker image from {StickerPath}", stickerPath);
                            // Continue with next sticker if one fails to load
                        }
                    }
                    
                    // Add stickers panel to photo container
                    photoContainer.Children.Add(stickersPanel);
                    Panel.SetZIndex(stickersPanel, 90); // Above photo
                }
            }
            
            // Add medal/badge above the image, on the right side, with sufficient spacing
            if (employeeData.TryGetValue("medal_badge_path", out var medalBadgePathObj) && 
                !string.IsNullOrEmpty(medalBadgePathObj?.ToString()))
            {
                var medalBadgePath = medalBadgePathObj.ToString();
                
                if (!string.IsNullOrEmpty(medalBadgePath) && File.Exists(medalBadgePath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(medalBadgePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        
                        // Medal/badge size
                        double medalSize = badgeWidth * 0.40; // Increased medal size
                        
                        var medalImage = new Image
                        {
                            Source = bitmap,
                            Width = medalSize,
                            Height = medalSize,
                            Stretch = Stretch.Uniform, // Maintain aspect ratio
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Top,
                            // Position: above the image, on the right side, with spacing from worker's face/body
                            // Top margin: small spacing from top edge
                            // Right margin: small spacing from right edge
                            Margin = new Thickness(0, largeRectHeight * 0.1, badgeWidth * 0.05, 0), // 10% from top, 5% from right
                            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5)
                        };
                        
                        // Add subtle shadow effect for better visibility
                        medalImage.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = Colors.Black,
                            Direction = 270,
                            ShadowDepth = 2,
                            BlurRadius = 3,
                            Opacity = 0.5
                        };
                        
                        // Add medal to photo container
                        photoContainer.Children.Add(medalImage);
                        // Set high z-index to ensure it's visible above photo and stickers
                        Panel.SetZIndex(medalImage, 95);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load medal/badge image from {MedalBadgePath}", medalBadgePath);
                        // Continue without medal if it fails to load
                    }
                }
            }
            
            // Process labels
            if (employeeData.TryGetValue("labels", out var labelsObj))
            {
                 List<Shared.Models.EmployeeLabel> labels = null;
                 
                 if (labelsObj is List<Shared.Models.EmployeeLabel> typedLabels)
                 {
                     labels = typedLabels;
                 }
                 else if (labelsObj is List<object> objList)
                 {
                     labels = new List<Shared.Models.EmployeeLabel>();
                     foreach (var obj in objList)
                     {
                         if (obj is Dictionary<string, object> labelDict)
                         {
                             labels.Add(Shared.Models.EmployeeLabel.FromDictionary(labelDict));
                         }
                         else if (obj is Newtonsoft.Json.Linq.JObject jObj)
                         {
                             var dict = ConvertJObjectToDictionary(jObj);
                             labels.Add(Shared.Models.EmployeeLabel.FromDictionary(dict));
                         }
                     }
                 }

                 if (labels != null && labels.Count > 0)
                 {
                     var labelsPanel = new StackPanel
                     {
                         Orientation = Orientation.Vertical,
                         HorizontalAlignment = HorizontalAlignment.Left,
                         VerticalAlignment = VerticalAlignment.Bottom,
                         Margin = new Thickness(2, 0, 0, 2),
                         Background = Brushes.Transparent,
                         MaxWidth = badgeWidth * 0.4
                     };

                     foreach (var label in labels)
                     {
                         var labelBorder = new Border
                         {
                             Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F2FD")),
                             BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#90CAF9")),
                             BorderThickness = new Thickness(1),
                             CornerRadius = new CornerRadius(2),
                             Padding = new Thickness(2, 0, 2, 0),
                             Margin = new Thickness(0, 1, 0, 1),
                             SnapsToDevicePixels = true
                         };

                         var labelText = new TextBlock
                         {
                             Text = label.Text,
                             FontFamily = new FontFamily("Tahoma"),
                             FontSize = 8,
                             Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2")),
                             TextAlignment = TextAlignment.Center,
                             TextWrapping = TextWrapping.NoWrap,
                             TextTrimming = TextTrimming.CharacterEllipsis
                         };

                         labelBorder.Child = labelText;
                         labelsPanel.Children.Add(labelBorder);
                     }

                     photoContainer.Children.Add(labelsPanel);
                     Panel.SetZIndex(labelsPanel, 85);
                 }
            }

            card.Child = badgeGrid;
            return card;
        }

        private BitmapImage CreateEmployeePlaceholderImage(double size = 50)
        {
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(Brushes.DarkGray, null, new System.Windows.Rect(0, 0, size, size));
                drawingContext.DrawText(
                    new FormattedText("Worker", 
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
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

        /// <summary>
        /// Creates a visual card for a status card that can be displayed in shift slots.
        /// Status cards are colored rectangles with text, used to indicate special slot states.
        /// </summary>
        private Border CreateStatusCardCard(DisplayApp.Models.StatusCardDisplayModel statusCard)
        {
            _logger.LogInformation("Creating status card: {StatusCardId} - {Name}", statusCard.StatusCardId, statusCard.Name);
            
            // Parse colors
            Color backgroundColor;
            Color textColor;
            
            try
            {
                var bgColorStr = statusCard.Color ?? "#FF5722";
                if (!bgColorStr.StartsWith("#")) bgColorStr = "#" + bgColorStr;
                if (bgColorStr.Length == 7) bgColorStr = "#FF" + bgColorStr.Substring(1);
                backgroundColor = (Color)ColorConverter.ConvertFromString(bgColorStr);
            }
            catch
            {
                backgroundColor = Color.FromRgb(255, 87, 34); // Default orange
            }
            
            try
            {
                var txtColorStr = statusCard.TextColor ?? "#FFFFFF";
                if (!txtColorStr.StartsWith("#")) txtColorStr = "#" + txtColorStr;
                if (txtColorStr.Length == 7) txtColorStr = "#FF" + txtColorStr.Substring(1);
                textColor = (Color)ColorConverter.ConvertFromString(txtColorStr);
            }
            catch
            {
                textColor = Colors.White;
            }
            
            // Badge dimensions (same as employee cards)
            // Badge dimensions
            double badgeWidth = _badgeWidth;
            double badgeHeight = _badgeHeight;
            double borderThickness = 4;
            
            // Create main card container
            var card = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                BorderBrush = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Max(0, backgroundColor.R - 40),
                    (byte)Math.Max(0, backgroundColor.G - 40),
                    (byte)Math.Max(0, backgroundColor.B - 40))),
                BorderThickness = new Thickness(borderThickness),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(3),
                Width = badgeWidth,
                Height = badgeHeight,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // Create centered text
            var nameText = new TextBlock
            {
                Text = statusCard.Name ?? "Status Card",
                Foreground = new SolidColorBrush(textColor),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Tahoma"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(5)
            };
            
            card.Child = nameText;
            return card;
        }

        private void UpdateAbsencePanel(Dictionary<string, object> reportData)
        {
            // Update absence counts and cards
            if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
            {
                LeaveCount.Text = GetAbsenceCount(absences, "Leave").ToString();
                SickCount.Text = GetAbsenceCount(absences, "Sick").ToString();
                AbsentCount.Text = GetAbsenceCount(absences, "Absent").ToString();
                
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
            
            // Process Leave category
            if (absences.TryGetValue("Leave", out var leaveObj) && leaveObj is List<object> leaveList)
            {
                _logger.LogInformation("Found {Count} leave absences", leaveList.Count);
                foreach (var absenceObj in leaveList)
                {
                    if (absenceObj is Dictionary<string, object> absenceData)
                    {
                        var absenceCard = CreateAbsenceCard(absenceData, "Leave");
                        LeavePanel.Items.Add(absenceCard);
                    }
                }
            }
            
            // Process Sick category
            if (absences.TryGetValue("Sick", out var sickObj) && sickObj is List<object> sickList)
            {
                _logger.LogInformation("Found {Count} sick absences", sickList.Count);
                foreach (var absenceObj in sickList)
                {
                    if (absenceObj is Dictionary<string, object> absenceData)
                    {
                        var absenceCard = CreateAbsenceCard(absenceData, "Sick");
                        SickPanel.Items.Add(absenceCard);
                    }
                }
            }
            
            // Process Absent category
            if (absences.TryGetValue("Absent", out var absentObj) && absentObj is List<object> absentList)
            {
                _logger.LogInformation("Found {Count} absent absences", absentList.Count);
                foreach (var absenceObj in absentList)
                {
                    if (absenceObj is Dictionary<string, object> absenceData)
                    {
                        var absenceCard = CreateAbsenceCard(absenceData, "Absent");
                        AbsentPanel.Items.Add(absenceCard);
                    }
                }
            }
        }
        
        private Border CreateAbsenceCard(Dictionary<string, object> absenceData, string category)
        {
            _logger.LogInformation("Creating absence card for category: {Category}, keys: {Keys}",
                category, string.Join(", ", absenceData.Keys));

            // Ensure all fields CreateEmployeeCard expects are present with sensible defaults
            var normalised = new Dictionary<string, object>(absenceData)
            {
                ["role"]             = absenceData.GetValueOrDefault("role", ""),
                ["shield_color"]     = absenceData.GetValueOrDefault("shield_color", "Blue"),
                ["show_shield"]      = absenceData.GetValueOrDefault("show_shield", false),
                ["show_phone"]       = absenceData.GetValueOrDefault("show_phone", true),
                ["sticker_paths"]    = absenceData.GetValueOrDefault("sticker_paths", new List<string>()),
                ["medal_badge_path"] = absenceData.GetValueOrDefault("medal_badge_path", ""),
                ["labels"]           = absenceData.GetValueOrDefault("labels", new List<object>()),
                ["personnel_id"]     = absenceData.GetValueOrDefault("personnel_id", ""),
            };

            // Reuse the exact same badge as the employee section
            return CreateEmployeeCard(normalised, "");
        }
        
        private int GetAbsenceCount(Dictionary<string, object> absences, string category)
        {
            // Try English key first, then legacy Persian for backward compatibility
            if (absences.TryGetValue(category, out var obj) && obj is List<object> list)
                return list.Count;
            var legacyKey = category == "Leave" ? "Leave" : category == "Sick" ? "Sick" : "Absent";
            if (absences.TryGetValue(legacyKey, out var legacyObj) && legacyObj is List<object> legacyList)
                return legacyList.Count;
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
                AIRecommendation.Text = "Error getting recommendation";
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
                        ChartTitle.Text = "Performance - No data available";
                        _logger.LogInformation("Chart has no data - no employees found");
                    }
                    else if (dataPoints == 1)
                    {
                        ChartTitle.Text = "Today's Performance";
                    }
                    else
                    {
                        ChartTitle.Text = $"Performance - Last {dataPoints} days";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating chart");
                ChartTitle.Text = "Performance - Error loading";
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
            CountdownText.Text = string.Format(ResourceManager.GetString("display_next_update", "Next update: {0}"), _countdownSeconds);
            
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


        private Dictionary<string, DisplayApp.Models.StatusCardDisplayModel> BuildStatusCardsLookup(Dictionary<string, object> reportData)
        {
            var lookup = new Dictionary<string, DisplayApp.Models.StatusCardDisplayModel>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!reportData.TryGetValue("status_cards", out var statusCardsObj))
                    return lookup;
                if (statusCardsObj is not Dictionary<string, object> statusCardsDict)
                    return lookup;
                foreach (var kvp in statusCardsDict)
                {
                    var cardId = kvp.Key;
                    Dictionary<string, object>? cardDict = null;
                    if (kvp.Value is Dictionary<string, object> d)
                        cardDict = d;
                    else if (kvp.Value is JObject jo)
                        cardDict = ConvertJObjectToDictionary(jo);
                    if (cardDict != null)
                    {
                        var name = cardDict.GetValueOrDefault("name", "")?.ToString() ?? "";
                        var color = cardDict.GetValueOrDefault("color", "#FF5722")?.ToString() ?? "#FF5722";
                        var textColor = cardDict.GetValueOrDefault("text_color", "#FFFFFF")?.ToString() ?? "#FFFFFF";
                        lookup[cardId] = new DisplayApp.Models.StatusCardDisplayModel
                        {
                            StatusCardId = cardId,
                            Name = name,
                            Color = color,
                            TextColor = textColor,
                            SlotIndex = 0
                        };
                    }
                }
                _logger.LogInformation("Built status cards lookup with {Count} cards", lookup.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error building status cards lookup");
            }
            return lookup;
        }

        private List<DisplayApp.Models.GroupDisplayModel> GetAllGroupsData(Dictionary<string, object> reportData)
        {
            var groups = new List<DisplayApp.Models.GroupDisplayModel>();
            var statusCardsLookup = BuildStatusCardsLookup(reportData);
            
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
                                            var groupModel = CreateGroupDisplayModelFromParsedData(groupData, groupId, reportData, statusCardsLookup);
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
                                var groupModel = CreateGroupDisplayModel(groupData, statusCardsLookup, reportData);
                                if (groupModel != null)
                                {
                                    groups.Add(groupModel);
                                }
                            }
                            else if (groupObj is JObject groupJObject)
                            {
                                var groupDict = ConvertJObjectToDictionary(groupJObject);
                                var groupModel = CreateGroupDisplayModel(groupDict, statusCardsLookup, reportData);
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
                                var groupModel = CreateGroupDisplayModel(groupDict, statusCardsLookup, reportData);
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
                            var groupModel = CreateGroupDisplayModel(selectedGroupData, statusCardsLookup, reportData);
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
                                GroupName = "Default Group",
                                GroupDescription = "System default group",
                                Color = "#4CAF50" // Default green color
                            };
                            
                            // Get morning shift employees
                            if (shifts.TryGetValue("morning", out var morningShiftObj) && morningShiftObj is Dictionary<string, object> morningShift)
                            {
                                if (morningShift.TryGetValue("assigned_employees", out var morningEmployees) && morningEmployees is List<object> morningEmpList)
                                {
                                    defaultGroup.MorningShiftEmployees = ConvertToEmployeeDisplayModels(morningEmpList, defaultGroup.GroupName);
                                }
                            }
                            
                            // Get afternoon shift employees
                            if (shifts.TryGetValue("afternoon", out var afternoonShiftObj) && afternoonShiftObj is Dictionary<string, object> afternoonShift)
                            {
                                if (afternoonShift.TryGetValue("assigned_employees", out var afternoonEmployees) && afternoonEmployees is List<object> afternoonEmpList)
                                {
                                    defaultGroup.AfternoonShiftEmployees = ConvertToEmployeeDisplayModels(afternoonEmpList, defaultGroup.GroupName);
                                }
                            }

                            // Get evening shift employees (legacy - map to afternoon)
                            if (shifts.TryGetValue("evening", out var eveningShiftObj) && eveningShiftObj is Dictionary<string, object> eveningShift)
                            {
                                if (eveningShift.TryGetValue("assigned_employees", out var eveningEmployees) && eveningEmployees is List<object> eveningEmpList)
                                {
                                    // If afternoon is empty, use evening
                                    if (defaultGroup.AfternoonShiftEmployees == null || defaultGroup.AfternoonShiftEmployees.Count == 0)
                                    {
                                        defaultGroup.AfternoonShiftEmployees = ConvertToEmployeeDisplayModels(eveningEmpList, defaultGroup.GroupName);
                                    }
                                }
                            }

                            // Get night shift employees
                            if (shifts.TryGetValue("night", out var nightShiftObj) && nightShiftObj is Dictionary<string, object> nightShift)
                            {
                                if (nightShift.TryGetValue("assigned_employees", out var nightEmployees) && nightEmployees is List<object> nightEmpList)
                                {
                                    defaultGroup.NightShiftEmployees = ConvertToEmployeeDisplayModels(nightEmpList, defaultGroup.GroupName);
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
                    DisplayTitle = "All Staff"
                };
                
                // Debug: Log details about each group
                foreach (var group in groups)
                {
                    _logger.LogInformation("Group '{GroupName}': {MorningCount} morning, {AfternoonCount} afternoon, {NightCount} night employees", 
                        group.GroupName, group.MorningShiftEmployees.Count, group.AfternoonShiftEmployees.Count, group.NightShiftEmployees.Count);
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
                
                // Combine all afternoon shift employees
                foreach (var group in groups)
                {
                    _logger.LogInformation("Processing {Count} afternoon employees from group '{GroupName}'", 
                        group.AfternoonShiftEmployees.Count, group.GroupName);
                    foreach (var employee in group.AfternoonShiftEmployees)
                    {
                        // Set the group name for each employee
                        employee.GroupName = group.GroupName;
                        combinedModel.AllAfternoonShiftEmployees.Add(employee);
                        _logger.LogInformation("Added afternoon employee: {EmployeeName} from group {GroupName}", 
                            employee.FullName, group.GroupName);
                    }
                }
                
                // Combine all night shift employees
                foreach (var group in groups)
                {
                    _logger.LogInformation("Processing {Count} night employees from group '{GroupName}'", 
                        group.NightShiftEmployees.Count, group.GroupName);
                    foreach (var employee in group.NightShiftEmployees)
                    {
                        // Set the group name for each employee
                        employee.GroupName = group.GroupName;
                        combinedModel.AllNightShiftEmployees.Add(employee);
                        _logger.LogInformation("Added night employee: {EmployeeName} from group {GroupName}", 
                            employee.FullName, group.GroupName);
                    }
                }
                
                _logger.LogInformation("Combined {MorningCount} morning, {AfternoonCount} afternoon, {NightCount} night employees", 
                    combinedModel.AllMorningShiftEmployees.Count, 
                    combinedModel.AllAfternoonShiftEmployees.Count,
                    combinedModel.AllNightShiftEmployees.Count);
                
                return combinedModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating combined display model");
                return null;
            }
        }

        private void AddStatusCardFromShiftIfPresent(Dictionary<string, object> shiftDict, List<DisplayApp.Models.StatusCardDisplayModel> list, Dictionary<string, DisplayApp.Models.StatusCardDisplayModel> lookup, int slotIndex = 0)
        {
            if (lookup == null || lookup.Count == 0) return;
            if (!shiftDict.TryGetValue("StatusCardIds", out var idsObj) && !shiftDict.TryGetValue("status_card_ids", out idsObj))
                return;
            string? cardId = null;
            if (idsObj is List<object> idList && slotIndex < idList.Count && idList[slotIndex] != null)
                cardId = idList[slotIndex].ToString();
            else if (idsObj is JArray jArr && slotIndex < jArr.Count && jArr[slotIndex] != null)
                cardId = jArr[slotIndex]?.ToString();
            else if (idsObj is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var idx = 0;
                foreach (var item in je.EnumerateArray())
                {
                    if (idx == slotIndex)
                    {
                        if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                            cardId = item.GetString();
                        break;
                    }
                    idx++;
                }
            }
            if (string.IsNullOrEmpty(cardId) || !lookup.TryGetValue(cardId, out var card))
                return;
            list.Add(new DisplayApp.Models.StatusCardDisplayModel
            {
                StatusCardId = card.StatusCardId,
                Name = card.Name,
                Color = card.Color,
                TextColor = card.TextColor,
                SlotIndex = slotIndex
            });
        }

        private DisplayApp.Models.GroupDisplayModel? CreateGroupDisplayModel(Dictionary<string, object> groupData, Dictionary<string, DisplayApp.Models.StatusCardDisplayModel>? statusCardsLookup = null, Dictionary<string, object>? reportData = null)
        {
            try
            {
                var groupName = groupData.GetValueOrDefault("name", "Unknown Group")?.ToString() ?? "Unknown Group";
                var groupDescription = groupData.GetValueOrDefault("description", "")?.ToString() ?? "";
                var groupColor = groupData.GetValueOrDefault("color", "#4CAF50")?.ToString() ?? "#4CAF50";
                var supervisorName = groupData.GetValueOrDefault("supervisor_name", "")?.ToString() ?? "";
                var supervisorId = GetStringFromGroupData(groupData, "supervisor_id", "SupervisorId");
                
                _logger.LogInformation("Creating group display model for: {GroupName} with color: {Color}", groupName, groupColor);
                
                var groupModel = new DisplayApp.Models.GroupDisplayModel
                {
                    GroupName = groupName,
                    GroupDescription = groupDescription,
                    Color = groupColor,
                    SupervisorId = supervisorId ?? "",
                    SupervisorName = supervisorName,
                    MorningCapacity = GetIntFromGroupData(groupData, "morning_capacity", "MorningCapacity", 15),
                    AfternoonCapacity = GetIntFromGroupData(groupData, "afternoon_capacity", "AfternoonCapacity", 15),
                    NightCapacity = GetIntFromGroupData(groupData, "night_capacity", "NightCapacity", 15)
                };
                // Prefer supervisor_photo_path from group data (report); fall back to employee lookup
                groupModel.SupervisorPhotoPath = groupData.GetValueOrDefault("supervisor_photo_path", "")?.ToString() ?? groupData.GetValueOrDefault("SupervisorPhotoPath", "")?.ToString() ?? "";
                if (string.IsNullOrEmpty(groupModel.SupervisorPhotoPath) && reportData != null && !string.IsNullOrEmpty(supervisorId))
                {
                    var supervisorEmployees = GetEmployeesByIds(new List<object> { supervisorId! }, reportData);
                    if (supervisorEmployees.Count > 0 && supervisorEmployees[0] is Dictionary<string, object> supervisorDict)
                    {
                        groupModel.SupervisorPhotoPath = supervisorDict.GetValueOrDefault("photo_path", "")?.ToString() ?? supervisorDict.GetValueOrDefault("PhotoPath", "")?.ToString() ?? "";
                    }
                }
                
                // Get morning shift employees, foreman, and status cards
                if (groupData.TryGetValue("morning_shift", out var morningShiftObj))
                {
                    Dictionary<string, object>? morningShift = null;
                    if (morningShiftObj is Dictionary<string, object> morningShiftDict)
                    {
                        morningShift = morningShiftDict;
                    }
                    else if (morningShiftObj is JObject morningShiftJObject)
                    {
                        morningShift = ConvertJObjectToDictionary(morningShiftJObject);
                    }
                    
                    if (morningShift != null)
                    {
                        // Get employees
                        if (morningShift.TryGetValue("assigned_employees", out var morningEmployees) && morningEmployees is List<object> morningEmpList)
                        {
                            groupModel.MorningShiftEmployees = ConvertToEmployeeDisplayModels(morningEmpList, groupName);
                            _logger.LogInformation("Found {Count} morning shift employees for group {GroupName}", morningEmpList.Count, groupName);
                            
                            // Get foreman name from team_leader_id (check both snake_case and PascalCase)
                            object? morningForemanId = null;
                            if (morningShift.TryGetValue("team_leader_id", out morningForemanId) || 
                                morningShift.TryGetValue("TeamLeaderId", out morningForemanId))
                            {
                                if (morningForemanId != null && !string.IsNullOrEmpty(morningForemanId.ToString()))
                                {
                                    var foremanId = morningForemanId.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(foremanId))
                                    {
                                        groupModel.MorningForemanName = GetEmployeeNameById(foremanId, morningEmpList);
                                        groupModel.MorningForemanPhotoPath = GetEmployeePhotoPathById(foremanId, morningEmpList);
                                    }
                                }
                            }
                        }
                        if (statusCardsLookup != null)
                            AddStatusCardFromShiftIfPresent(morningShift, groupModel.MorningShiftStatusCards, statusCardsLookup);
                    }
                }
                
                // Get afternoon shift employees and foreman
                if (groupData.TryGetValue("afternoon_shift", out var afternoonShiftObj))
                {
                    Dictionary<string, object>? afternoonShift = null;
                    if (afternoonShiftObj is Dictionary<string, object> afternoonShiftDict)
                    {
                        afternoonShift = afternoonShiftDict;
                    }
                    else if (afternoonShiftObj is JObject afternoonShiftJObject)
                    {
                        afternoonShift = ConvertJObjectToDictionary(afternoonShiftJObject);
                    }
                    
                    if (afternoonShift != null)
                    {
                        // Get employees
                        if (afternoonShift.TryGetValue("assigned_employees", out var afternoonEmployees) && afternoonEmployees is List<object> afternoonEmpList)
                        {
                            groupModel.AfternoonShiftEmployees = ConvertToEmployeeDisplayModels(afternoonEmpList, groupName);
                            _logger.LogInformation("Found {Count} afternoon shift employees for group {GroupName}", afternoonEmpList.Count, groupName);
                            
                            // Get foreman name from team_leader_id
                            object? afternoonForemanId = null;
                            if (afternoonShift.TryGetValue("team_leader_id", out afternoonForemanId) || 
                                afternoonShift.TryGetValue("TeamLeaderId", out afternoonForemanId))
                            {
                                if (afternoonForemanId != null && !string.IsNullOrEmpty(afternoonForemanId.ToString()))
                                {
                                    var foremanId = afternoonForemanId.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(foremanId))
                                    {
                                        groupModel.AfternoonForemanName = GetEmployeeNameById(foremanId, afternoonEmpList);
                                        groupModel.AfternoonForemanPhotoPath = GetEmployeePhotoPathById(foremanId, afternoonEmpList);
                                    }
                                }
                            }
                        }
                        if (statusCardsLookup != null)
                            AddStatusCardFromShiftIfPresent(afternoonShift, groupModel.AfternoonShiftStatusCards, statusCardsLookup);
                    }
                }

                // Get night shift employees and foreman
                if (groupData.TryGetValue("night_shift", out var nightShiftObj))
                {
                    Dictionary<string, object>? nightShift = null;
                    if (nightShiftObj is Dictionary<string, object> nightShiftDict)
                    {
                        nightShift = nightShiftDict;
                    }
                    else if (nightShiftObj is JObject nightShiftJObject)
                    {
                        nightShift = ConvertJObjectToDictionary(nightShiftJObject);
                    }
                    
                    if (nightShift != null)
                    {
                        // Get employees
                        if (nightShift.TryGetValue("assigned_employees", out var nightEmployees) && nightEmployees is List<object> nightEmpList)
                        {
                            groupModel.NightShiftEmployees = ConvertToEmployeeDisplayModels(nightEmpList, groupName);
                            _logger.LogInformation("Found {Count} night shift employees for group {GroupName}", nightEmpList.Count, groupName);
                            
                            // Get foreman name from team_leader_id
                            object? nightForemanId = null;
                            if (nightShift.TryGetValue("team_leader_id", out nightForemanId) || 
                                nightShift.TryGetValue("TeamLeaderId", out nightForemanId))
                            {
                                if (nightForemanId != null && !string.IsNullOrEmpty(nightForemanId.ToString()))
                                {
                                    var foremanId = nightForemanId.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(foremanId))
                                    {
                                        groupModel.NightForemanName = GetEmployeeNameById(foremanId, nightEmpList);
                                        groupModel.NightForemanPhotoPath = GetEmployeePhotoPathById(foremanId, nightEmpList);
                                    }
                                }
                            }
                        }
                        if (statusCardsLookup != null)
                            AddStatusCardFromShiftIfPresent(nightShift, groupModel.NightShiftStatusCards, statusCardsLookup);
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

        private DisplayApp.Models.GroupDisplayModel? CreateGroupDisplayModelFromParsedData(Dictionary<string, object> groupData, string groupId, Dictionary<string, object> reportData, Dictionary<string, DisplayApp.Models.StatusCardDisplayModel>? statusCardsLookup = null)
        {
            try
            {
                var groupName = groupData.GetValueOrDefault("Name", "Unknown Group")?.ToString() ?? "Unknown Group";
                var groupDescription = groupData.GetValueOrDefault("Description", "")?.ToString() ?? "";
                var groupColor = groupData.GetValueOrDefault("Color", "#4CAF50")?.ToString() ?? "#4CAF50";
                var supervisorName = groupData.GetValueOrDefault("SupervisorName", "")?.ToString() ?? "";
                var supervisorId = GetStringFromGroupData(groupData, "SupervisorId", "supervisor_id");
                
                _logger.LogInformation("Creating group display model from parsed data for: {GroupName} (ID: {GroupId}) with color: {Color}", groupName, groupId, groupColor);
                _logger.LogInformation("Group data keys: {Keys}", string.Join(", ", groupData.Keys));
                
                var groupModel = new DisplayApp.Models.GroupDisplayModel
                {
                    GroupName = groupName,
                    GroupDescription = groupDescription,
                    Color = groupColor,
                    SupervisorId = supervisorId ?? "",
                    SupervisorName = supervisorName,
                    MorningCapacity = GetIntFromGroupData(groupData, "MorningCapacity", "morning_capacity", 15),
                    AfternoonCapacity = GetIntFromGroupData(groupData, "AfternoonCapacity", "afternoon_capacity", 15),
                    NightCapacity = GetIntFromGroupData(groupData, "NightCapacity", "night_capacity", 15)
                };
                // Prefer supervisor_photo_path from group data (report); fall back to employee lookup
                groupModel.SupervisorPhotoPath = groupData.GetValueOrDefault("SupervisorPhotoPath", "")?.ToString() ?? groupData.GetValueOrDefault("supervisor_photo_path", "")?.ToString() ?? "";
                if (string.IsNullOrEmpty(groupModel.SupervisorPhotoPath) && !string.IsNullOrEmpty(supervisorId))
                {
                    var supervisorEmployees = GetEmployeesByIds(new List<object> { supervisorId! }, reportData);
                    if (supervisorEmployees.Count > 0 && supervisorEmployees[0] is Dictionary<string, object> supervisorDict)
                    {
                        groupModel.SupervisorPhotoPath = supervisorDict.GetValueOrDefault("photo_path", "")?.ToString() ?? supervisorDict.GetValueOrDefault("PhotoPath", "")?.ToString() ?? "";
                    }
                }
                
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
                                
                                // Get foreman name from TeamLeaderId (check both PascalCase and snake_case)
                                object? morningForemanId = null;
                                if (morningShiftData.TryGetValue("TeamLeaderId", out morningForemanId) || 
                                    morningShiftData.TryGetValue("team_leader_id", out morningForemanId))
                                {
                                    if (morningForemanId != null && !string.IsNullOrEmpty(morningForemanId.ToString()))
                                    {
                                        var foremanEmployee = GetEmployeesByIds(new List<object> { morningForemanId }, reportData);
                                        if (foremanEmployee.Count > 0 && foremanEmployee[0] is Dictionary<string, object> foremanData)
                                        {
                                            var firstName = foremanData.GetValueOrDefault("first_name", "")?.ToString() ?? "";
                                            var lastName = foremanData.GetValueOrDefault("last_name", "")?.ToString() ?? "";
                                            groupModel.MorningForemanName = $"{firstName} {lastName}".Trim();
                                            groupModel.MorningForemanPhotoPath = foremanData.GetValueOrDefault("photo_path", "")?.ToString() ?? foremanData.GetValueOrDefault("PhotoPath", "")?.ToString() ?? "";
                                        }
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogWarning("No AssignedEmployeeIds found in morning shift data for group {GroupName}", groupName);
                            }
                            if (statusCardsLookup != null)
                                AddStatusCardFromShiftIfPresent(morningShiftData, groupModel.MorningShiftStatusCards, statusCardsLookup);
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
                
                // Parse afternoon shift from JSON string
                if (groupData.TryGetValue("AfternoonShift", out var afternoonShiftObj))
                {
                    // Handle both string and JsonElement types
                    string? afternoonShiftJson = null;
                    if (afternoonShiftObj is string str)
                    {
                        afternoonShiftJson = str;
                    }
                    else if (afternoonShiftObj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        afternoonShiftJson = jsonElement.GetString();
                    }
                    
                    if (!string.IsNullOrEmpty(afternoonShiftJson))
                    {
                        _logger.LogInformation("Found AfternoonShift JSON string for group {GroupName}: {Length} characters", groupName, afternoonShiftJson.Length);
                        try
                        {
                            var unescapedJson = afternoonShiftJson.Replace("\\\"", "\"").Replace("\\r\\n", "").Replace("\\n", "");
                            var afternoonShiftData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(unescapedJson);
                            if (afternoonShiftData != null)
                            {
                                if (afternoonShiftData.TryGetValue("AssignedEmployeeIds", out var assignedIds))
                                {
                                    List<object> assignedIdsList = new List<object>();
                                    if (assignedIds is List<object> list) assignedIdsList = list;
                                    else if (assignedIds is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var item in jsonElement.EnumerateArray())
                                        {
                                            if (item.ValueKind == System.Text.Json.JsonValueKind.String) assignedIdsList.Add(item.GetString() ?? "");
                                            else if (item.ValueKind == System.Text.Json.JsonValueKind.Null) assignedIdsList.Add(null!);
                                        }
                                    }
                                    
                                    var validIds = assignedIdsList.Where(id => id != null && !string.IsNullOrEmpty(id.ToString())).ToList();
                                    var afternoonEmployees = GetEmployeesByIds(validIds, reportData);
                                    groupModel.AfternoonShiftEmployees = ConvertToEmployeeDisplayModels(afternoonEmployees, groupName);
                                    
                                    object? foremanIdObj = null;
                                    if (afternoonShiftData.TryGetValue("TeamLeaderId", out foremanIdObj) || 
                                        afternoonShiftData.TryGetValue("team_leader_id", out foremanIdObj))
                                    {
                                        if (foremanIdObj != null && !string.IsNullOrEmpty(foremanIdObj.ToString()))
                                        {
                                            var foremanEmployee = GetEmployeesByIds(new List<object> { foremanIdObj }, reportData);
                                            if (foremanEmployee.Count > 0 && foremanEmployee[0] is Dictionary<string, object> foremanData)
                                            {
                                                var firstName = foremanData.GetValueOrDefault("first_name", "")?.ToString() ?? "";
                                                var lastName = foremanData.GetValueOrDefault("last_name", "")?.ToString() ?? "";
                                                groupModel.AfternoonForemanName = $"{firstName} {lastName}".Trim();
                                                groupModel.AfternoonForemanPhotoPath = foremanData.GetValueOrDefault("photo_path", "")?.ToString() ?? foremanData.GetValueOrDefault("PhotoPath", "")?.ToString() ?? "";
                                            }
                                        }
                                    }
                                }
                                if (statusCardsLookup != null)
                                    AddStatusCardFromShiftIfPresent(afternoonShiftData, groupModel.AfternoonShiftStatusCards, statusCardsLookup);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse afternoon shift data for group {GroupName}", groupName);
                        }
                    }
                }
                
                // Parse EveningShift as fallback for Afternoon
                if (groupData.TryGetValue("EveningShift", out var eveningShiftObj) && (groupModel.AfternoonShiftEmployees == null || groupModel.AfternoonShiftEmployees.Count == 0))
                {
                    string? eveningShiftJson = null;
                    if (eveningShiftObj is string str) eveningShiftJson = str;
                    else if (eveningShiftObj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.String) eveningShiftJson = jsonElement.GetString();
                    
                    if (!string.IsNullOrEmpty(eveningShiftJson))
                    {
                         try
                        {
                            var unescapedJson = eveningShiftJson.Replace("\\\"", "\"").Replace("\\r\\n", "").Replace("\\n", "");
                            var eveningShiftData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(unescapedJson);
                            if (eveningShiftData != null)
                            {
                                if (eveningShiftData.TryGetValue("AssignedEmployeeIds", out var assignedIds))
                                {
                                    List<object> assignedIdsList = new List<object>();
                                    if (assignedIds is List<object> list) assignedIdsList = list;
                                    else if (assignedIds is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var item in jsonElement.EnumerateArray())
                                        {
                                            if (item.ValueKind == System.Text.Json.JsonValueKind.String) assignedIdsList.Add(item.GetString() ?? "");
                                            else if (item.ValueKind == System.Text.Json.JsonValueKind.Null) assignedIdsList.Add(null!);
                                        }
                                    }
                                    var validIds = assignedIdsList.Where(id => id != null && !string.IsNullOrEmpty(id.ToString())).ToList();
                                    var eveningEmployees = GetEmployeesByIds(validIds, reportData);
                                    groupModel.AfternoonShiftEmployees = ConvertToEmployeeDisplayModels(eveningEmployees, groupName); // Map to Afternoon
                                    
                                     object? foremanIdObj = null;
                                    if (eveningShiftData.TryGetValue("TeamLeaderId", out foremanIdObj) || eveningShiftData.TryGetValue("team_leader_id", out foremanIdObj))
                                    {
                                        if (foremanIdObj != null && !string.IsNullOrEmpty(foremanIdObj.ToString()))
                                        {
                                             var foremanEmployee = GetEmployeesByIds(new List<object> { foremanIdObj }, reportData);
                                            if (foremanEmployee.Count > 0 && foremanEmployee[0] is Dictionary<string, object> foremanData)
                                            {
                                                var firstName = foremanData.GetValueOrDefault("first_name", "")?.ToString() ?? "";
                                                var lastName = foremanData.GetValueOrDefault("last_name", "")?.ToString() ?? "";
                                                groupModel.AfternoonForemanName = $"{firstName} {lastName}".Trim();
                                                groupModel.AfternoonForemanPhotoPath = foremanData.GetValueOrDefault("photo_path", "")?.ToString() ?? foremanData.GetValueOrDefault("PhotoPath", "")?.ToString() ?? "";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex) {_logger.LogWarning(ex, "Failed to parse evening shift data fallback");}
                    }
                }

                // Parse night shift from JSON string
                if (groupData.TryGetValue("NightShift", out var nightShiftObj))
                {
                    // Handle both string and JsonElement types
                    string? nightShiftJson = null;
                    if (nightShiftObj is string str)
                    {
                        nightShiftJson = str;
                    }
                    else if (nightShiftObj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        nightShiftJson = jsonElement.GetString();
                    }
                    
                    if (!string.IsNullOrEmpty(nightShiftJson))
                    {
                        _logger.LogInformation("Found NightShift JSON string for group {GroupName}: {Length} characters", groupName, nightShiftJson.Length);
                        try
                        {
                            var unescapedJson = nightShiftJson.Replace("\\\"", "\"").Replace("\\r\\n", "").Replace("\\n", "");
                            var nightShiftData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(unescapedJson);
                            if (nightShiftData != null)
                            {
                                if (nightShiftData.TryGetValue("AssignedEmployeeIds", out var assignedIds))
                                {
                                    List<object> assignedIdsList = new List<object>();
                                    if (assignedIds is List<object> list) assignedIdsList = list;
                                    else if (assignedIds is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var item in jsonElement.EnumerateArray())
                                        {
                                            if (item.ValueKind == System.Text.Json.JsonValueKind.String) assignedIdsList.Add(item.GetString() ?? "");
                                            else if (item.ValueKind == System.Text.Json.JsonValueKind.Null) assignedIdsList.Add(null!);
                                        }
                                    }
                                    
                                    var validIds = assignedIdsList.Where(id => id != null && !string.IsNullOrEmpty(id.ToString())).ToList();
                                    var nightEmployees = GetEmployeesByIds(validIds, reportData);
                                    groupModel.NightShiftEmployees = ConvertToEmployeeDisplayModels(nightEmployees, groupName);
                                    
                                    object? foremanIdObj = null;
                                    if (nightShiftData.TryGetValue("TeamLeaderId", out foremanIdObj) || 
                                        nightShiftData.TryGetValue("team_leader_id", out foremanIdObj))
                                    {
                                        if (foremanIdObj != null && !string.IsNullOrEmpty(foremanIdObj.ToString()))
                                        {
                                            var foremanEmployee = GetEmployeesByIds(new List<object> { foremanIdObj }, reportData);
                                            if (foremanEmployee.Count > 0 && foremanEmployee[0] is Dictionary<string, object> foremanData)
                                            {
                                                var firstName = foremanData.GetValueOrDefault("first_name", "")?.ToString() ?? "";
                                                var lastName = foremanData.GetValueOrDefault("last_name", "")?.ToString() ?? "";
                                                groupModel.NightForemanName = $"{firstName} {lastName}".Trim();
                                                groupModel.NightForemanPhotoPath = foremanData.GetValueOrDefault("photo_path", "")?.ToString() ?? foremanData.GetValueOrDefault("PhotoPath", "")?.ToString() ?? "";
                                            }
                                        }
                                    }
                                }
                                if (statusCardsLookup != null)
                                    AddStatusCardFromShiftIfPresent(nightShiftData, groupModel.NightShiftStatusCards, statusCardsLookup);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse night shift data for group {GroupName}", groupName);
                        }
                    }
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

        private string GetEmployeePhotoPathById(string employeeId, List<object> employeeList)
        {
            try
            {
                foreach (var empObj in employeeList)
                {
                    Dictionary<string, object>? empData = null;
                    if (empObj is Dictionary<string, object> empDict)
                    {
                        empData = empDict;
                    }
                    else if (empObj is JObject empJObject)
                    {
                        empData = ConvertJObjectToDictionary(empJObject);
                    }
                    
                    if (empData != null)
                    {
                        var empId = empData.GetValueOrDefault("employee_id", "")?.ToString() ?? "";
                        if (empId == employeeId)
                        {
                            return empData.GetValueOrDefault("photo_path", "")?.ToString() ?? empData.GetValueOrDefault("PhotoPath", "")?.ToString() ?? "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee photo path by ID: {EmployeeId}", employeeId);
            }
            return "";
        }

        private string GetEmployeeNameById(string employeeId, List<object> employeeList)
        {
            try
            {
                foreach (var empObj in employeeList)
                {
                    Dictionary<string, object>? empData = null;
                    if (empObj is Dictionary<string, object> empDict)
                    {
                        empData = empDict;
                    }
                    else if (empObj is JObject empJObject)
                    {
                        empData = ConvertJObjectToDictionary(empJObject);
                    }
                    
                    if (empData != null)
                    {
                        var empId = empData.GetValueOrDefault("employee_id", "")?.ToString() ?? "";
                        if (empId == employeeId)
                        {
                            var firstName = empData.GetValueOrDefault("first_name", "")?.ToString() ?? "";
                            var lastName = empData.GetValueOrDefault("last_name", "")?.ToString() ?? "";
                            return $"{firstName} {lastName}".Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting employee name by ID: {EmployeeId}", employeeId);
            }
            return "";
        }

        private static string? GetStringFromGroupData(Dictionary<string, object> groupData, string key1, string key2)
        {
            if (groupData.TryGetValue(key1, out var v1))
            {
                if (v1 is string s1 && !string.IsNullOrEmpty(s1)) return s1;
                if (v1 is System.Text.Json.JsonElement je1 && je1.ValueKind == System.Text.Json.JsonValueKind.String)
                    return je1.GetString();
                var t1 = v1?.ToString();
                if (!string.IsNullOrEmpty(t1)) return t1;
            }
            if (groupData.TryGetValue(key2, out var v2))
            {
                if (v2 is string s2 && !string.IsNullOrEmpty(s2)) return s2;
                if (v2 is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.String)
                    return je2.GetString();
                var t2 = v2?.ToString();
                if (!string.IsNullOrEmpty(t2)) return t2;
            }
            return null;
        }

        private static bool ParseFlag(Dictionary<string, object> dict, string key, bool defaultValue = true)
        {
            if (!dict.TryGetValue(key, out var obj)) return defaultValue;
            if (obj is bool b) return b;
            return bool.TryParse(obj?.ToString(), out var parsed) ? parsed : defaultValue;
        }

        private static bool ParseShowShield(Dictionary<string, object> dict, bool defaultValue = true)
        {
            return ParseFlag(dict, "show_shield", defaultValue);
        }

        private static bool ParseShowPhone(Dictionary<string, object> dict, bool defaultValue = true)
        {
            return ParseFlag(dict, "show_phone", defaultValue);
        }

        private static SolidColorBrush GetShieldColorBrush(string colorName)
        {
            var name = (colorName ?? "Blue").Trim();
            return name.ToUpperInvariant() switch
            {
                "RED" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")),
                "YELLOW" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f1c40f")),
                "BLACK" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000")),
                "ORANGE" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12")),
                "GRAY" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")),
                _ => new SolidColorBrush(Color.FromRgb(0, 100, 200)) // Blue (default)
            };
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
                        // Extract sticker paths
                        var stickerPaths = new List<string>();
                        if (employeeDict.TryGetValue("sticker_paths", out var stickerPathsObj))
                        {
                            if (stickerPathsObj is List<object> stickerList)
                            {
                                stickerPaths = stickerList.Select(s => s?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
                            }
                            else if (stickerPathsObj is Newtonsoft.Json.Linq.JArray jArray)
                            {
                                stickerPaths = jArray.ToObject<List<string>>() ?? new List<string>();
                            }
                        }
                        
                        // Extract labels
                        var labels = new List<Shared.Models.EmployeeLabel>();
                        if (employeeDict.TryGetValue("labels", out var labelsObj) && labelsObj is List<object> labelsList)
                        {
                            foreach (var item in labelsList)
                            {
                                if (item is Dictionary<string, object> labelDict)
                                {
                                    labels.Add(Shared.Models.EmployeeLabel.FromDictionary(labelDict));
                                }
                                else if (item is Newtonsoft.Json.Linq.JObject labelJObj)
                                {
                                     var dict = ConvertJObjectToDictionary(labelJObj);
                                     labels.Add(Shared.Models.EmployeeLabel.FromDictionary(dict));
                                }
                            }
                        }

                        var employeeModel = new EmployeeDisplayModel
                        {
                            EmployeeId = employeeDict.GetValueOrDefault("employee_id", "").ToString() ?? "",
                            FirstName = employeeDict.GetValueOrDefault("first_name", "").ToString() ?? "",
                            LastName = employeeDict.GetValueOrDefault("last_name", "").ToString() ?? "",
                            PhotoPath = employeeDict.GetValueOrDefault("photo_path", "").ToString() ?? "",
                            Role = employeeDict.GetValueOrDefault("role", "").ToString() ?? "",
                            GroupName = groupName,
                            ShieldColor = employeeDict.GetValueOrDefault("shield_color", "Blue").ToString() ?? "Blue",
                            ShowShield = ParseShowShield(employeeDict),
                            Phone = employeeDict.GetValueOrDefault("phone", "").ToString() ?? "",
                            ShowPhone = ParseShowPhone(employeeDict),
                            StickerPaths = stickerPaths,
                            MedalBadgePath = employeeDict.GetValueOrDefault("medal_badge_path", "").ToString() ?? "",
                            PersonnelId = employeeDict.GetValueOrDefault("personnel_id", "").ToString() ?? "",
                            Labels = labels
                        };
                        employeeModels.Add(employeeModel);
                    }
                    else if (employeeObj is JObject employeeJObject)
                    {
                        var employeeDictFromJObject = ConvertJObjectToDictionary(employeeJObject);
                        
                        // Extract sticker paths
                        var stickerPaths = new List<string>();
                        if (employeeDictFromJObject.TryGetValue("sticker_paths", out var stickerPathsObj))
                        {
                            if (stickerPathsObj is List<object> stickerList)
                            {
                                stickerPaths = stickerList.Select(s => s?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
                            }
                            else if (stickerPathsObj is Newtonsoft.Json.Linq.JArray jArray)
                            {
                                stickerPaths = jArray.ToObject<List<string>>() ?? new List<string>();
                            }
                        }
                        
                        // Extract labels
                        var labels = new List<Shared.Models.EmployeeLabel>();
                        if (employeeDictFromJObject.TryGetValue("labels", out var labelsObj2) && labelsObj2 is List<object> labelsList2)
                        {
                            foreach (var item in labelsList2)
                            {
                                if (item is Dictionary<string, object> labelDict)
                                {
                                    labels.Add(Shared.Models.EmployeeLabel.FromDictionary(labelDict));
                                }
                                else if (item is Newtonsoft.Json.Linq.JObject labelJObj)
                                {
                                     var dict = ConvertJObjectToDictionary(labelJObj);
                                     labels.Add(Shared.Models.EmployeeLabel.FromDictionary(dict));
                                }
                            }
                        }

                        var employeeModel = new EmployeeDisplayModel
                        {
                            EmployeeId = employeeDictFromJObject.GetValueOrDefault("employee_id", "").ToString() ?? "",
                            FirstName = employeeDictFromJObject.GetValueOrDefault("first_name", "").ToString() ?? "",
                            LastName = employeeDictFromJObject.GetValueOrDefault("last_name", "").ToString() ?? "",
                            PhotoPath = employeeDictFromJObject.GetValueOrDefault("photo_path", "").ToString() ?? "",
                            Role = employeeDictFromJObject.GetValueOrDefault("role", "").ToString() ?? "",
                            GroupName = groupName,
                            ShieldColor = employeeDictFromJObject.GetValueOrDefault("shield_color", "Blue").ToString() ?? "Blue",
                            ShowShield = ParseShowShield(employeeDictFromJObject),
                            Phone = employeeDictFromJObject.GetValueOrDefault("phone", "").ToString() ?? "",
                            ShowPhone = ParseShowPhone(employeeDictFromJObject),
                            StickerPaths = stickerPaths,
                            MedalBadgePath = employeeDictFromJObject.GetValueOrDefault("medal_badge_path", "").ToString() ?? "",
                            PersonnelId = employeeDictFromJObject.GetValueOrDefault("personnel_id", "").ToString() ?? "",
                            Labels = labels
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


        private int GetIntFromGroupData(Dictionary<string, object> data, string key, string altKey, int defaultValue)
        {
            if (data.TryGetValue(key, out var val) || data.TryGetValue(altKey, out val))
            {
                if (val is int i) return i;
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var jeInt)) return jeInt;
                if (int.TryParse(val?.ToString(), out var parsed)) return parsed;
            }
            return defaultValue;
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            _countdownTimer?.Stop();
            _configReloadTimer?.Stop();
            _configWatcher?.Dispose();
            _syncManager?.Dispose();
            base.OnClosed(e);
        }
    }
}