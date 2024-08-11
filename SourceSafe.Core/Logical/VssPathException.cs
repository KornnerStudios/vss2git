
namespace SourceSafe.Logical
{
    /// <summary>
    /// Exception thrown when an invalid VSS path is used.
    /// </summary>
    public class VssPathException : Exception
    {
        public VssPathException()
        {
        }

        public VssPathException(string message)
            : base(message)
        {
        }

        public VssPathException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    };
}
