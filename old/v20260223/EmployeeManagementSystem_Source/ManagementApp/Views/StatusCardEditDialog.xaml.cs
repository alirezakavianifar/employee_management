using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shared.Models;

namespace ManagementApp.Views
{
    public partial class StatusCardEditDialog : Window
    {
        private bool _isEditMode = false;

        public StatusCardEditDialog()
        {
            InitializeComponent();
            UpdatePreview();
        }

        public StatusCardEditDialog(StatusCard existingCard) : this()
        {
            _isEditMode = true;
            Title = "Edit status card";

            StatusCardIdTextBox.Text = existingCard.StatusCardId;
            StatusCardIdTextBox.IsEnabled = false; // Can't change ID when editing

            StatusCardNameTextBox.Text = existingCard.Name;

            // Select the matching color in the combo box
            SelectColorInComboBox(ColorComboBox, existingCard.Color);
            SelectColorInComboBox(TextColorComboBox, existingCard.TextColor);

            UpdatePreview();
        }

        private void SelectColorInComboBox(ComboBox comboBox, string colorHex)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString()?.ToUpper() == colorHex.ToUpper())
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            // If not found, keep the first item selected
        }

        public string StatusCardId => StatusCardIdTextBox.Text.Trim();
        public string StatusCardName => StatusCardNameTextBox.Text.Trim();
        
        public string SelectedColor
        {
            get
            {
                var selected = ColorComboBox.SelectedItem as ComboBoxItem;
                return selected?.Tag?.ToString() ?? "#FF5722";
            }
        }
        
        public string SelectedTextColor
        {
            get
            {
                var selected = TextColorComboBox.SelectedItem as ComboBoxItem;
                return selected?.Tag?.ToString() ?? "#FFFFFF";
            }
        }

        private void ColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void TextColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (ColorPreview != null)
            {
                try
                {
                    ColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedColor));
                }
                catch { ColorPreview.Background = new SolidColorBrush(Colors.Orange); }
            }

            if (TextColorPreview != null)
            {
                try
                {
                    TextColorPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedTextColor));
                }
                catch { TextColorPreview.Background = new SolidColorBrush(Colors.White); }
            }

            if (CardPreview != null)
            {
                try
                {
                    CardPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedColor));
                }
                catch { CardPreview.Background = new SolidColorBrush(Colors.Orange); }
            }

            if (CardPreviewText != null)
            {
                try
                {
                    CardPreviewText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedTextColor));
                }
                catch { CardPreviewText.Foreground = new SolidColorBrush(Colors.White); }

                // Update preview text with actual name if available
                if (!string.IsNullOrWhiteSpace(StatusCardNameTextBox?.Text))
                {
                    CardPreviewText.Text = StatusCardNameTextBox.Text;
                }
                else
                {
                    CardPreviewText.Text = "Sample text";
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(StatusCardId))
            {
                MessageBox.Show("Please enter the card ID", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusCardIdTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(StatusCardName))
            {
                MessageBox.Show("Please enter the card name", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusCardNameTextBox.Focus();
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
