using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shared.Utils;

namespace ManagementApp.Views
{
    public partial class TaskDialog : Window
    {
        public new string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public string Priority { get; private set; } = "متوسط";
        public double EstimatedHours { get; private set; } = 1.0;
        public string? TargetDate { get; private set; }

        public TaskDialog()
        {
            InitializeComponent();
        }

        public TaskDialog(Shared.Models.Task task) : this()
        {
            TitleTextBox.Text = task.Title;
            DescriptionTextBox.Text = task.Description;
            
            // Set priority
            PriorityComboBox.SelectedIndex = (int)task.Priority;
            
            EstimatedHoursTextBox.Text = task.EstimatedHours.ToString();
            
            // Set the target date (already in Shamsi format)
            TargetDate = task.TargetDate;
            TargetDatePicker.SelectedDate = task.TargetDate;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("لطفاً عنوان وظیفه را وارد کنید", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Use robust parsing method
            var originalText = EstimatedHoursTextBox.Text;
            
            // Debug information
            System.Diagnostics.Debug.WriteLine($"Original text: '{originalText}'");
            System.Diagnostics.Debug.WriteLine($"Text length: {originalText?.Length}");
            System.Diagnostics.Debug.WriteLine($"Text bytes: {string.Join(", ", System.Text.Encoding.UTF8.GetBytes(originalText ?? ""))}");
            
            if (!TryParseEstimatedHours(originalText ?? "", out double estimatedHours))
            {
                var convertedText = ConvertPersianToEnglishNumerals(originalText);
                System.Diagnostics.Debug.WriteLine($"Parsing failed for: '{originalText}' -> '{convertedText}'");
                MessageBox.Show($"لطفاً ساعت تخمینی معتبر وارد کنید\nمقدار وارد شده: '{originalText}'\nمقدار تبدیل شده: '{convertedText}'\nطول متن: {originalText?.Length}", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Successfully parsed: '{originalText}' -> {estimatedHours}");

            Title = TitleTextBox.Text.Trim();
            Description = DescriptionTextBox.Text.Trim();
            Priority = ((ComboBoxItem)PriorityComboBox.SelectedItem)?.Content?.ToString() ?? "متوسط";
            EstimatedHours = estimatedHours;
            TargetDate = TargetDatePicker.SelectedDate;
            
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void EstimatedHoursTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only numbers (English and Persian) and decimal point/comma
            var regex = new Regex(@"^[0-9۰-۹]+[.,،]?[0-9۰-۹]*$");
            var textBox = sender as TextBox;
            
            if (textBox != null)
            {
                // Get the text that would result from this input
                var newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
                
                // Check if the new text would be valid
                if (!regex.IsMatch(newText) && !string.IsNullOrEmpty(newText))
                {
                    e.Handled = true; // Block the input
                }
            }
        }

        private string ConvertPersianToEnglishNumerals(string persianText)
        {
            if (string.IsNullOrEmpty(persianText))
                return persianText;

            return persianText
                .Replace('۰', '0')
                .Replace('۱', '1')
                .Replace('۲', '2')
                .Replace('۳', '3')
                .Replace('۴', '4')
                .Replace('۵', '5')
                .Replace('۶', '6')
                .Replace('۷', '7')
                .Replace('۸', '8')
                .Replace('۹', '9')
                .Replace('،', '.'); // Persian comma to English decimal point
        }

        private bool TryParseEstimatedHours(string text, out double result)
        {
            result = 0;
            
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Convert Persian numerals to English
            var convertedText = ConvertPersianToEnglishNumerals(text.Trim());
            
            // Use InvariantCulture to ensure English formatting is used for parsing
            var invariantCulture = System.Globalization.CultureInfo.InvariantCulture;
            
            // Try parsing with different decimal separators using InvariantCulture
            if (double.TryParse(convertedText, System.Globalization.NumberStyles.Float, invariantCulture, out result))
                return result > 0;
                
            // Try with comma as decimal separator
            if (double.TryParse(convertedText.Replace(',', '.'), System.Globalization.NumberStyles.Float, invariantCulture, out result))
                return result > 0;
                
            // Try with period as decimal separator
            if (double.TryParse(convertedText.Replace('.', ','), System.Globalization.NumberStyles.Float, invariantCulture, out result))
                return result > 0;
                
            return false;
        }
    }
}
