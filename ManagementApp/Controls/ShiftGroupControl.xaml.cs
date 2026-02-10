using System;
using System.Windows;
using System.Windows.Controls;
using ManagementApp.Views;
using Shared.Models;
using ManagementApp.Controllers;

namespace ManagementApp.Controls
{
    public partial class ShiftGroupControl : UserControl
    {
        public ShiftGroupControl()
        {
            InitializeComponent();
        }

        private void ShiftSlot_ItemDropped(object sender, ShiftDropEventArgs e)
        {
            if (DataContext is not ShiftGroup group) return;
            if (MainWindow.Instance?.Controller == null) return;

            var controller = MainWindow.Instance.Controller;
            string groupId = group.GroupId;
            string shiftType = e.ShiftType;
            int slotIndex = e.SlotIndex;

            if (e.ItemType == DropItemType.Employee && e.DroppedItem is Employee employee)
            {
                // Assign Employee
                var result = controller.AssignEmployeeToShift(employee, shiftType, slotIndex, groupId);

                if (!result.Success)
                {
                    if (result.Conflict != null)
                    {
                        // Handle formatting conflict message 
                        // Simplified handling: just show error or auto-resolve if needed.
                        // Existing MainWindow logic handles this with dialogs usually.
                        // We will try to resolve simple conflicts or show message.
                        
                        string message = result.ErrorMessage ?? "Error assigning to shift";
                        if (result.Conflict.Type == ConflictType.DifferentGroup)
                        {
                             message = $"Employee is already assigned to group {result.Conflict.CurrentGroupName}. Do you want to move them?";
                             if (MessageBox.Show(message, "Conflict", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                             {
                                 if (controller.RemoveEmployeeFromPreviousAssignment(employee, result.Conflict, groupId))
                                 {
                                     // Retry assignment
                                     controller.AssignEmployeeToShift(employee, shiftType, slotIndex, groupId);
                                 }
                             }
                             return;
                        }
                        else if (result.Conflict.Type == ConflictType.DifferentShift)
                        {
                             message = $"Employee is already on the {result.Conflict.CurrentShiftType} shift. Do you want to move them?";
                             if (MessageBox.Show(message, "Conflict", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                             {
                                 if (controller.RemoveEmployeeFromPreviousAssignment(employee, result.Conflict, groupId))
                                 {
                                     // Retry assignment
                                     controller.AssignEmployeeToShift(employee, shiftType, slotIndex, groupId);
                                 }
                             }
                             return;
                        }

                        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                         MessageBox.Show(result.ErrorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else if (e.ItemType == DropItemType.StatusCard && e.DroppedItem is StatusCard card)
            {
                // Assign Status Card
                controller.AssignStatusCardToShift(card.StatusCardId, groupId, shiftType, slotIndex);
            }
            else if (e.ItemType == DropItemType.EmployeeLabel && e.DroppedItem is EmployeeLabel label)
            {
                // Assign label to employee in this slot
                var slotControl = sender as ShiftSlotControl;
                var targetEmployee = slotControl?.ShiftData?.GetEmployeeAtSlot(slotControl.SlotIndex);
                if (targetEmployee != null && MainWindow.Instance?.LabelService != null)
                {
                    MainWindow.Instance.LabelService.AssignLabelToEmployee(targetEmployee, label.LabelId);
                    controller.SaveData();
                    MainWindow.Instance.RefreshShiftSlots();
                }
            }
        }

        private void ShiftSlot_RemoveLabelRequested(object sender, RemoveLabelRequestedEventArgs e)
        {
            if (MainWindow.Instance?.LabelService == null || MainWindow.Instance?.Controller == null)
                return;
            MainWindow.Instance.LabelService.RemoveLabelFromEmployee(e.Employee, e.LabelId);
            MainWindow.Instance.Controller.SaveData();
            MainWindow.Instance.RefreshShiftSlots();
        }

        private void Header_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is not ShiftGroup group) return;
            // MainWindow instance check safety
            if (MainWindow.Instance == null || MainWindow.Instance.Controller == null) return;
            
            if (e.Data.GetDataPresent(typeof(Employee)))
            {
                 var employee = e.Data.GetData(typeof(Employee)) as Employee;
                 if (employee != null)
                 {
                     MainWindow.Instance.Controller.AssignSupervisor(group.GroupId, employee.EmployeeId);
                 }
            }
        }

        private void EditGroup_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ShiftGroup group) return;
            
            var dialog = new ShiftGroupEditDialog(group);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                // Explicitly call UpdateShiftGroup to save changes
                if (MainWindow.Instance != null && MainWindow.Instance.Controller != null)
                {
                    MainWindow.Instance.Controller.UpdateShiftGroup(
                        group.GroupId,
                        dialog.Name,
                        dialog.Description,
                        null, // supervisorId (not used in this dialog)
                        dialog.Color,
                        dialog.MorningCapacity,
                        dialog.AfternoonCapacity,
                        dialog.NightCapacity,
                        dialog.IsGroupActive,
                        dialog.MorningForemanId,
                        dialog.AfternoonForemanId,
                        dialog.NightForemanId
                    );
                    
                    // Refresh UI
                    MainWindow.Instance.LoadShifts();
                }
            }
        }
    }
}
