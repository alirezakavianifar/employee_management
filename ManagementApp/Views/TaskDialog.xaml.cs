using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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

            if (!double.TryParse(EstimatedHoursTextBox.Text, out double estimatedHours) || estimatedHours <= 0)
            {
                MessageBox.Show("لطفاً ساعت تخمینی معتبر وارد کنید", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
    }
}
