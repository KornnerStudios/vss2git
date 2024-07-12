
namespace SourceSafe.Physical.DeltaDiff
{
    /// <summary>
    /// Enumeration of file revision delta commands.
    /// </summary>
    public enum DeltaCommand
    {
        // Insert <count> bytes from the data stream.
        WriteLog = 0, // write data from the log file
        // Copy <count> bytes from the <pNewFile> array.
        WriteSuccessor = 1, // write data from the subsequent revision
        Stop = 2 // indicates the last operation
    };
}
