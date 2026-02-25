using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shared.Models;
using Shared.Services;
using ManagementApp.Views;

namespace ManagementApp.Controls
{
    /// <summary>
    /// Label creation panel with archive and drag-drop support.
    /// </summary>
    public partial class LabelCreationPanel : UserControl
    {
        private LabelService? _labelService;
        private List<EmployeeLabel> _allLabels = new();
        private Point _dragStartPoint;

        /// <summary>
        /// Event raised when a label is created.
        /// </summary>
        public event EventHandler<EmployeeLabel>? LabelCreated;

        /// <summary>
        /// Event raised when a label is deleted from archive.
        /// </summary>
        public event EventHandler<string>? LabelDeleted;

        /// <summary>
        /// Event raised when a label drag operation starts.
        /// </summary>
        public event EventHandler<EmployeeLabel>? LabelDragStarted;

        public LabelCreationPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize the panel with a LabelService instance.
        /// </summary>
        public void Initialize(LabelService labelService)
        {
            _labelService = labelService;
            RefreshLabelList();
        }

        /// <summary>
        /// Refresh the label archive list from the service.
        /// </summary>
        public void RefreshLabelList()
        {
            if (_labelService == null) return;

            _allLabels = _labelService.GetAllLabels().OrderByDescending(l => l.CreatedAt).ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var searchText = SearchBox.Text?.Trim().ToLower() ?? "";
            
            var filteredLabels = string.IsNullOrEmpty(searchText)
                ? _allLabels
                : _allLabels.Where(l => l.Text.ToLower().Contains(searchText)).ToList();

            LabelArchiveList.ItemsSource = filteredLabels;
            
            // Show/hide empty state
            EmptyStateText.Visibility = filteredLabels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CreateLabelButton_Click(object sender, RoutedEventArgs e)
        {
            CreateLabel();
        }

        private void LabelTextInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateLabel();
                e.Handled = true;
            }
        }

        private void CreateLabel()
        {
            if (_labelService == null) return;

            var text = LabelTextInput.Text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("Please enter label text.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var label = _labelService.CreateLabel(text);
            LabelTextInput.Text = "";
            RefreshLabelList();
            
            LabelCreated?.Invoke(this, label);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void LabelItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void LabelItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            // Check if we've moved enough to start a drag
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is Border border && border.Tag is string labelId)
                {
                    var label = _allLabels.FirstOrDefault(l => l.LabelId == labelId);
                    if (label != null)
                    {
                        LabelDragStarted?.Invoke(this, label);
                        
                        // Create drag data
                        var dragData = new DataObject("EmployeeLabel", label);
                        DragDrop.DoDragDrop(border, dragData, DragDropEffects.Copy);
                    }
                }
            }
        }

        private void DeleteFromArchive_Click(object sender, RoutedEventArgs e)
        {
            if (_labelService == null) return;

            if (sender is MenuItem menuItem && menuItem.Tag is string labelId)
            {
                var label = _allLabels.FirstOrDefault(l => l.LabelId == labelId);
                if (label == null) return;

                var result = MessageBox.Show(
                    $"Delete label \"{label.Text}\" from archive?\n\nNote: Labels already assigned to employees will remain.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _labelService.DeleteLabelFromArchive(labelId);
                    RefreshLabelList();
                    LabelDeleted?.Invoke(this, labelId);
                }
            }
        }

        /// <summary>
        /// Gets a label from the archive by ID.
        /// </summary>
        public EmployeeLabel? GetLabel(string labelId)
        {
            return _allLabels.FirstOrDefault(l => l.LabelId == labelId);
        }

        /// <summary>
        /// Gets all labels from the archive.
        /// </summary>
        public List<EmployeeLabel> GetAllLabels()
        {
            return _allLabels.ToList();
        }

        private void LabelArchive_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("EmployeeLabelRemove"))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void LabelArchive_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("EmployeeLabelRemove"))
                return;
            var data = e.Data.GetData("EmployeeLabelRemove") as RemoveLabelDragData;
            if (data == null || MainWindow.Instance?.LabelService == null || MainWindow.Instance?.Controller == null)
                return;
            MainWindow.Instance.LabelService.RemoveLabelFromEmployee(data.Employee, data.LabelId);
            MainWindow.Instance.Controller.SaveData();
            MainWindow.Instance.RefreshShiftSlots();
            e.Handled = true;
        }
    }
}
