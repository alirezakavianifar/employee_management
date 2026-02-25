using System;
using System.Globalization;

namespace Shared.Utils
{
    public static class ShamsiDateHelper
    {
        private static readonly PersianCalendar _persianCalendar = new();

        /// <summary>
        /// Converts a Gregorian DateTime to Shamsi date string
        /// </summary>
        /// <param name="date">Gregorian date</param>
        /// <returns>Shamsi date in format yyyy/MM/dd</returns>
        public static string ToShamsiString(DateTime date)
        {
            var year = _persianCalendar.GetYear(date);
            var month = _persianCalendar.GetMonth(date);
            var day = _persianCalendar.GetDayOfMonth(date);
            
            return $"{year:0000}/{month:00}/{day:00}";
        }

        /// <summary>
        /// Converts a Shamsi date string to Gregorian DateTime
        /// </summary>
        /// <param name="shamsiDate">Shamsi date in format yyyy/MM/dd or yyyy-MM-dd</param>
        /// <returns>Gregorian DateTime</returns>
        public static DateTime FromShamsiString(string shamsiDate)
        {
            if (string.IsNullOrEmpty(shamsiDate))
                return DateTime.Now;

            // Normalize the date format (replace - with /)
            var normalizedDate = shamsiDate.Replace("-", "/");
            
            var parts = normalizedDate.Split('/');
            if (parts.Length != 3)
                return DateTime.Now;

            if (int.TryParse(parts[0], out int year) &&
                int.TryParse(parts[1], out int month) &&
                int.TryParse(parts[2], out int day))
            {
                try
                {
                    return _persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
                }
                catch
                {
                    return DateTime.Now;
                }
            }

            return DateTime.Now;
        }

        /// <summary>
        /// Gets the current Shamsi date
        /// </summary>
        /// <returns>Current Shamsi date string</returns>
        public static string GetCurrentShamsiDate()
        {
            return ToShamsiString(DateTime.Now);
        }

        /// <summary>
        /// Gets the current Gregorian date in yyyy-MM-dd format
        /// </summary>
        /// <returns>Current Gregorian date string</returns>
        public static string GetCurrentGregorianDate()
        {
            return DateTime.Now.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Converts Gregorian date string to Shamsi
        /// </summary>
        /// <param name="gregorianDate">Gregorian date in yyyy-MM-dd format</param>
        /// <returns>Shamsi date string</returns>
        public static string GregorianToShamsi(string gregorianDate)
        {
            if (string.IsNullOrEmpty(gregorianDate))
                return GetCurrentShamsiDate();

            if (DateTime.TryParseExact(gregorianDate, "yyyy-MM-dd", null, DateTimeStyles.None, out DateTime date))
            {
                return ToShamsiString(date);
            }

            return GetCurrentShamsiDate();
        }

        /// <summary>
        /// Converts Shamsi date string to Gregorian
        /// </summary>
        /// <param name="shamsiDate">Shamsi date in yyyy/MM/dd format</param>
        /// <returns>Gregorian date string in yyyy-MM-dd format</returns>
        public static string ShamsiToGregorian(string shamsiDate)
        {
            if (string.IsNullOrEmpty(shamsiDate))
                return GetCurrentGregorianDate();

            var gregorianDate = FromShamsiString(shamsiDate);
            return gregorianDate.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Formats Shamsi date for display (English month names for UI).
        /// </summary>
        /// <param name="shamsiDate">Shamsi date string</param>
        /// <returns>Formatted date for display</returns>
        public static string FormatForDisplay(string shamsiDate)
        {
            if (string.IsNullOrEmpty(shamsiDate))
                return GetCurrentShamsiDate();

            var parts = shamsiDate.Replace("-", "/").Split('/');
            if (parts.Length == 3)
            {
                var year = parts[0];
                var month = parts[1];
                var day = parts[2];
                var monthNames = GetMonthNames();

                if (int.TryParse(month, out int monthNum) && monthNum >= 1 && monthNum <= 12)
                {
                    return $"{day} {monthNames[monthNum - 1]} {year}";
                }
            }

            return shamsiDate;
        }

        /// <summary>
        /// Gets Jalali month names in English for user-facing UI.
        /// </summary>
        /// <returns>Array of month names (Farvardin, Ordibehesht, ...)</returns>
        public static string[] GetMonthNames()
        {
            return new[]
            {
                "Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar",
                "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand"
            };
        }

        /// <summary>
        /// Gets Jalali weekday names in English for user-facing UI.
        /// </summary>
        /// <returns>Array of day names (Saturday, Sunday, ...)</returns>
        public static string[] GetDayNames()
        {
            return new[]
            {
                "Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"
            };
        }
    }
}
