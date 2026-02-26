using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shared.Models; // For Employee, Shift
using ManagementApp.Services; // If needed
using ManagementApp.Views; // For MainWindow

namespace ManagementApp.Controls
{
    public partial class ShiftSlotControl : UserControl
    {
        // Custom Event for Drop
        public event EventHandler<ShiftDropEventArgs> OnItemDropped;
        public event EventHandler<ShiftDragStartEventArgs> OnItemDragStarted;
        /// <summary>Raised when user requests removal of a label from the employee in this slot.</summary>
        public event EventHandler<RemoveLabelRequestedEventArgs>? OnRemoveLabelRequested;

        public static readonly DependencyProperty ShiftDataProperty =
            DependencyProperty.Register("ShiftData", typeof(Shift), typeof(ShiftSlotControl), 
                new PropertyMetadata(null, OnShiftDataChanged));

        public static readonly DependencyProperty ShiftTypeProperty =
            DependencyProperty.Register("ShiftType", typeof(string), typeof(ShiftSlotControl), new PropertyMetadata(""));

        public static readonly DependencyProperty ShiftTitleProperty =
            DependencyProperty.Register("ShiftTitle", typeof(string), typeof(ShiftSlotControl), 
                new PropertyMetadata("", OnTitleChanged));

        public static readonly DependencyProperty ShiftColorProperty =
            DependencyProperty.Register("ShiftColor", typeof(Brush), typeof(ShiftSlotControl), 
                new PropertyMetadata(Brushes.Gray, OnColorChanged));

        public static readonly DependencyProperty SlotIndexProperty =
            DependencyProperty.Register("SlotIndex", typeof(int), typeof(ShiftSlotControl), 
                new PropertyMetadata(0, OnSlotIndexChanged));

        public Shift ShiftData
        {
            get { return (Shift)GetValue(ShiftDataProperty); }
            set { SetValue(ShiftDataProperty, value); }
        }

        public string ShiftType
        {
            get { return (string)GetValue(ShiftTypeProperty); }
            set { SetValue(ShiftTypeProperty, value); }
        }

        public string ShiftTitle
        {
            get { return (string)GetValue(ShiftTitleProperty); }
            set { SetValue(ShiftTitleProperty, value); }
        }

        public Brush ShiftColor
        {
            get { return (Brush)GetValue(ShiftColorProperty); }
            set { SetValue(ShiftColorProperty, value); }
        }

        public int SlotIndex
        {
            get { return (int)GetValue(SlotIndexProperty); }
            set { SetValue(SlotIndexProperty, value); }
        }

        public static readonly DependencyProperty BadgeSizeProperty =
            DependencyProperty.Register("BadgeSize", typeof(double), typeof(ShiftSlotControl), new PropertyMetadata(250.0));

        public double BadgeSize
        {
            get { return (double)GetValue(BadgeSizeProperty); }
            set { SetValue(BadgeSizeProperty, value); }
        }

        public ShiftSlotControl()
        {
            InitializeComponent();
        }

        private static void OnShiftDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ShiftSlotControl)d;
            control.UpdateUI();
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ShiftSlotControl)d;
            var val = e.NewValue as string;
            control.TitleBlock.Text = val;
            control.TitleBlock.Visibility = string.IsNullOrEmpty(val) ? Visibility.Collapsed : Visibility.Visible;
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ShiftSlotControl)d;
            control.TitleBlock.Foreground = e.NewValue as Brush;
        }

        private static void OnSlotIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ShiftSlotControl)d;
            control.UpdateUI();
        }

        public void UpdateUI()
        {
            // Reset Visibilities
            EmployeeCard.Visibility = Visibility.Collapsed;
            StatusCard.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;

            if (ShiftData == null) return;

            // Check for Status Card (at SlotIndex)
            var statusCardId = ShiftData.GetStatusCardAtSlot(SlotIndex);
            if (!string.IsNullOrEmpty(statusCardId))
            {
                // Render Status Card
                // TODO: Need to lookup StatusCard details (Name/Color) using ID.
                // For now, we might need a service or pass the StatusCard object directly if possible.
                // Assuming we can't easily get the full object here without a lookup service.
                // We might need to inject dependencies or use a singleton lookup.
                
                // Temporary: Display ID or generic info if lookup fails
                // Ideally, we passed the StatusCard object or have a way to find it.
                // Let's assume for now we just show the ID or need to fetch it.
                // Better approach: Bind StatusCards dictionary to the MainWindow/Control?
                // Or use the static StatusCardService if available?
                
                // Accessing pure status card via ID:
                // We will try to find it in the global list if easier, 
                // OR we'll trust that the ID contains info (it doesn't).
                
                // Accessing StatusCards via MainWindow singleton
                StatusCard card = null;
                if (MainWindow.Instance?.Controller?.StatusCards != null)
                {
                    MainWindow.Instance.Controller.StatusCards.TryGetValue(statusCardId, out card);
                }

                if (card != null)
                {
                    StatusText.Text = card.Name;
                    try 
                    {
                        StatusCard.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(card.Color));
                    }
                    catch
                    {
                        StatusCard.Background = Brushes.Gray;
                    }
                }
                else
                {
                    StatusText.Text = Shared.Utils.ResourceManager.GetString("status_card_fallback", "Status card");
                    StatusCard.Background = Brushes.Gray;
                }

                StatusCard.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
                return;
            }

            // Check for Employee (at SlotIndex)
            var employee = ShiftData.GetEmployeeAtSlot(SlotIndex);
            if (employee != null)
            {
                // Render Employee
                EmployeeNameText.Text = employee.FullName;
                EmployeeIdText.Text = employee.PersonnelId;
                ShieldTypeText.Text = employee.ShowShield ? "Shield: Visible" : "Shield: Hidden";
                ShieldColorText.Text = $"Color: {employee.ShieldColor}";
                ShieldColorChip.Background = GetShieldColorBrush(employee.ShieldColor);

                var stickerCount = employee.StickerPaths?.Count ?? 0;
                StickerInfoText.Text = $"Stickers: {stickerCount}";

                var hasMedal = !string.IsNullOrWhiteSpace(employee.MedalBadgePath);
                MedalInfoText.Text = hasMedal ? "Medal: Set" : "Medal: None";
                MedalInfoText.ToolTip = hasMedal ? System.IO.Path.GetFileName(employee.MedalBadgePath) : null;
                
                // Photo
                // Using the converter logic manually or binding if possible? 
                // Creating ImageSource manually:
                 try
                {
                    if (!string.IsNullOrEmpty(employee.PhotoPath) && System.IO.File.Exists(employee.PhotoPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(employee.PhotoPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        EmployeePhotoBrush.ImageSource = bitmap;
                    }
                    else
                    {
                         // Default placeholder
                        EmployeePhotoBrush.ImageSource = new BitmapImage(new Uri("pack://application:,,,/ManagementApp;component/Resources/user_placeholder.png")); 
                        // Note: URI subject to validation
                    }
                }
                catch
                {
                     // Fallback
                }

                // Labels
                LabelsList.ItemsSource = employee.Labels;

                EmployeeCard.Visibility = Visibility.Visible;
                EmptyState.Visibility = Visibility.Collapsed;
            }
        }

        private Brush GetShieldColorBrush(string shieldColor)
        {
            if (string.IsNullOrWhiteSpace(shieldColor))
                return Brushes.SteelBlue;

            try
            {
                var converted = new BrushConverter().ConvertFromString(shieldColor);
                if (converted is Brush brush)
                    return brush;
            }
            catch
            {
            }

            return shieldColor.Trim().ToLowerInvariant() switch
            {
                "red" => Brushes.IndianRed,
                "blue" => Brushes.SteelBlue,
                "yellow" => Brushes.Goldenrod,
                "black" => Brushes.Black,
                "orange" => Brushes.DarkOrange,
                "green" => Brushes.ForestGreen,
                "gray" => Brushes.Gray,
                _ => Brushes.SteelBlue
            };
        }

        private void Border_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Employee)) || e.Data.GetDataPresent(typeof(StatusCard)))
            {
                e.Effects = DragDropEffects.Move | DragDropEffects.Copy;
                e.Handled = true;

                // Visual feedback
                MainBorder.Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 255));
            }
            else if (e.Data.GetDataPresent("EmployeeLabel") && ShiftData?.GetEmployeeAtSlot(SlotIndex) != null)
            {
                // Allow label drop only when slot has an employee
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                MainBorder.Background = new SolidColorBrush(Color.FromArgb(20, 0, 128, 0));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        
        private void Border_DragLeave(object sender, DragEventArgs e)
        {
             MainBorder.Background = Brushes.Transparent;
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            MainBorder.Background = Brushes.Transparent;

            if (e.Data.GetDataPresent(typeof(Employee)))
            {
                var employee = e.Data.GetData(typeof(Employee)) as Employee;
                OnItemDropped?.Invoke(this, new ShiftDropEventArgs 
                { 
                    ShiftType = this.ShiftType,
                    DroppedItem = employee,
                    ItemType = DropItemType.Employee,
                    SlotIndex = this.SlotIndex
                });
            }
            else if (e.Data.GetDataPresent(typeof(StatusCard)))
            {
                var card = e.Data.GetData(typeof(StatusCard)) as StatusCard;
                OnItemDropped?.Invoke(this, new ShiftDropEventArgs 
                { 
                    ShiftType = this.ShiftType,
                    DroppedItem = card,
                    ItemType = DropItemType.StatusCard,
                    SlotIndex = this.SlotIndex
                });
            }
            else if (e.Data.GetDataPresent("EmployeeLabel") && ShiftData?.GetEmployeeAtSlot(SlotIndex) != null)
            {
                var label = e.Data.GetData("EmployeeLabel") as EmployeeLabel;
                if (label != null)
                {
                    OnItemDropped?.Invoke(this, new ShiftDropEventArgs 
                    { 
                        ShiftType = this.ShiftType,
                        DroppedItem = label,
                        ItemType = DropItemType.EmployeeLabel,
                        SlotIndex = this.SlotIndex
                    });
                }
            }
        }

        private Point _dragStartPoint;

        private void Element_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
             _dragStartPoint = e.GetPosition(null);
        }

        private void Element_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && ShiftData != null)
            {
                var currentPoint = e.GetPosition(null);
                var diff = _dragStartPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var employee = ShiftData.GetEmployeeAtSlot(SlotIndex);
                    if (employee != null)
                    {
                        var dragData = new DataObject(typeof(Employee), employee);
                        dragData.SetData("ShiftType", this.ShiftType);
                        DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
                    }
                    else
                    {
                        var statusCardId = ShiftData.GetStatusCardAtSlot(SlotIndex);
                        if (!string.IsNullOrEmpty(statusCardId))
                        {
                            StatusCard card = null;
                            if (MainWindow.Instance?.Controller?.StatusCards != null)
                            {
                                MainWindow.Instance.Controller.StatusCards.TryGetValue(statusCardId, out card);
                            }
                            if (card != null)
                            {
                                var dragData = new DataObject(typeof(StatusCard), card);
                                dragData.SetData("ShiftType", this.ShiftType);
                                DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
                            }
                        }
                    }
                }
            }
        }

        private void LabelRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.MenuItem menuItem)
                return;
            var label = menuItem.DataContext as EmployeeLabel;
            var employee = ShiftData?.GetEmployeeAtSlot(SlotIndex);
            if (label != null && employee != null)
                OnRemoveLabelRequested?.Invoke(this, new RemoveLabelRequestedEventArgs(employee, label.LabelId));
        }

        private Point _labelDragStartPoint;

        private void Label_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _labelDragStartPoint = e.GetPosition(null);
        }

        private void Label_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;
            var currentPosition = e.GetPosition(null);
            if (Math.Abs(currentPosition.X - _labelDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - _labelDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;
            if (sender is not FrameworkElement fe || fe.DataContext is not EmployeeLabel label)
                return;
            var employee = ShiftData?.GetEmployeeAtSlot(SlotIndex);
            if (employee == null)
                return;
            var dragData = new DataObject("EmployeeLabelRemove", new RemoveLabelDragData(employee, label.LabelId));
            DragDrop.DoDragDrop(fe, dragData, DragDropEffects.Move);
        }
    }

    /// <summary>Drag data when dragging a label off an employee to remove it (e.g. drop on archive).</summary>
    public class RemoveLabelDragData
    {
        public Employee Employee { get; }
        public string LabelId { get; }
        public RemoveLabelDragData(Employee employee, string labelId)
        {
            Employee = employee;
            LabelId = labelId;
        }
    }

    public class RemoveLabelRequestedEventArgs : EventArgs
    {
        public Employee Employee { get; }
        public string LabelId { get; }
        public RemoveLabelRequestedEventArgs(Employee employee, string labelId)
        {
            Employee = employee;
            LabelId = labelId;
        }
    }

    public class ShiftDropEventArgs : EventArgs
    {
        public string ShiftType { get; set; }
        public object DroppedItem { get; set; }
        public DropItemType ItemType { get; set; }
        public int SlotIndex { get; set; }
    }

    public enum DropItemType
    {
        Employee,
        StatusCard,
        EmployeeLabel
    }
    
    public class ShiftDragStartEventArgs : EventArgs
    {
         // TODO for full interaction
    }
}
