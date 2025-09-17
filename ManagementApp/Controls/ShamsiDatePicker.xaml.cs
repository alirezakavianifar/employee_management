using System;
using System.Windows;
using System.Windows.Controls;
using Shared.Utils;

namespace ManagementApp.Controls
{
    public partial class ShamsiDatePicker : UserControl
    {
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register("SelectedDate", typeof(string), typeof(ShamsiDatePicker),
                new PropertyMetadata(ShamsiDateHelper.GetCurrentShamsiDate(), OnSelectedDateChanged));

        public string SelectedDate
        {
            get { return (string)GetValue(SelectedDateProperty); }
            set { SetValue(SelectedDateProperty, value); }
        }

        public static readonly DependencyProperty SelectedGregorianDateProperty =
            DependencyProperty.Register("SelectedGregorianDate", typeof(string), typeof(ShamsiDatePicker),
                new PropertyMetadata(ShamsiDateHelper.GetCurrentGregorianDate()));

        public string SelectedGregorianDate
        {
            get { return (string)GetValue(SelectedGregorianDateProperty); }
            set { SetValue(SelectedGregorianDateProperty, value); }
        }

        public ShamsiDatePicker()
        {
            InitializeComponent();
            UpdateDisplay();
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShamsiDatePicker picker)
            {
                picker.UpdateDisplay();
                picker.SelectedGregorianDate = ShamsiDateHelper.ShamsiToGregorian(picker.SelectedDate);
            }
        }

        private void UpdateDisplay()
        {
            if (DateTextBox != null)
            {
                DateTextBox.Text = ShamsiDateHelper.FormatForDisplay(SelectedDate);
            }
        }

        private void CalendarButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDatePickerDialog();
        }

        private void ShowDatePickerDialog()
        {
            var dialog = new Window
            {
                Title = "انتخاب تاریخ",
                Width = 350,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new System.Windows.Media.FontFamily("Tahoma")
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Current date display
            var currentDateLabel = new Label
            {
                Content = $"تاریخ فعلی: {ShamsiDateHelper.FormatForDisplay(SelectedDate)}",
                FontSize = 12,
                Margin = new Thickness(10, 10, 10, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(currentDateLabel, 0);
            grid.Children.Add(currentDateLabel);

            // Year selection
            var yearLabel = new Label { Content = "سال:", FontSize = 12, Margin = new Thickness(10, 5, 10, 5) };
            Grid.SetRow(yearLabel, 1);
            grid.Children.Add(yearLabel);

            var yearComboBox = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(yearComboBox, 1);
            Grid.SetColumn(yearComboBox, 1);

            var currentYear = DateTime.Now.Year;
            var persianYear = ShamsiDateHelper.FromShamsiString(SelectedDate).Year;
            for (int year = persianYear - 5; year <= persianYear + 5; year++)
            {
                yearComboBox.Items.Add(year);
            }
            yearComboBox.SelectedItem = persianYear;
            grid.Children.Add(yearComboBox);

            // Month selection
            var monthLabel = new Label { Content = "ماه:", FontSize = 12, Margin = new Thickness(10, 5, 10, 5) };
            Grid.SetRow(monthLabel, 2);
            grid.Children.Add(monthLabel);

            var monthComboBox = new ComboBox
            {
                Width = 120,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(monthComboBox, 2);
            Grid.SetColumn(monthComboBox, 1);

            var monthNames = ShamsiDateHelper.GetMonthNames();
            for (int i = 0; i < monthNames.Length; i++)
            {
                monthComboBox.Items.Add($"{i + 1:00} - {monthNames[i]}");
            }

            var currentMonth = ShamsiDateHelper.FromShamsiString(SelectedDate).Month;
            monthComboBox.SelectedIndex = currentMonth - 1;
            grid.Children.Add(monthComboBox);

            // Day selection
            var dayLabel = new Label { Content = "روز:", FontSize = 12, Margin = new Thickness(10, 5, 10, 5) };
            Grid.SetRow(dayLabel, 3);
            grid.Children.Add(dayLabel);

            var dayComboBox = new ComboBox
            {
                Width = 80,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(dayComboBox, 3);
            Grid.SetColumn(dayComboBox, 1);

            UpdateDayComboBox(dayComboBox, persianYear, currentMonth);
            var currentDay = ShamsiDateHelper.FromShamsiString(SelectedDate).Day;
            dayComboBox.SelectedItem = currentDay;
            grid.Children.Add(dayComboBox);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10, 10, 10, 10)
            };
            Grid.SetRow(buttonPanel, 4);

            var okButton = new Button
            {
                Content = "تأیید",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 5, 5, 5)
            };
            okButton.Click += (s, e) =>
            {
                var selectedYear = (int)yearComboBox.SelectedItem;
                var selectedMonth = monthComboBox.SelectedIndex + 1;
                var selectedDay = (int)dayComboBox.SelectedItem;
                
                SelectedDate = $"{selectedYear:0000}/{selectedMonth:00}/{selectedDay:00}";
                dialog.Close();
            };

            var cancelButton = new Button
            {
                Content = "لغو",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 5, 5, 5)
            };
            cancelButton.Click += (s, e) => dialog.Close();

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);

            // Update day combo when year or month changes
            yearComboBox.SelectionChanged += (s, e) => UpdateDayComboBox(dayComboBox, (int)yearComboBox.SelectedItem, monthComboBox.SelectedIndex + 1);
            monthComboBox.SelectionChanged += (s, e) => UpdateDayComboBox(dayComboBox, (int)yearComboBox.SelectedItem, monthComboBox.SelectedIndex + 1);

            dialog.Content = grid;
            dialog.ShowDialog();
        }

        private void UpdateDayComboBox(ComboBox dayComboBox, int year, int month)
        {
            dayComboBox.Items.Clear();
            
            // Get days in the month (Persian calendar)
            var daysInMonth = 31; // Default for most months
            if (month >= 1 && month <= 6)
                daysInMonth = 31;
            else if (month >= 7 && month <= 11)
                daysInMonth = 30;
            else if (month == 12)
            {
                // Check if it's a leap year
                var persianCalendar = new System.Globalization.PersianCalendar();
                var isLeapYear = persianCalendar.IsLeapYear(year);
                daysInMonth = isLeapYear ? 30 : 29;
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                dayComboBox.Items.Add(day);
            }
        }
    }
}
