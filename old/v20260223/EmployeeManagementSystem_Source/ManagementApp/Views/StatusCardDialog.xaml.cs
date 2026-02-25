using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ManagementApp.Controllers;
using Shared.Models;

namespace ManagementApp.Views
{
    public partial class StatusCardDialog : Window
    {
        private readonly MainController _controller;
        private List<StatusCard> _allCards = new();
        private bool _searchBoxHasPlaceholder = true;

        public StatusCardDialog(MainController controller)
        {
            InitializeComponent();
            _controller = controller;
            LoadCards();
        }

        private void LoadCards()
        {
            _allCards = _controller.GetAllStatusCards();
            FilterCards();
        }

        private void FilterCards()
        {
            var searchText = _searchBoxHasPlaceholder ? "" : CardSearchBox.Text.Trim().ToLower();
            
            var filtered = string.IsNullOrEmpty(searchText) 
                ? _allCards 
                : _allCards.Where(c => 
                    c.Name.ToLower().Contains(searchText) || 
                    c.StatusCardId.ToLower().Contains(searchText)).ToList();
            
            CardListBox.ItemsSource = filtered;
        }

        private void CardSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_searchBoxHasPlaceholder)
            {
                FilterCards();
            }
        }

        private void CardSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_searchBoxHasPlaceholder)
            {
                CardSearchBox.Text = "";
                CardSearchBox.Foreground = System.Windows.Media.Brushes.Black;
                _searchBoxHasPlaceholder = false;
            }
        }

        private void CardSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CardSearchBox.Text))
            {
                CardSearchBox.Text = "Search cards...";
                CardSearchBox.Foreground = System.Windows.Media.Brushes.Gray;
                _searchBoxHasPlaceholder = true;
            }
        }

        private void CardListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var isSelected = CardListBox.SelectedItem != null;
            EditCardButton.IsEnabled = isSelected;
            DeleteCardButton.IsEnabled = isSelected;
        }

        private void AddCardButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new StatusCardEditDialog();
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                var success = _controller.AddStatusCard(
                    dialog.StatusCardId,
                    dialog.StatusCardName,
                    dialog.SelectedColor,
                    dialog.SelectedTextColor);

                if (success)
                {
                    LoadCards();
                    MessageBox.Show("Status card was added successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Error adding status card. The ID may already exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditCardButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedCard = CardListBox.SelectedItem as StatusCard;
            if (selectedCard == null)
            {
                MessageBox.Show("Please select a card", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new StatusCardEditDialog(selectedCard);
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true)
            {
                var success = _controller.UpdateStatusCard(
                    selectedCard.StatusCardId,
                    dialog.StatusCardName,
                    dialog.SelectedColor,
                    dialog.SelectedTextColor);

                if (success)
                {
                    LoadCards();
                    MessageBox.Show("Status card was updated successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Error updating status card", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteCardButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedCard = CardListBox.SelectedItem as StatusCard;
            if (selectedCard == null)
            {
                MessageBox.Show("Please select a card", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete card '{selectedCard.Name}'?", 
                "Confirm delete", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var success = _controller.DeleteStatusCard(selectedCard.StatusCardId);
                if (success)
                {
                    LoadCards();
                    MessageBox.Show("Status card was deleted successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Error deleting status card", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
