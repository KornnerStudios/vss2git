using System;

namespace Hpdi.Vss2Git
{
    public static class TimeZoneInfoExtension
    {
        /// <summary>
        /// This extension method converts Local time into Utc taking into account ambiguous time.
        ///
        /// If the value is ambiguous, the method returns a DateTime value that represents the corresponding UTC time.
        /// The method handles this conversion by subtracting the value of the local time zone's BaseUtcOffset property from the local time.
        ///
        /// This method makes the arbitrary assumption that an ambiguous time should always be mapped to the time zone's standard time.
        /// The BaseUtcOffset property returns the offset between UTC and a time zone's standard time.
        ///
        /// https://docs.microsoft.com/en-us/dotnet/standard/datetime/resolve-ambiguous-times
        ///
        /// </summary>
        /// <param name="ambiguousTime"></param>
        /// <returns></returns>
        public static DateTime ConvertAmbiguousTimeToUtc( this DateTime ambiguousTime, Logger logger)
        {
            TimeZoneInfo tzi = TimeZoneInfo.Local;

            try
            {
                if (!tzi.IsInvalidTime(ambiguousTime) && !tzi.IsAmbiguousTime(ambiguousTime))
                {
                    return TimeZoneInfo.ConvertTimeToUtc(ambiguousTime, tzi);
                }
            }
            catch (ArgumentException e)
            {
                if (e.ParamName != "dateTime")
                {
                    throw;
                }
            }

            DateTime ambiguousTimeUtc = DateTime.SpecifyKind(ambiguousTime - tzi.BaseUtcOffset, DateTimeKind.Utc);

            if (null != logger)
            {
                logger.WriteLine("WARNING: Ambiguous time '{0}', falling back to '{1}'", ambiguousTime, ambiguousTimeUtc);
            }

            return ambiguousTimeUtc;
        }
    }
}
