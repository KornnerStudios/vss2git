
namespace SourceSafe.Exceptions
{
    /// <summary>
    /// Formats exceptions expected in this application with type-specific details.
    /// </summary>
    public static class ExceptionFormatter
    {
        public static string Format(
            Exception e)
        {
            string message = e.Message;

            var processExit = e as ExternalProcessExitException;
            if (processExit != null)
            {
                return string.Format("{0}\nExecutable: {1}\nArguments: {2}\nStdout: {3}\nStderr: {4}",
                    message, processExit.Executable, processExit.Arguments, processExit.Stdout, processExit.Stderr);
            }

            var process = e as ExternalProcessException;
            if (process != null)
            {
                return string.Format("{0}\nExecutable: {1}\nArguments: {2}",
                    message, process.Executable, process.Arguments);
            }

            return message;
        }
    };
}
