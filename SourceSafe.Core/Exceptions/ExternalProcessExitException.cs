
namespace SourceSafe.Exceptions
{
    /// <summary>
    /// Exception thrown when a process exits with a non-zero exit code.
    /// </summary>
    public class ExternalProcessExitException : ExternalProcessException
    {
        public string Stdout { get; }
        public string Stderr { get; }

        public ExternalProcessExitException(
            string message,
            string executable,
            string arguments,
            string stdout,
            string stderr)
            : base(message, executable, arguments)
        {
            Stdout = stdout;
            Stderr = stderr;
        }
    };
}
