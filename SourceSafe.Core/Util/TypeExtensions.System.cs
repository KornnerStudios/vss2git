using System.Collections;

namespace SourceSafe
{
    partial class TypeExtensions
    {
        #region Collections
        [System.Diagnostics.DebuggerStepThrough]
        public static bool IsNullOrEmpty<T>(this ICollection<T> coll)
        {
            return coll == null || coll.Count == 0;
        }

        [System.Diagnostics.DebuggerStepThrough]
        public static bool IsNotNullOrEmpty<T>(this ICollection<T> coll)
        {
            return coll != null && coll.Count != 0;
        }

        public static bool IsNullOrEmpty(this IEnumerable items)
        {
            if (items is ICollection itemCollection)
            {
                return itemCollection.Count == 0;
            }
            else
            {
                return !items.GetEnumerator().MoveNext();
            }
        }
        #endregion

        public static string ToIsoTimestamp(this DateTime time)
        {
            //return time.ToString(@"yyyy-MM-ddTHH\:mm\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture);
            return time.ToString(@"yyyy-MM-dd HH\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

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
        public static DateTime ConvertAmbiguousTimeToUtc(
            this DateTime ambiguousTime,
            IO.SimpleLogger logger)
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

            var ambiguousTimeUtc = DateTime.SpecifyKind(ambiguousTime - tzi.BaseUtcOffset, DateTimeKind.Utc);

            if (logger != null)
            {
                logger.WriteLine($"WARNING: Ambiguous time '{ambiguousTime}', falling back to '{ambiguousTimeUtc}'");
            }

            return ambiguousTimeUtc;
        }
    };
}
