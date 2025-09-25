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

namespace ManagementApp.Services
{
    public class PdfReportService
    {
        private readonly ILogger<PdfReportService> _logger;

        public PdfReportService()
        {
            _logger = LoggingService.CreateLogger<PdfReportService>();
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

                // Create PDF writer and document
                _logger.LogInformation("Creating file stream for: {FilePath}", filePath);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    _logger.LogInformation("Creating PDF writer");
                    using (var writer = new PdfWriter(fileStream))
                    using (var pdf = new PdfDocument(writer))
                    using (var document = new Document(pdf))
                    {
                        // Set up fonts with different sizes
                        var titleFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                        var headerFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                        var contentFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                        var smallFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                        // Add header with company info
                        var headerTable = new Table(1).UseAllAvailableWidth();
                        headerTable.SetMarginBottom(20);
                        
                        var headerCell = new Cell()
                            .Add(new Paragraph("سیستم مدیریت کارمندان")
                                .SetFont(titleFont)
                                .SetFontSize(20)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetFontColor(ColorConstants.BLUE))
                            .Add(new Paragraph(reportTitle)
                                .SetFont(headerFont)
                                .SetFontSize(16)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetMarginTop(10)
                                .SetFontColor(ColorConstants.DARK_GRAY))
                            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                            .SetTextAlignment(TextAlignment.CENTER);
                        
                        headerTable.AddCell(headerCell);
                        document.Add(headerTable);

                        // Add report metadata
                        var metadataTable = new Table(2).UseAllAvailableWidth();
                        metadataTable.SetMarginBottom(20);
                        
                        var dateCell = new Cell()
                            .Add(new Paragraph($"تاریخ تولید: {DateTime.Now:yyyy/MM/dd HH:mm}")
                                .SetFont(contentFont)
                                .SetFontSize(10))
                            .SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                        
                        var assignedCell = new Cell()
                            .Add(new Paragraph(!string.IsNullOrEmpty(assignedTo) ? $"مسئول: {assignedTo}" : "")
                                .SetFont(contentFont)
                                .SetFontSize(10)
                                .SetTextAlignment(TextAlignment.RIGHT))
                            .SetBorder(iText.Layout.Borders.Border.NO_BORDER);
                        
                        metadataTable.AddCell(dateCell);
                        metadataTable.AddCell(assignedCell);
                        document.Add(metadataTable);

                        // Add decorative line
                        var line = new Paragraph(new string('-', 50))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetMarginBottom(15);
                        document.Add(line);
                        document.Add(new Paragraph(" ").SetMarginBottom(15));

                        // Process and format report content
                        var formattedContent = FormatReportContent(reportContent);
                        var lines = formattedContent.Split('\n');

                        foreach (var contentLine in lines)
                        {
                            if (string.IsNullOrWhiteSpace(contentLine))
                            {
                                document.Add(new Paragraph(" ").SetMarginBottom(5));
                                continue;
                            }

                            // Format different types of content
                            if (IsSectionHeader(contentLine))
                            {
                                var headerParagraph = new Paragraph(contentLine)
                                    .SetFont(headerFont)
                                    .SetFontSize(14)
                                    .SetMarginTop(15)
                                    .SetMarginBottom(8)
                                    .SetFontColor(ColorConstants.BLUE);
                                document.Add(headerParagraph);
                            }
                            else if (IsStatisticsLine(contentLine))
                            {
                                var statsParagraph = new Paragraph(contentLine)
                                    .SetFont(contentFont)
                                    .SetFontSize(11)
                                    .SetMarginLeft(20)
                                    .SetMarginBottom(4)
                                    .SetFontColor(ColorConstants.DARK_GRAY);
                                document.Add(statsParagraph);
                            }
                            else if (IsDateRangeLine(contentLine))
                            {
                                var dateParagraph = new Paragraph(contentLine)
                                    .SetFont(contentFont)
                                    .SetFontSize(10)
                                    .SetMarginBottom(6)
                                    .SetFontColor(ColorConstants.GRAY);
                                document.Add(dateParagraph);
                            }
                            else if (IsDetailLine(contentLine))
                            {
                                var detailParagraph = new Paragraph(contentLine)
                                    .SetFont(smallFont)
                                    .SetFontSize(9)
                                    .SetMarginLeft(30)
                                    .SetMarginBottom(2)
                                    .SetFontColor(ColorConstants.GRAY);
                                document.Add(detailParagraph);
                            }
                            else
                            {
                                // Regular content
                                var paragraph = new Paragraph(contentLine)
                                    .SetFont(contentFont)
                                    .SetFontSize(11)
                                    .SetMarginBottom(4);
                                document.Add(paragraph);
                            }
                        }

                        // Add footer
                        document.Add(new Paragraph(" ").SetMarginTop(30));
                        var footerLine = new Paragraph(new string('-', 50))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetMarginBottom(10);
                        document.Add(footerLine);
                        
                        var footerTable = new Table(1).UseAllAvailableWidth();
                        footerTable.SetMarginTop(10);
                        
                        var footerCell = new Cell()
                            .Add(new Paragraph("گزارش تولید شده توسط سیستم مدیریت کارمندان")
                                .SetFont(smallFont)
                                .SetFontSize(8)
                                .SetTextAlignment(TextAlignment.CENTER)
                                .SetFontColor(ColorConstants.LIGHT_GRAY))
                            .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                            .SetTextAlignment(TextAlignment.CENTER);
                        
                        footerTable.AddCell(footerCell);
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