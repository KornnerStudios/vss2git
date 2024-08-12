
namespace SourceSafe.Exceptions
{
    public class ExternalProcessException : Exception
    {
        public string Executable { get; }

        public string Arguments { get; }

        public ExternalProcessException(string message, string executable, string arguments)
            : base(message)
        {
            Executable = executable;
            Arguments = arguments;
        }

        public ExternalProcessException(string message, Exception innerException, string executable, string arguments)
            : base(message, innerException)
        {
            Executable = executable;
            Arguments = arguments;
        }
    };
}
