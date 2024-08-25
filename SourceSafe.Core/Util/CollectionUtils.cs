using System.Collections;
using System.Text;

namespace SourceSafe
{
    /// <summary>
    /// A set of utility methods for operating on collections.
    /// </summary>
    public static class CollectionUtils
    {
        public delegate T TransformFunction<F, T>(F obj);

        public static IEnumerable<T> Transform<F, T>(
            IEnumerable<F> items,
            TransformFunction<F, T> func)
        {
            foreach (F item in items)
            {
                yield return func(item);
            }
        }

        public static void Join(
            StringBuilder buf,
            string separator,
            IEnumerable items)
        {
            bool first = true;
            foreach (object item in items)
            {
                if (!first)
                {
                    buf.Append(separator);
                }
                else
                {
                    first = false;
                }
                buf.Append(item);
            }
        }

        public static string FormatCollection<T>(
            IEnumerable<T> collection,
            string separator = ", ")
        {
            StringBuilder buf = new();
            foreach (T item in collection)
            {
                if (buf.Length > 0)
                {
                    buf.Append(separator);
                }
                buf.Append(item);
            }
            return buf.ToString();
        }

        public static HashSet<string>? ConvertToUpper(
            HashSet<string>? input)
        {
            HashSet<string>? output = null;

            if (input != null)
            {
                output = new(input.Count);
                foreach (string previousValue in input)
                {
                    string newValue = previousValue
                        .ToUpperInvariant();
                    output.Add(newValue);
                }
            }

            return output;
        }

        public static HashSet<string>? ConvertForwardSlashesToBackSlashAndToUpper(
            HashSet<string>? input)
        {
            HashSet<string>? output = null;

            if (input != null)
            {
                output = new(input.Count);
                foreach (string previousValue in input)
                {
                    string newValue = previousValue
                        .Replace('/', '\\')
                        .ToUpperInvariant();
                    output.Add(newValue);
                }
            }

            return output;
        }

        public static Dictionary<string, string>? ConvertForwardSlashesToBackSlashAndToUpper(
            Dictionary<string, string>? input,
            bool alsoConvertValues = true)
        {
            Dictionary<string, string>? output = null;

            if (input != null)
            {
                output = new(input.Count);
                foreach (KeyValuePair<string, string> previousKvp in input)
                {
                    string newKey = previousKvp.Key
                        .Replace('/', '\\')
                        .ToUpperInvariant();
                    string newValue = alsoConvertValues
                        ? previousKvp.Value
                            .Replace('/', '\\')
                            .ToUpperInvariant()
                        : previousKvp.Value;
                    output.Add(newKey, newValue);
                }
            }

            return output;
        }
    };
}
