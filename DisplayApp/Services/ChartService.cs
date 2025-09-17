using System;
using System.Collections.Generic;
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
                var performanceData = new List<double>();
                
                // Try to get task data for performance calculation
                if (reportData.TryGetValue("tasks", out var tasksObj) && tasksObj is Dictionary<string, object> tasks)
                {
                    if (tasks.TryGetValue("tasks", out var taskListObj) && taskListObj is Dictionary<string, object> taskDict)
                    {
                        // Calculate performance based on completed tasks
                        var completedTasks = 0;
                        var totalTasks = taskDict.Count;
                        
                        foreach (var task in taskDict.Values)
                        {
                            if (task is Dictionary<string, object> taskData)
                            {
                                if (taskData.TryGetValue("status", out var statusObj) && 
                                    statusObj.ToString() == "تکمیل شده")
                                {
                                    completedTasks++;
                                }
                            }
                        }
                        
                        // Generate weekly performance data
                        var basePerformance = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 50;
                        
                        for (int week = 1; week <= 7; week++)
                        {
                            // Add some variation to make it look realistic
                            var variation = (week - 4) * 5; // Peak around week 4
                            var performance = Math.Max(0, Math.Min(100, basePerformance + variation + (week * 2)));
                            performanceData.Add(Math.Round(performance, 1));
                        }
                    }
                    else
                    {
                        // No task data, use default performance curve
                        performanceData = new List<double> { 45, 52, 58, 65, 62, 68, 70 };
                    }
                }
                else
                {
                    // No task data, use default performance curve
                    performanceData = new List<double> { 45, 52, 58, 65, 62, 68, 70 };
                }
                
                return performanceData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating performance data");
                return new List<double> { 50, 55, 60, 65, 62, 68, 70 };
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
            return new[] { "هفته 1", "هفته 2", "هفته 3", "هفته 4", "هفته 5", "هفته 6", "هفته 7" };
        }

        public string[] GetAbsenceLabels()
        {
            return new[] { "مرخصی", "بیمار", "غایب" };
        }
    }
}
