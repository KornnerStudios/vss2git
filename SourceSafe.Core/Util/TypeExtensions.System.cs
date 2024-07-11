
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
        #endregion
    };
}
