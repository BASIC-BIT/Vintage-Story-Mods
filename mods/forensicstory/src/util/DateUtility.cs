using System;

namespace forensicstory.util
{
    public static class DateUtility
    {
        public static string DateFormat = "yyyy-MM-dd";

        public static string GetDateString(DateTime? date = null)
        {
            var outputDate = date ?? DateTime.Now;

            return outputDate.ToString(DateFormat);
        }
    }
}