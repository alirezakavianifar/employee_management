using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shared.Models;

namespace ManagementApp.Views
{
    public partial class RoleEditDialog : Window
    {
        public string RoleId => RoleIdTextBox.Text.Trim();
        public string RoleName => RoleNameTextBox.Text.Trim();
        public string Description => DescriptionTextBox.Text.Trim();
        public string Color => GetSelectedColor();
        public int Priority => GetPriority();

        public RoleEditDialog()
        {
            InitializeComponent();
            Title = "افزودن نقش جدید";
            ColorComboBox.SelectionChanged += ColorComboBox_SelectionChanged;
        }

        public RoleEditDialog(Role role) : this()
        {
            Title = "ویرایش نقش";
            RoleIdTextBox.Text = role.RoleId;
            RoleIdTextBox.IsReadOnly = true; // Don't allow editing ID
            RoleNameTextBox.Text = role.Name;
            DescriptionTextBox.Text = role.Description;
            PriorityTextBox.Text = role.Priority.ToString();
            
            // Set color
            foreach (ComboBoxItem item in ColorComboBox.Items)
            {
                if (item.Tag?.ToString() == role.Color)
                {
                    ColorComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedColor = GetSelectedColor();
            ColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selectedColor));
        }

        private string GetSelectedColor()
        {
            if (ColorComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Tag?.ToString() ?? "#4CAF50";
            }
            return "#4CAF50";
        }

        private int GetPriority()
        {
            if (int.TryParse(PriorityTextBox.Text, out int priority))
            {
                return priority;
            }
            return 50;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (string.IsNullOrEmpty(RoleId))
            {
                MessageBox.Show("لطفاً شناسه نقش را وارد کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                RoleIdTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(RoleName))
            {
                MessageBox.Show("لطفاً نام نقش را وارد کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                RoleNameTextBox.Focus();
                return;
            }

            if (Priority < 0 || Priority > 1000)
            {
                MessageBox.Show("اولویت باید بین 0 تا 1000 باشد.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                PriorityTextBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
