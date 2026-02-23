using System;
using System.Windows;
using System.Windows.Controls;
using Shared.Utils;

namespace ManagementApp.Controls
{
    public partial class GeorgianDatePicker : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register("SelectedDate", typeof(string), typeof(GeorgianDatePicker),
                new PropertyMetadata(GeorgianDateHelper.GetCurrentGeorgianDate(), OnSelectedDateChanged));

        public string SelectedDate
        {
            get { return (string)GetValue(SelectedDateProperty); }
            set { SetValue(SelectedDateProperty, value); }
        }

        public static readonly DependencyProperty SelectedGregorianDateProperty =
            DependencyProperty.Register("SelectedGregorianDate", typeof(string), typeof(GeorgianDatePicker),
                new PropertyMetadata(GeorgianDateHelper.GetCurrentGregorianDate()));

        public string SelectedGregorianDate
        {
            get { return (string)GetValue(SelectedGregorianDateProperty); }
            set { SetValue(SelectedGregorianDateProperty, value); }
        }

        public GeorgianDatePicker()
        {
            InitializeComponent();
            UpdateDisplay();
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GeorgianDatePicker picker)
            {
                picker.UpdateDisplay();
                picker.SelectedGregorianDate = GeorgianDateHelper.GeorgianToGregorian(picker.SelectedDate);
            }
        }

        private void UpdateDisplay()
        {
            if (DateTextBox != null)
            {
                DateTextBox.Text = GeorgianDateHelper.FormatForDisplay(SelectedDate);
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
                Title = "Select Date",
                Width = 350,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                FlowDirection = FlowDirection.LeftToRight,
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
                Content = $"Current Date: {GeorgianDateHelper.FormatForDisplay(SelectedDate)}",
                FontSize = 12,
                Margin = new Thickness(10, 10, 10, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(currentDateLabel, 0);
            grid.Children.Add(currentDateLabel);

            // Year selection
            var yearLabel = new Label { Content = "Year:", FontSize = 12, Margin = new Thickness(10, 5, 10, 5) };
            Grid.SetRow(yearLabel, 1);
            grid.Children.Add(yearLabel);

            var yearComboBox = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(yearComboBox, 1);
            Grid.SetColumn(yearComboBox, 1);

            // Extract Georgian year directly from the date string
            var georgianYear = GetGeorgianYearFromString(SelectedDate);
            for (int year = georgianYear - 5; year <= georgianYear + 5; year++)
            {
                yearComboBox.Items.Add(year);
            }
            yearComboBox.SelectedItem = georgianYear;
            grid.Children.Add(yearComboBox);

            // Month selection
            var monthLabel = new Label { Content = "Month:", FontSize = 12, Margin = new Thickness(10, 5, 10, 5) };
            Grid.SetRow(monthLabel, 2);
            grid.Children.Add(monthLabel);

            var monthComboBox = new ComboBox
            {
                Width = 120,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(monthComboBox, 2);
            Grid.SetColumn(monthComboBox, 1);

            var monthNames = GeorgianDateHelper.GetMonthNames();
            for (int i = 0; i < monthNames.Length; i++)
            {
                monthComboBox.Items.Add($"{i + 1:00} - {monthNames[i]}");
            }

            var currentMonth = GetGeorgianMonthFromString(SelectedDate);
            monthComboBox.SelectedIndex = currentMonth - 1;
            grid.Children.Add(monthComboBox);

            // Day selection
            var dayLabel = new Label { Content = "Day:", FontSize = 12, Margin = new Thickness(10, 5, 10, 5) };
            Grid.SetRow(dayLabel, 3);
            grid.Children.Add(dayLabel);

            var dayComboBox = new ComboBox
            {
                Width = 80,
                Margin = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(dayComboBox, 3);
            Grid.SetColumn(dayComboBox, 1);

            UpdateDayComboBox(dayComboBox, georgianYear, currentMonth);
            var currentDay = GetGeorgianDayFromString(SelectedDate);
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
                Content = "OK",
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
                Content = "Cancel",
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

        private int GetGeorgianYearFromString(string georgianDate)
        {
            if (string.IsNullOrEmpty(georgianDate))
                return GetCurrentGeorgianYear();

            var normalizedDate = georgianDate.Replace("-", "/");
            var parts = normalizedDate.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[0], out int year))
            {
                return year; // This is the Georgian year from the string
            }

            return GetCurrentGeorgianYear();
        }

        private int GetGeorgianMonthFromString(string georgianDate)
        {
            if (string.IsNullOrEmpty(georgianDate))
                return GetCurrentGeorgianMonth();

            var normalizedDate = georgianDate.Replace("-", "/");
            var parts = normalizedDate.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[1], out int month))
            {
                return month; // This is the Georgian month from the string
            }

            return GetCurrentGeorgianMonth();
        }

        private int GetGeorgianDayFromString(string georgianDate)
        {
            if (string.IsNullOrEmpty(georgianDate))
                return GetCurrentGeorgianDay();

            var normalizedDate = georgianDate.Replace("-", "/");
            var parts = normalizedDate.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[2], out int day))
            {
                return day; // This is the Georgian day from the string
            }

            return GetCurrentGeorgianDay();
        }

        private int GetCurrentGeorgianYear()
        {
            var currentGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
            var parts = currentGeorgian.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[0], out int year))
            {
                return year;
            }
            return DateTime.Now.Year; // Default fallback
        }

        private int GetCurrentGeorgianMonth()
        {
            var currentGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
            var parts = currentGeorgian.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[1], out int month))
            {
                return month;
            }
            return DateTime.Now.Month; // Default fallback
        }

        private int GetCurrentGeorgianDay()
        {
            var currentGeorgian = GeorgianDateHelper.GetCurrentGeorgianDate();
            var parts = currentGeorgian.Split('/');
            if (parts.Length == 3 && int.TryParse(parts[2], out int day))
            {
                return day;
            }
            return DateTime.Now.Day; // Default fallback
        }

        private void UpdateDayComboBox(ComboBox dayComboBox, int year, int month)
        {
            dayComboBox.Items.Clear();
            
            // Get days in the month (Georgian calendar)
            var daysInMonth = DateTime.DaysInMonth(year, month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                dayComboBox.Items.Add(day);
            }
        }
    }
}
