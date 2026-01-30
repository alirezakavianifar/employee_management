using System;
using System.IO;
using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using Microsoft.Extensions.Logging;
using Shared.Services;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;

namespace ManagementApp.Services
{
    public class PdfReportService
    {
        private readonly ILogger<PdfReportService> _logger;

        // Color constants matching print functionality
        private static readonly DeviceRgb HeaderBackgroundColor = new DeviceRgb(227, 242, 253);
        private static readonly DeviceRgb SectionBackgroundColor = new DeviceRgb(245, 245, 245);
        private static readonly DeviceRgb BorderColor = new DeviceRgb(189, 189, 189);
        private static readonly DeviceRgb HeaderTextColor = new DeviceRgb(25, 118, 210);
        private static readonly DeviceRgb DarkTextColor = new DeviceRgb(66, 66, 66);

        // Structured report data
        private class ReportData
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

        public PdfReportService()
        {
            _logger = LoggingService.CreateLogger<PdfReportService>();
        }

        private string NormalizePersianText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Normalize Unicode to ensure proper character representation
            // NFC (Canonical Composition) is preferred for Persian/Arabic
            text = text.Normalize(NormalizationForm.FormC);
            
            return text;
        }

        private PdfFont LoadPersianFont(bool bold = false)
        {
            // Try Arial Unicode MS first (best Persian/Arabic support with proper OpenType features)
            try
            {
                var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "arialuni.ttf");
                if (File.Exists(fontPath))
                {
                    _logger.LogInformation("Loading Arial Unicode MS font from: {FontPath}", fontPath);
                    var font = PdfFontFactory.CreateFont(fontPath, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
                    // Enable Arabic/Persian text processing
                    font.SetSubset(false); // Don't subset to preserve all glyphs
                    return font;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Arial Unicode MS font, trying Tahoma");
            }

            // Try Tahoma (Windows default Persian-supporting font)
            try
            {
                var fontName = bold ? "tahomabd" : "tahoma";
                var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", $"{fontName}.ttf");
                
                if (File.Exists(fontPath))
                {
                    _logger.LogInformation("Loading Tahoma font from: {FontPath}", fontPath);
                    var font = PdfFontFactory.CreateFont(fontPath, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
                    font.SetSubset(false); // Don't subset to preserve all glyphs
                    return font;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Tahoma font, trying Segoe UI");
            }

            // Try Segoe UI (Windows 10+ default, has Persian support)
            try
            {
                var fontName = bold ? "segoeuib" : "segoeui";
                var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", $"{fontName}.ttf");
                
                if (File.Exists(fontPath))
                {
                    _logger.LogInformation("Loading Segoe UI font from: {FontPath}", fontPath);
                    var font = PdfFontFactory.CreateFont(fontPath, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
                    font.SetSubset(false);
                    return font;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Segoe UI font");
            }

            // Final fallback: Use standard font (will not support Persian but won't crash)
            _logger.LogWarning("No Persian-supporting font found, using standard font (Persian text may not render correctly)");
            return bold 
                ? PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)
                : PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        }

        private Table CreateHeaderTable(string reportTitle, PdfFont titleFont, PdfFont headerFont, PdfFont contentFont, string startDate, string endDate)
        {
            var headerTable = new Table(1).UseAllAvailableWidth();
            headerTable.SetMarginBottom(20);
            
            var headerCell = new Cell()
                .Add(new Paragraph(NormalizePersianText("سیستم مدیریت کارمندان"))
                    .SetFont(titleFont)
                    .SetFontSize(20)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(HeaderTextColor))
                .Add(new Paragraph(NormalizePersianText(reportTitle))
                    .SetFont(headerFont)
                    .SetFontSize(18)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginTop(10)
                    .SetFontColor(HeaderTextColor));
            
            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                headerCell.Add(new Paragraph(NormalizePersianText($"از {startDate} تا {endDate}"))
                    .SetFont(contentFont)
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginTop(5)
                    .SetFontColor(DarkTextColor));
            }
            
            headerCell.Add(new Paragraph(NormalizePersianText($"تاریخ تولید: {DateTime.Now:yyyy/MM/dd HH:mm}"))
                .SetFont(contentFont)
                .SetFontSize(10)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(8)
                .SetFontColor(DarkTextColor))
                .SetBackgroundColor(HeaderBackgroundColor)
                .SetBorder(new iText.Layout.Borders.SolidBorder(BorderColor, 1))
                .SetPadding(15)
                .SetTextAlignment(TextAlignment.CENTER);
            
            headerTable.AddCell(headerCell);
            return headerTable;
        }

        private Table CreateStatisticsTable(string sectionTitle, List<(string Label, string Value)> data, PdfFont headerFont, PdfFont contentFont)
        {
            var table = new Table(2).UseAllAvailableWidth();
            table.SetMarginBottom(20);
            table.SetBorder(new iText.Layout.Borders.SolidBorder(BorderColor, 1));
            
            // Section header row
            var headerRow = new Cell(1, 2) // rowspan=1, colspan=2
                .Add(new Paragraph(NormalizePersianText(sectionTitle))
                    .SetFont(headerFont)
                    .SetFontSize(16)
                    .SetFontColor(HeaderTextColor)
                    .SetTextAlignment(TextAlignment.RIGHT))
                .SetBackgroundColor(SectionBackgroundColor)
                .SetBorder(new iText.Layout.Borders.SolidBorder(BorderColor, 1))
                .SetPadding(12)
                .SetTextAlignment(TextAlignment.RIGHT);
            table.AddCell(headerRow);
            
            // Data rows
            bool isAlternate = false;
            foreach (var item in data)
            {
                var labelCell = new Cell()
                    .Add(new Paragraph(NormalizePersianText(item.Label))
                        .SetFont(contentFont)
                        .SetFontSize(12)
                        .SetFontColor(DarkTextColor)
                        .SetTextAlignment(TextAlignment.RIGHT))
                    .SetBackgroundColor(isAlternate ? SectionBackgroundColor : ColorConstants.WHITE)
                    .SetBorder(new iText.Layout.Borders.SolidBorder(BorderColor, 1))
                    .SetPadding(12)
                    .SetTextAlignment(TextAlignment.RIGHT);
                
                var valueCell = new Cell()
                    .Add(new Paragraph(item.Value)
                        .SetFont(headerFont)
                        .SetFontSize(14)
                        .SetFontColor(HeaderTextColor)
                        .SetTextAlignment(TextAlignment.CENTER))
                    .SetBackgroundColor(isAlternate ? SectionBackgroundColor : ColorConstants.WHITE)
                    .SetBorder(new iText.Layout.Borders.SolidBorder(BorderColor, 1))
                    .SetPadding(12)
                    .SetTextAlignment(TextAlignment.CENTER);
                
                table.AddCell(labelCell);
                table.AddCell(valueCell);
                isAlternate = !isAlternate;
            }
            
            return table;
        }

        private void AddVisualSeparator(Document document, PdfFont contentFont)
        {
            var separator = new Paragraph(new string('─', 50))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(15)
                .SetMarginBottom(15)
                .SetFontColor(BorderColor);
            document.Add(separator);
        }

        private Table CreateFooterTable(PdfFont smallFont)
        {
            var footerTable = new Table(1).UseAllAvailableWidth();
            footerTable.SetMarginTop(30);
            
            var footerCell = new Cell()
                .Add(new Paragraph(NormalizePersianText("گزارش تولید شده توسط سیستم مدیریت کارمندان"))
                    .SetFont(smallFont)
                    .SetFontSize(9)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(DarkTextColor))
                .SetBackgroundColor(SectionBackgroundColor)
                .SetBorder(new iText.Layout.Borders.SolidBorder(BorderColor, 1))
                .SetPadding(12)
                .SetTextAlignment(TextAlignment.CENTER);
            
            footerTable.AddCell(footerCell);
            return footerTable;
        }

        public bool ExportReportToPdf(string reportContent, string filePath, string reportTitle = "گزارش", string assignedTo = "")
        {
            try
            {
                _logger.LogInformation("Starting PDF export to: {FilePath}", filePath);
                _logger.LogInformation("Report title: {ReportTitle}", reportTitle);
                _logger.LogInformation("Report content length: {Length}", reportContent?.Length ?? 0);
                _logger.LogInformation("Assigned to: {AssignedTo}", assignedTo);
                
                if (string.IsNullOrEmpty(reportContent))
                {
                    _logger.LogWarning("Report content is empty");
                    return false;
                }
                
                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogWarning("File path is empty");
                    return false;
                }

                // Parse report content into structured data
                var reportData = ParseReportContent(reportContent);
                
                // Create PDF writer and document
                _logger.LogInformation("Creating file stream for: {FilePath}", filePath);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    _logger.LogInformation("Creating PDF writer");
                    using (var writer = new PdfWriter(fileStream))
                    using (var pdf = new PdfDocument(writer))
                    using (var document = new Document(pdf))
                    {
                        // Set up fonts with Persian support
                        var titleFont = LoadPersianFont(bold: true);
                        var headerFont = LoadPersianFont(bold: true);
                        var contentFont = LoadPersianFont(bold: false);
                        var smallFont = LoadPersianFont(bold: false);

                        // Add header table
                        var headerTable = CreateHeaderTable(reportTitle, titleFont, headerFont, contentFont, 
                            reportData.StartDate, reportData.EndDate);
                        document.Add(headerTable);

                        // Add metadata table (assigned to)
                        if (!string.IsNullOrEmpty(assignedTo))
                        {
                            var metadataTable = new Table(1).UseAllAvailableWidth();
                            metadataTable.SetMarginBottom(20);
                            
                            var metadataCell = new Cell()
                                .Add(new Paragraph(NormalizePersianText($"مسئول گزارش: {assignedTo}"))
                                    .SetFont(contentFont)
                                    .SetFontSize(11)
                                    .SetFontColor(DarkTextColor))
                                .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                                .SetPadding(5)
                                .SetTextAlignment(TextAlignment.RIGHT);
                            
                            metadataTable.AddCell(metadataCell);
                            document.Add(metadataTable);
                        }

                        AddVisualSeparator(document, contentFont);

                        // Employee Statistics Table (always show)
                        var employeeData = new List<(string, string)>
                        {
                            ("کل کارمندان", reportData.TotalEmployees > 0 ? reportData.TotalEmployees.ToString() : "0"),
                            ("کل غیبت‌ها در بازه", reportData.TotalAbsences > 0 ? reportData.TotalAbsences.ToString() : "0")
                        };
                        
                        var employeeTable = CreateStatisticsTable("آمار کارمندان", employeeData, headerFont, contentFont);
                        document.Add(employeeTable);
                        AddVisualSeparator(document, contentFont);

                        // Shift Statistics Table (always show, use default capacity if not parsed)
                        var capacity = reportData.ShiftCapacity > 0 ? reportData.ShiftCapacity : 15;
                        var shiftData = new List<(string, string)>
                        {
                            ("شیفت صبح (میانگین)", $"{reportData.AverageMorningShift:F1}/{capacity}"),
                            ("شیفت عصر (میانگین)", $"{reportData.AverageAfternoonShift:F1}/{capacity}"),
                            ("شیفت شب (میانگین)", $"{reportData.AverageNightShift:F1}/{capacity}"),
                            ("حداکثر شیفت صبح", $"{reportData.MaxMorningShift}/{capacity}"),
                            ("حداکثر شیفت عصر", $"{reportData.MaxAfternoonShift}/{capacity}"),
                            ("حداکثر شیفت شب", $"{reportData.MaxNightShift}/{capacity}")
                        };
                        
                        var shiftTable = CreateStatisticsTable("آمار شیفت‌ها (میانگین)", shiftData, headerFont, contentFont);
                        document.Add(shiftTable);
                        AddVisualSeparator(document, contentFont);

                        // Task Statistics Table (always show)
                        var taskData = new List<(string, string)>
                        {
                            ("کل وظایف", reportData.TotalTasks.ToString()),
                            ("تکمیل شده", reportData.CompletedTasks.ToString()),
                            ("در حال انجام", reportData.InProgressTasks.ToString()),
                            ("در انتظار", reportData.PendingTasks.ToString())
                        };
                        
                        var taskTable = CreateStatisticsTable("آمار وظایف (کل دوره)", taskData, headerFont, contentFont);
                        document.Add(taskTable);
                        AddVisualSeparator(document, contentFont);

                        // Daily Details Section
                        if (reportData.DailyDetails.Count > 0)
                        {
                            var dailyHeaderTable = new Table(1).UseAllAvailableWidth();
                            dailyHeaderTable.SetMarginBottom(10);
                            
                            var dailyHeaderCell = new Cell()
                                .Add(new Paragraph(NormalizePersianText("جزئیات روزانه"))
                                    .SetFont(headerFont)
                                    .SetFontSize(16)
                                    .SetFontColor(HeaderTextColor)
                                    .SetTextAlignment(TextAlignment.RIGHT))
                                .SetBackgroundColor(SectionBackgroundColor)
                                .SetBorder(new iText.Layout.Borders.SolidBorder(BorderColor, 1))
                                .SetPadding(12)
                                .SetTextAlignment(TextAlignment.RIGHT);
                            
                            dailyHeaderTable.AddCell(dailyHeaderCell);
                            document.Add(dailyHeaderTable);
                            
                            foreach (var detail in reportData.DailyDetails)
                            {
                                var detailParagraph = new Paragraph(NormalizePersianText(detail))
                                    .SetFont(contentFont)
                                    .SetFontSize(11)
                                    .SetFontColor(DarkTextColor)
                                    .SetTextAlignment(TextAlignment.RIGHT)
                                    .SetMarginBottom(5)
                                    .SetPaddingLeft(15);
                                document.Add(detailParagraph);
                            }
                            
                            AddVisualSeparator(document, contentFont);
                        }

                        // Add footer
                        var footerTable = CreateFooterTable(smallFont);
                        document.Add(footerTable);
                    }
                }

                _logger.LogInformation("PDF export completed successfully: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting PDF to: {FilePath}", filePath);
                return false;
            }
        }

        private ReportData ParseReportContent(string content)
        {
            var reportData = new ReportData();
            
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Report content is empty, returning default report data");
                return reportData;
            }

            _logger.LogInformation("Parsing report content, length: {Length}", content.Length);
            var lines = content.Split('\n');
            bool inDailyDetails = false;
            int parsedLines = 0;
            
            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (string.IsNullOrWhiteSpace(cleanLine))
                {
                    inDailyDetails = false;
                    continue;
                }

                // Remove emojis and formatting characters if present
                cleanLine = Regex.Replace(cleanLine, @"[📊📅👥❌⏰📋📈•=]", "").Trim();
                
                // Normalize whitespace
                cleanLine = Regex.Replace(cleanLine, @"\s+", " ");

                // Parse report type
                if (cleanLine.StartsWith("گزارش") && !cleanLine.Contains(":"))
                {
                    var match = Regex.Match(cleanLine, @"گزارش\s+(.+)");
                    if (match.Success)
                        reportData.ReportType = match.Groups[1].Value.Trim();
                }
                // Parse dates
                else if (cleanLine.Contains("تاریخ شروع:"))
                {
                    var match = Regex.Match(cleanLine, @"تاریخ شروع:\s*(.+)");
                    if (match.Success)
                        reportData.StartDate = match.Groups[1].Value.Trim();
                }
                else if (cleanLine.Contains("تاریخ پایان:"))
                {
                    var match = Regex.Match(cleanLine, @"تاریخ پایان:\s*(.+)");
                    if (match.Success)
                        reportData.EndDate = match.Groups[1].Value.Trim();
                }
                // Parse employee statistics
                else if (cleanLine.Contains("کل کارمندان") && !cleanLine.Contains("مدیران"))
                {
                    var match = Regex.Match(cleanLine, @"کل\s+کارمندان[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
                        reportData.TotalEmployees = count;
                        parsedLines++;
                        _logger.LogDebug("Parsed TotalEmployees: {Count}", count);
                    }
                }
                else if (cleanLine.Contains("کل غیبت") && (cleanLine.Contains("بازه") || cleanLine.Contains("در بازه")))
                {
                    var match = Regex.Match(cleanLine, @"کل\s+غیبت[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
                        reportData.TotalAbsences = count;
                        parsedLines++;
                        _logger.LogDebug("Parsed TotalAbsences: {Count}", count);
                    }
                }
                // Parse shift statistics
                else if (cleanLine.Contains("شیفت صبح") && cleanLine.Contains("/") && !cleanLine.Contains("حداکثر"))
                {
                    var match = Regex.Match(cleanLine, @"شیفت\s+صبح[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                        {
                            reportData.AverageMorningShift = avg;
                            parsedLines++;
                            _logger.LogDebug("Parsed AverageMorningShift: {Value}", avg);
                        }
                        if (int.TryParse(match.Groups[2].Value, out int cap) && reportData.ShiftCapacity == 0)
                        {
                            reportData.ShiftCapacity = cap;
                            _logger.LogDebug("Parsed ShiftCapacity: {Value}", cap);
                        }
                    }
                }
                else if (cleanLine.Contains("شیفت عصر") && cleanLine.Contains("/") && !cleanLine.Contains("حداکثر"))
                {
                    var match = Regex.Match(cleanLine, @"شیفت\s+عصر[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                    {
                        reportData.AverageAfternoonShift = avg;
                        parsedLines++;
                        _logger.LogDebug("Parsed AverageAfternoonShift: {Value}", avg);
                    }
                }
                else if (cleanLine.Contains("شیفت شب") && cleanLine.Contains("/") && !cleanLine.Contains("حداکثر"))
                {
                    var match = Regex.Match(cleanLine, @"شیفت\s+شب[^:]*:\s*([\d.]+)/(\d+)");
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double avg))
                    {
                        reportData.AverageNightShift = avg;
                        parsedLines++;
                        _logger.LogDebug("Parsed AverageNightShift: {Value}", avg);
                    }
                }
                else if (cleanLine.Contains("حداکثر") && cleanLine.Contains("شیفت صبح"))
                {
                    var match = Regex.Match(cleanLine, @"حداکثر\s+شیفت\s+صبح[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                    {
                        reportData.MaxMorningShift = max;
                        parsedLines++;
                        _logger.LogDebug("Parsed MaxMorningShift: {Value}", max);
                    }
                }
                else if (cleanLine.Contains("حداکثر") && cleanLine.Contains("شیفت عصر"))
                {
                    var match = Regex.Match(cleanLine, @"حداکثر\s+شیفت\s+عصر[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                    {
                        reportData.MaxAfternoonShift = max;
                        parsedLines++;
                        _logger.LogDebug("Parsed MaxAfternoonShift: {Value}", max);
                    }
                }
                else if (cleanLine.Contains("حداکثر") && cleanLine.Contains("شیفت شب"))
                {
                    var match = Regex.Match(cleanLine, @"حداکثر\s+شیفت\s+شب[^:]*:\s*(\d+)/(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int max))
                    {
                        reportData.MaxNightShift = max;
                        parsedLines++;
                        _logger.LogDebug("Parsed MaxNightShift: {Value}", max);
                    }
                }
                // Parse task statistics
                else if (cleanLine.Contains("کل وظایف") && cleanLine.Contains(":"))
                {
                    var match = Regex.Match(cleanLine, @"کل\s+وظایف[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
                        reportData.TotalTasks = count;
                        parsedLines++;
                        _logger.LogDebug("Parsed TotalTasks: {Count}", count);
                    }
                }
                else if (cleanLine.Contains("تکمیل شده") && cleanLine.Contains(":"))
                {
                    var match = Regex.Match(cleanLine, @"تکمیل\s+شده[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
                        reportData.CompletedTasks = count;
                        parsedLines++;
                        _logger.LogDebug("Parsed CompletedTasks: {Count}", count);
                    }
                }
                else if (cleanLine.Contains("در حال انجام") && cleanLine.Contains(":"))
                {
                    var match = Regex.Match(cleanLine, @"در\s+حال\s+انجام[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
                        reportData.InProgressTasks = count;
                        parsedLines++;
                        _logger.LogDebug("Parsed InProgressTasks: {Count}", count);
                    }
                }
                else if (cleanLine.Contains("در انتظار") && cleanLine.Contains(":"))
                {
                    var match = Regex.Match(cleanLine, @"در\s+انتظار[^:]*:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
                        reportData.PendingTasks = count;
                        parsedLines++;
                        _logger.LogDebug("Parsed PendingTasks: {Count}", count);
                    }
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
                    {
                        reportData.DailyDetails.Add(cleanLine);
                        parsedLines++;
                    }
                }
            }

            _logger.LogInformation("Parsing completed. Parsed {ParsedLines} data lines. ReportData: Employees={Employees}, Absences={Absences}, Tasks={Tasks}", 
                parsedLines, reportData.TotalEmployees, reportData.TotalAbsences, reportData.TotalTasks);
            
            return reportData;
        }

        private string FormatReportContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "";

            // Clean up the content and format it properly
            var lines = content.Split('\n');
            var formattedLines = new StringBuilder();

            // Add a proper header
            formattedLines.AppendLine("📊 خلاصه گزارش");
            formattedLines.AppendLine("=" + new string('=', 30));

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (string.IsNullOrEmpty(cleanLine))
                {
                    formattedLines.AppendLine();
                    continue;
                }

                // Remove system prefixes and clean up the line
                if (cleanLine.StartsWith("System.Windows.Controls.ComboBoxItem: "))
                {
                    cleanLine = cleanLine.Substring("System.Windows.Controls.ComboBoxItem: ".Length);
                }

                // Skip lines that are just colons or system data
                if (cleanLine == ":" || cleanLine.StartsWith(": ") || cleanLine.Length < 3)
                {
                    continue;
                }

                // Format specific patterns
                if (cleanLine.Contains("تاریخ شروع:") || cleanLine.Contains("تاریخ پایان:"))
                {
                    formattedLines.AppendLine($"📅 {cleanLine}");
                }
                else if (cleanLine.Contains("تعداد کل کارمندان:") || cleanLine.Contains("تعداد مدیران:"))
                {
                    formattedLines.AppendLine($"👥 {cleanLine}");
                }
                else if (cleanLine.Contains("غیبت") || cleanLine.Contains("غایب"))
                {
                    formattedLines.AppendLine($"❌ {cleanLine}");
                }
                else if (cleanLine.Contains("شیفت صبح") || cleanLine.Contains("شیفت عصر"))
                {
                    formattedLines.AppendLine($"⏰ {cleanLine}");
                }
                else if (cleanLine.Contains("وظایف") || cleanLine.Contains("تکمیل شده") || cleanLine.Contains("در حال انجام"))
                {
                    formattedLines.AppendLine($"📋 {cleanLine}");
                }
                else if (cleanLine.Contains(":") && IsPersianText(cleanLine) && cleanLine.Length < 50)
                {
                    // Section headers
                    formattedLines.AppendLine($"\n📊 {cleanLine}");
                }
                else if (cleanLine.Contains("=") && cleanLine.Contains("/"))
                {
                    // Statistics lines
                    formattedLines.AppendLine($"📈 {cleanLine}");
                }
                else if (IsPersianText(cleanLine) && cleanLine.Length > 5)
                {
                    // Regular Persian content
                    formattedLines.AppendLine($"• {cleanLine}");
                }
                else if (cleanLine.Length > 3)
                {
                    // Other content
                    formattedLines.AppendLine(cleanLine);
                }
            }

            // Add a footer section
            formattedLines.AppendLine("\n" + new string('=', 30));
            formattedLines.AppendLine("📝 توضیحات:");
            formattedLines.AppendLine("• این گزارش به صورت خودکار تولید شده است");
            formattedLines.AppendLine("• تمام داده‌ها از سیستم مدیریت کارمندان استخراج شده‌اند");
            formattedLines.AppendLine("• برای اطلاعات بیشتر با بخش منابع انسانی تماس بگیرید");

            return formattedLines.ToString();
        }

        private bool IsSectionHeader(string line)
        {
            return line.StartsWith("📊") || (line.Contains(":") && IsPersianText(line) && line.Length < 50);
        }

        private bool IsStatisticsLine(string line)
        {
            return line.StartsWith("⏰") || line.StartsWith("👥") || line.StartsWith("❌") || line.StartsWith("📋") ||
                   (line.Contains("=") && line.Contains("/"));
        }

        private bool IsDateRangeLine(string line)
        {
            return line.StartsWith("📅");
        }

        private bool IsDetailLine(string line)
        {
            return line.Contains("(") && line.Contains(")") && line.Length > 20;
        }

        private bool IsPersianText(string text)
        {
            // Check if text contains Persian characters
            foreach (char c in text)
            {
                if (c >= 0x0600 && c <= 0x06FF) // Persian/Arabic Unicode range
                {
                    return true;
                }
            }
            return false;
        }

        public string GetDefaultFileName(string reportType, string startDate, string endDate)
        {
            var sanitizedReportType = SanitizeFileName(reportType);
            var sanitizedStartDate = SanitizeFileName(startDate);
            var sanitizedEndDate = SanitizeFileName(endDate);
            
            return $"{sanitizedReportType}_{sanitizedStartDate}_to_{sanitizedEndDate}.pdf";
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = fileName;
            
            foreach (var invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }
            
            // Replace Persian characters with English equivalents for better compatibility
            sanitized = sanitized.Replace("/", "-")
                               .Replace(":", "-")
                               .Replace(" ", "_");
            
            return sanitized;
        }
    }
}