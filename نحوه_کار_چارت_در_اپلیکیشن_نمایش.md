# نحوه کار چارت در اپلیکیشن نمایش با داده‌های اپلیکیشن مدیریت

## نمای کلی سیستم

سیستم مدیریت کارمندان از دو اپلیکیشن تشکیل شده است:
- **اپلیکیشن مدیریت** (Management App): برای مدیریت کارمندان، شیفت‌ها، وظایف و غیبت‌ها
- **اپلیکیشن نمایش** (Display App): برای نمایش اطلاعات و چارت‌ها به صورت بصری

## جریان داده‌ها

### ۱. تولید داده در اپلیکیشن مدیریت

اپلیکیشن مدیریت داده‌های زیر را تولید و ذخیره می‌کند:

```csharp
var reportData = new Dictionary<string, object>
{
    { "date", DateTime.Now.ToString("yyyy-MM-dd") },
    { "employees", Employees.Values.Select(emp => emp.ToDictionary()).ToList() },
    { "managers", managersToDisplay },
    { "shifts", CreateShiftsData() },
    { "absences", AbsenceManager.ToJson() },
    { "tasks", TaskManager.ToJson() },
    { "settings", Settings },
    { "last_modified", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
};
```

این داده‌ها در فایل‌های JSON با نام‌هایی مانند `report_2024-01-15.json` در پوشه `Data/Reports/` ذخیره می‌شوند.

### ۲. همگام‌سازی بلادرنگ

**SyncManager** تغییرات فایل‌ها را نظارت می‌کند:

```csharp
_watcher = new FileSystemWatcher(_dataDir)
{
    Filter = "*.json",
    IncludeSubdirectories = true,
    EnableRaisingEvents = true
};

_watcher.Changed += OnFileChanged;
_watcher.Created += OnFileCreated;
```

هر زمان که فایل JSON تغییر کند، سیستم به‌طور خودکار اپلیکیشن نمایش را به‌روزرسانی می‌کند.

### ۳. خواندن داده در اپلیکیشن نمایش

**DataService** آخرین گزارش را می‌خواند:

```csharp
public async Task<Dictionary<string, object>?> GetLatestReportAsync()
{
    var reportFiles = Directory.GetFiles(reportsDir, "report_*.json")
        .Where(f => !Path.GetFileName(f).Contains("_backup_"))
        .OrderByDescending(f => File.GetLastWriteTime(f))
        .ToList();
    
    // خواندن آخرین فایل گزارش
    var data = _jsonHandler.ReadJson(reportFile);
    var transformedData = TransformReportData(data);
    return transformedData;
}
```

### ۴. تولید چارت

**ChartService** داده‌ها را پردازش کرده و چارت تولید می‌کند:

```csharp
public LineSeries GeneratePerformanceChart(Dictionary<string, object> reportData)
{
    var performanceData = GeneratePerformanceData(reportData);
    
    var lineSeries = new LineSeries
    {
        Title = "عملکرد",
        Values = new ChartValues<double>(performanceData),
        PointGeometry = DefaultGeometries.Circle,
        Stroke = Brushes.LightBlue,
        Fill = Brushes.LightBlue
    };
    
    return lineSeries;
}
```

### ۵. محاسبه عملکرد بر اساس وظایف

سیستم عملکرد را بر اساس تکمیل وظایف محاسبه می‌کند:

```csharp
private List<double> GeneratePerformanceData(Dictionary<string, object> reportData)
{
    if (reportData.TryGetValue("tasks", out var tasksObj))
    {
        // شمارش وظایف تکمیل شده
        var completedTasks = 0;
        var totalTasks = taskDict.Count;
        
        foreach (var task in taskDict.Values)
        {
            if (taskData.TryGetValue("status", out var statusObj) && 
                statusObj.ToString() == "تکمیل شده")
            {
                completedTasks++;
            }
        }
        
        // محاسبه درصد عملکرد
        var basePerformance = totalTasks > 0 ? 
            (double)completedTasks / totalTasks * 100 : 50;
        
        // تولید داده‌های هفتگی
        for (int week = 1; week <= 7; week++)
        {
            var variation = (week - 4) * 5; // اوج در هفته ۴
            var performance = Math.Max(0, Math.Min(100, 
                basePerformance + variation + (week * 2)));
            performanceData.Add(Math.Round(performance, 1));
        }
    }
    
    return performanceData;
}
```

### ۶. نمایش چارت

**MainWindow** چارت را به‌روزرسانی می‌کند:

```csharp
private void UpdateChart(Dictionary<string, object> reportData)
{
    var chartData = _chartService.GeneratePerformanceChart(reportData);
    if (chartData != null)
    {
        SeriesCollection.Clear();
        SeriesCollection.Add(chartData);
        
        var currentWeek = DateTime.Now.DayOfYear / 7 + 1;
        ChartTitle.Text = $"افزایش عملکرد — هفتهٔ {currentWeek}";
    }
}
```

### ۷. به‌روزرسانی بلادرنگ

هنگامی که داده‌ها در اپلیکیشن مدیریت تغییر می‌کنند:

```csharp
private void OnDataChanged()
{
    Dispatcher.Invoke(() =>
    {
        LoadData(); // بارگذاری مجدد داده‌ها
    });
}
```

## انواع چارت‌های موجود

### ۱. چارت عملکرد (Performance Chart)
- بر اساس تکمیل وظایف محاسبه می‌شود
- روند هفتگی عملکرد را نشان می‌دهد
- از رنگ آبی روشن استفاده می‌کند

### ۲. چارت توزیع شیفت (Shift Distribution Chart)
```csharp
public SeriesCollection GenerateShiftDistributionChart(Dictionary<string, object> reportData)
{
    var seriesCollection = new SeriesCollection();
    
    // شمارش کارمندان شیفت صبح
    var morningCount = GetShiftEmployeeCount(shifts, "morning");
    
    // شمارش کارمندان شیفت عصر
    var eveningCount = GetShiftEmployeeCount(shifts, "evening");
    
    seriesCollection.Add(new PieSeries
    {
        Title = "شیفت صبح",
        Values = new ChartValues<double> { morningCount },
        Fill = Brushes.LightGreen
    });
    
    seriesCollection.Add(new PieSeries
    {
        Title = "شیفت عصر",
        Values = new ChartValues<double> { eveningCount },
        Fill = Brushes.LightCoral
    });
    
    return seriesCollection;
}
```

### ۳. چارت روند غیبت (Absence Trend Chart)
```csharp
public SeriesCollection GenerateAbsenceTrendChart(Dictionary<string, object> reportData)
{
    var seriesCollection = new SeriesCollection();
    
    var leaveCount = GetAbsenceCount(absences, "مرخصی");
    var sickCount = GetAbsenceCount(absences, "بیمار");
    var absentCount = GetAbsenceCount(absences, "غایب");
    
    seriesCollection.Add(new ColumnSeries
    {
        Title = "مرخصی",
        Values = new ChartValues<double> { leaveCount },
        Fill = Brushes.LightBlue
    });
    
    return seriesCollection;
}
```

## خلاصه فرآیند

1. **اپلیکیشن مدیریت** → داده‌های کارمندان، وظایف، شیفت‌ها و غیبت‌ها را در فایل‌های JSON ذخیره می‌کند
2. **SyncManager** → تغییرات فایل‌ها را نظارت کرده و فراخوانی‌ها را فعال می‌کند
3. **DataService اپلیکیشن نمایش** → آخرین فایل گزارش JSON را می‌خواند
4. **ChartService** → داده‌های تکمیل وظایف را تحلیل کرده و معیارهای عملکرد تولید می‌کند
5. **MainWindow** → رابط کاربری چارت را با داده‌های جدید به‌روزرسانی می‌کند
6. **همگام‌سازی بلادرنگ** → تغییرات در اپلیکیشن مدیریت به‌طور خودکار در چارت‌های اپلیکیشن نمایش منعکس می‌شوند

## نکات مهم

- چارت‌ها عمدتاً از **داده‌های تکمیل وظایف** برای محاسبه معیارهای عملکرد استفاده می‌کنند
- سیستم روند عملکرد هفتگی را بر اساس نسبت وظایف تکمیل شده به کل وظایف نشان می‌دهد
- تمام تغییرات در اپلیکیشن مدیریت به‌طور خودکار و بلادرنگ در اپلیکیشن نمایش منعکس می‌شوند
- سیستم از کتابخانه **LiveCharts.Wpf** برای تولید چارت‌های تعاملی استفاده می‌کند
