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
    };
}
