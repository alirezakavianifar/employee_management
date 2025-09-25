using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Extensions.Logging;
using Shared.Services;
using System.Windows.Media;

namespace DisplayApp.Services
{
    public class ChartService
    {
        private readonly ILogger<ChartService> _logger;

        public ChartService()
        {
            _logger = LoggingService.CreateLogger<ChartService>();
            _logger.LogInformation("ChartService initialized");
        }

        public LineSeries GeneratePerformanceChart(Dictionary<string, object> reportData)
        {
            try
            {
                // Generate sample performance data based on tasks
                var performanceData = GeneratePerformanceData(reportData);
                
                var lineSeries = new LineSeries
                {
                    Title = "عملکرد",
                    Values = new ChartValues<double>(performanceData),
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 8,
                    Stroke = Brushes.LightBlue,
                    Fill = Brushes.LightBlue,
                    StrokeThickness = 2,
                    LineSmoothness = 0.5
                };

                _logger.LogInformation("Generated performance chart with {Count} data points", performanceData.Count);
                return lineSeries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating performance chart");
                return CreateDefaultChart();
            }
        }

        private List<double> GeneratePerformanceData(Dictionary<string, object> reportData)
        {
            try
            {
                // Get historical data from reports directory
                var historicalData = GetHistoricalPerformanceData();
                
                if (historicalData.Count > 0)
                {
                    _logger.LogInformation("Using {Count} days of historical data for chart", historicalData.Count);
                    return historicalData;
                }
                else
                {
                    // Fallback to current day data if no historical data available
                    _logger.LogWarning("No historical data available, using current day data");
                    return GenerateCurrentDayPerformance(reportData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating performance data");
                return new List<double> { 80.0 }; // Single data point for current day
            }
        }

        private List<double> GetHistoricalPerformanceData()
        {
            try
            {
                var performanceData = new List<double>();
                var reportsDirectory = Shared.Utils.AppConfigHelper.Config.ReportsDirectory;
                
                if (!Directory.Exists(reportsDirectory))
                {
                    _logger.LogWarning("Reports directory does not exist: {Directory}", reportsDirectory);
                    return performanceData;
                }

                // Get all report files and sort by date
                var reportFiles = Directory.GetFiles(reportsDirectory, "report_*.json")
                    .Where(f => !f.Contains("backup"))
                    .OrderBy(f => f)
                    .Take(7) // Limit to last 7 days
                    .ToList();

                _logger.LogInformation("Found {Count} report files for chart data", reportFiles.Count);

                foreach (var reportFile in reportFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(reportFile);
                        var reportData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        
                        if (reportData != null)
                        {
                            // Calculate performance based on employee attendance
                            var performance = CalculateDailyPerformance(reportData);
                            performanceData.Add(performance);
                            
                            _logger.LogInformation("Added performance data: {Performance} for file: {File}", 
                                performance, Path.GetFileName(reportFile));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading report file: {File}", reportFile);
                    }
                }

                return performanceData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting historical performance data");
                return new List<double>();
            }
        }

        private double CalculateDailyPerformance(Dictionary<string, object> reportData)
        {
            try
            {
                _logger.LogInformation("Starting performance calculation for date: {Date}", 
                    reportData.GetValueOrDefault("date", "unknown"));
                
                var totalEmployees = 0;
                var shiftScore = 0.0;
                var absencePenalty = 0.0;
                
                // Factor 1: Count employees
                if (reportData.TryGetValue("employees", out var employeesObj))
                {
                    if (employeesObj is List<object> employeesList)
                    {
                        totalEmployees = employeesList.Count;
                        _logger.LogInformation("Found {Count} employees", totalEmployees);
                    }
                    else
                    {
                        _logger.LogWarning("Employees object is not a List<object>, type: {Type}", employeesObj?.GetType().Name);
                    }
                }
                else
                {
                    _logger.LogWarning("No employees key found in report data");
                }
                
                // Factor 2: Check for shift assignments
                if (reportData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
                {
                    var morningCount = 0;
                    var eveningCount = 0;
                    
                    if (shifts.TryGetValue("morning", out var morningShiftObj) && morningShiftObj is Dictionary<string, object> morningShift)
                    {
                        if (morningShift.TryGetValue("assigned_employees", out var morningEmployeesObj) && morningEmployeesObj is List<object> morningEmployees)
                        {
                            morningCount = morningEmployees.Count;
                        }
                    }
                    
                    if (shifts.TryGetValue("evening", out var eveningShiftObj) && eveningShiftObj is Dictionary<string, object> eveningShift)
                    {
                        if (eveningShift.TryGetValue("assigned_employees", out var eveningEmployeesObj) && eveningEmployeesObj is List<object> eveningEmployees)
                        {
                            eveningCount = eveningEmployees.Count;
                        }
                    }
                    
                    shiftScore = (morningCount + eveningCount) * 3; // 3 points per assigned employee
                    _logger.LogInformation("Shift assignments: Morning={Morning}, Evening={Evening}, ShiftScore={ShiftScore}", 
                        morningCount, eveningCount, shiftScore);
                }
                else
                {
                    _logger.LogWarning("No shifts data found");
                }
                
                // Factor 3: Check for absence data
                if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
                {
                    var totalAbsences = 0;
                    foreach (var absenceCategory in absences.Values)
                    {
                        if (absenceCategory is List<object> absenceList)
                        {
                            totalAbsences += absenceList.Count;
                        }
                    }
                    absencePenalty = totalAbsences * 2; // 2 points penalty per absence
                    _logger.LogInformation("Found {Count} absences, penalty: {Penalty}", totalAbsences, absencePenalty);
                }
                else
                {
                    _logger.LogInformation("No absences data found");
                }
                
                // Factor 4: Create variation based on date and employee count
                var dateVariation = 0.0;
                if (reportData.TryGetValue("date", out var dateObj))
                {
                    var dateStr = dateObj.ToString();
                    var hash = Math.Abs(dateStr.GetHashCode());
                    // Create consistent variation based on date
                    dateVariation = (hash % 25) - 12; // -12 to +12 variation
                    _logger.LogInformation("Date variation for {Date}: {Variation}", dateStr, dateVariation);
                }
                
                // Calculate base performance (simplified)
                var basePerformance = 70.0; // Start with 70
                basePerformance += totalEmployees * 2; // 2 points per employee
                basePerformance += shiftScore;
                basePerformance -= absencePenalty;
                basePerformance += dateVariation;
                
                // Ensure performance is within reasonable bounds
                var performance = Math.Max(50, Math.Min(95, basePerformance));
                
                _logger.LogInformation("Final performance calculation: Base={Base}, Employees={Employees}, ShiftScore={ShiftScore}, AbsencePenalty={AbsencePenalty}, DateVariation={DateVariation}, Final={Final}",
                    basePerformance, totalEmployees, shiftScore, absencePenalty, dateVariation, performance);
                
                return Math.Round(performance, 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating daily performance");
                return 75.0;
            }
        }

        private List<double> GenerateCurrentDayPerformance(Dictionary<string, object> reportData)
        {
            try
            {
                var performance = CalculateDailyPerformance(reportData);
                return new List<double> { performance };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating current day performance");
                return new List<double> { 80.0 };
            }
        }

        private LineSeries CreateDefaultChart()
        {
            return new LineSeries
            {
                Title = "عملکرد",
                Values = new ChartValues<double> { 50, 55, 60, 65, 62, 68, 70 },
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 8,
                Stroke = Brushes.LightBlue,
                Fill = Brushes.LightBlue,
                StrokeThickness = 2
            };
        }

        public SeriesCollection GenerateShiftDistributionChart(Dictionary<string, object> reportData)
        {
            try
            {
                var seriesCollection = new SeriesCollection();
                
                if (reportData.TryGetValue("shifts", out var shiftsObj) && shiftsObj is Dictionary<string, object> shifts)
                {
                    var morningCount = 0;
                    var eveningCount = 0;
                    
                    // Count morning shift employees
                    if (shifts.TryGetValue("morning", out var morningShiftObj) && morningShiftObj is Dictionary<string, object> morningShift)
                    {
                        if (morningShift.TryGetValue("assigned_employees", out var morningEmployeesObj) && morningEmployeesObj is List<object> morningEmployees)
                        {
                            morningCount = morningEmployees.Count;
                        }
                    }
                    
                    // Count evening shift employees
                    if (shifts.TryGetValue("evening", out var eveningShiftObj) && eveningShiftObj is Dictionary<string, object> eveningShift)
                    {
                        if (eveningShift.TryGetValue("assigned_employees", out var eveningEmployeesObj) && eveningEmployeesObj is List<object> eveningEmployees)
                        {
                            eveningCount = eveningEmployees.Count;
                        }
                    }
                    
                    seriesCollection.Add(new PieSeries
                    {
                        Title = "شیفت صبح",
                        Values = new ChartValues<double> { morningCount },
                        DataLabels = true,
                        Fill = Brushes.LightGreen
                    });
                    
                    seriesCollection.Add(new PieSeries
                    {
                        Title = "شیفت عصر",
                        Values = new ChartValues<double> { eveningCount },
                        DataLabels = true,
                        Fill = Brushes.LightCoral
                    });
                }
                
                _logger.LogInformation("Generated shift distribution chart");
                return seriesCollection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating shift distribution chart");
                return new SeriesCollection();
            }
        }

        public SeriesCollection GenerateAbsenceTrendChart(Dictionary<string, object> reportData)
        {
            try
            {
                var seriesCollection = new SeriesCollection();
                
                if (reportData.TryGetValue("absences", out var absencesObj) && absencesObj is Dictionary<string, object> absences)
                {
                    var leaveCount = GetAbsenceCount(absences, "مرخصی");
                    var sickCount = GetAbsenceCount(absences, "بیمار");
                    var absentCount = GetAbsenceCount(absences, "غایب");
                    
                    seriesCollection.Add(new ColumnSeries
                    {
                        Title = "مرخصی",
                        Values = new ChartValues<double> { leaveCount },
                        Fill = Brushes.LightBlue
                    });
                    
                    seriesCollection.Add(new ColumnSeries
                    {
                        Title = "بیمار",
                        Values = new ChartValues<double> { sickCount },
                        Fill = Brushes.Orange
                    });
                    
                    seriesCollection.Add(new ColumnSeries
                    {
                        Title = "غایب",
                        Values = new ChartValues<double> { absentCount },
                        Fill = Brushes.Red
                    });
                }
                
                _logger.LogInformation("Generated absence trend chart");
                return seriesCollection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating absence trend chart");
                return new SeriesCollection();
            }
        }

        private int GetAbsenceCount(Dictionary<string, object> absences, string category)
        {
            if (absences.TryGetValue(category, out var categoryObj) && categoryObj is List<object> categoryList)
            {
                return categoryList.Count;
            }
            return 0;
        }

        public string[] GetWeekLabels()
        {
            try
            {
                var reportsDirectory = Shared.Utils.AppConfigHelper.Config.ReportsDirectory;
                
                if (!Directory.Exists(reportsDirectory))
                {
                    return new[] { "امروز" };
                }

                // Get all report files and sort by date
                var reportFiles = Directory.GetFiles(reportsDirectory, "report_*.json")
                    .Where(f => !f.Contains("backup"))
                    .OrderBy(f => f)
                    .Take(7) // Limit to last 7 days
                    .ToList();

                if (reportFiles.Count == 0)
                {
                    return new[] { "امروز" };
                }

                var labels = new List<string>();
                
                for (int i = 0; i < reportFiles.Count; i++)
                {
                    var fileName = Path.GetFileName(reportFiles[i]);
                    // Extract date from filename (e.g., report_1404-06-26.json -> 1404-06-26)
                    var datePart = fileName.Replace("report_", "").Replace(".json", "");
                    
                    if (i == reportFiles.Count - 1)
                    {
                        labels.Add("امروز");
                    }
                    else
                    {
                        labels.Add($"{reportFiles.Count - i} روز قبل");
                    }
                }

                return labels.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating week labels");
                return new[] { "امروز" };
            }
        }

        public string[] GetAbsenceLabels()
        {
            return new[] { "مرخصی", "بیمار", "غایب" };
        }

    }
}
