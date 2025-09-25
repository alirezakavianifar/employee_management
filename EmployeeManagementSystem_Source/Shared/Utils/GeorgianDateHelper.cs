using System;
using System.Globalization;

namespace Shared.Utils
{
    public static class GeorgianDateHelper
    {
        /// <summary>
        /// Converts a DateTime to Georgian date string
        /// </summary>
        /// <param name="date">DateTime</param>
        /// <returns>Georgian date in format yyyy/MM/dd</returns>
        public static string ToGeorgianString(DateTime date)
        {
            return date.ToString("yyyy/MM/dd");
        }

        /// <summary>
        /// Converts a Georgian date string to DateTime
        /// </summary>
        /// <param name="georgianDate">Georgian date in format yyyy/MM/dd or yyyy-MM-dd</param>
        /// <returns>DateTime</returns>
        public static DateTime FromGeorgianString(string georgianDate)
        {
            if (string.IsNullOrEmpty(georgianDate))
                return DateTime.Now;

            // Normalize the date format (replace - with /)
            var normalizedDate = georgianDate.Replace("-", "/");
            
            var parts = normalizedDate.Split('/');
            if (parts.Length != 3)
                return DateTime.Now;

            if (int.TryParse(parts[0], out int year) &&
                int.TryParse(parts[1], out int month) &&
                int.TryParse(parts[2], out int day))
            {
                try
                {
                    return new DateTime(year, month, day);
                }
                catch
                {
                    return DateTime.Now;
                }
            }

            return DateTime.Now;
        }

        /// <summary>
        /// Gets the current Georgian date
        /// </summary>
        /// <returns>Current Georgian date string</returns>
        public static string GetCurrentGeorgianDate()
        {
            return ToGeorgianString(DateTime.Now);
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
        /// Converts Gregorian date string to Georgian
        /// </summary>
        /// <param name="gregorianDate">Gregorian date in yyyy-MM-dd format</param>
        /// <returns>Georgian date string</returns>
        public static string GregorianToGeorgian(string gregorianDate)
        {
            if (string.IsNullOrEmpty(gregorianDate))
                return GetCurrentGeorgianDate();

            if (DateTime.TryParseExact(gregorianDate, "yyyy-MM-dd", null, DateTimeStyles.None, out DateTime date))
            {
                return ToGeorgianString(date);
            }

            return GetCurrentGeorgianDate();
        }

        /// <summary>
        /// Converts Georgian date string to Gregorian
        /// </summary>
        /// <param name="georgianDate">Georgian date in yyyy/MM/dd format</param>
        /// <returns>Gregorian date string in yyyy-MM-dd format</returns>
        public static string GeorgianToGregorian(string georgianDate)
        {
            if (string.IsNullOrEmpty(georgianDate))
                return GetCurrentGregorianDate();

            var gregorianDate = FromGeorgianString(georgianDate);
            return gregorianDate.ToString("yyyy-MM-dd");
        }

        /// <summary>
        /// Formats Georgian date for display
        /// </summary>
        /// <param name="georgianDate">Georgian date string</param>
        /// <returns>Formatted date for display</returns>
        public static string FormatForDisplay(string georgianDate)
        {
            if (string.IsNullOrEmpty(georgianDate))
                return GetCurrentGeorgianDate();

            var parts = georgianDate.Replace("-", "/").Split('/');
            if (parts.Length == 3)
            {
                var year = parts[0];
                var month = parts[1];
                var day = parts[2];
                
                // Get month name in English
                var monthNames = new[]
                {
                    "", "January", "February", "March", "April", "May", "June",
                    "July", "August", "September", "October", "November", "December"
                };

                if (int.TryParse(month, out int monthNum) && monthNum >= 1 && monthNum <= 12)
                {
                    return $"{day} {monthNames[monthNum]} {year}";
                }
            }

            return georgianDate;
        }

        /// <summary>
        /// Gets Georgian month names
        /// </summary>
        /// <returns>Array of month names</returns>
        public static string[] GetMonthNames()
        {
            return new[]
            {
                "January", "February", "March", "April", "May", "June",
                "July", "August", "September", "October", "November", "December"
            };
        }

        /// <summary>
        /// Gets Georgian day names
        /// </summary>
        /// <returns>Array of day names</returns>
        public static string[] GetDayNames()
        {
            return new[]
            {
                "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
            };
        }
    }
}
