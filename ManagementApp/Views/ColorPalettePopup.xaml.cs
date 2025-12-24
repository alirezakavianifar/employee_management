using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ManagementApp.Views
{
    public partial class ColorPalettePopup : Window
    {
        public string SelectedColor { get; private set; } = "#4CAF50";

        public ColorPalettePopup(string currentColor = "#4CAF50")
        {
            InitializeComponent();
            SelectedColor = currentColor;
            UpdatePreview(currentColor);
            HighlightSelectedColor(currentColor);
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string colorHex)
            {
                SelectedColor = colorHex;
                UpdatePreview(colorHex);
                HighlightSelectedColor(colorHex);
            }
        }

        private void UpdatePreview(string colorHex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                ColorPreviewBorder.Background = new SolidColorBrush(color);
                ColorHexTextBlock.Text = colorHex;
            }
            catch
            {
                // If color conversion fails, use default
                ColorPreviewBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                ColorHexTextBlock.Text = "#4CAF50";
            }
        }

        private void HighlightSelectedColor(string colorHex)
        {
            // Reset all buttons
            foreach (var child in ColorGrid.Children)
            {
                if (child is Button btn)
                {
                    if (btn.Content is Border border)
                    {
                        border.BorderBrush = Brushes.Gray;
                        border.BorderThickness = new Thickness(2);
                    }
                }
            }

            // Highlight the selected color
            foreach (var child in ColorGrid.Children)
            {
                if (child is Button btn && btn.Tag?.ToString() == colorHex)
                {
                    if (btn.Content is Border border)
                    {
                        border.BorderBrush = Brushes.Black;
                        border.BorderThickness = new Thickness(3);
                    }
                    break;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
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

