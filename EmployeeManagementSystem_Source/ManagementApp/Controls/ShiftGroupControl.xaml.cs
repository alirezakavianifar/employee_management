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
            int slotIndex = 0; // Single slot per shift requirement

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
                var targetEmployee = slotControl?.ShiftData?.GetEmployeeAtSlot(0);
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
            var controller = MainWindow.Instance?.Controller;
            if (controller == null) return;

            if (e.Data.GetDataPresent(typeof(Employee)))
            {
                var employee = e.Data.GetData(typeof(Employee)) as Employee;
                if (employee != null)
                {
                    controller.AssignSupervisor(group.GroupId, employee.EmployeeId);
                }
            }
        }
    }
}
