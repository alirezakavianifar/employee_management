using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Shared.Models;

namespace ManagementApp.Converters
{
    public class ShiftToSlotListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Shift shift)
            {
                var slots = new List<ShiftSlotViewModel>();
                for (int i = 0; i < shift.Capacity; i++)
                {
                    slots.Add(new ShiftSlotViewModel { Shift = shift, Index = i });
                }
                return slots;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ShiftSlotViewModel
    {
        public Shift Shift { get; set; }
        public int Index { get; set; }
    }
}
