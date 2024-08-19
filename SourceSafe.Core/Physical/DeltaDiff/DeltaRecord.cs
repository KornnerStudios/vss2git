
namespace SourceSafe.Physical.DeltaDiff
{
    /// <summary>
    /// VSS record representing a reverse-delta for a file revision.
    /// </summary>
    public sealed class DeltaRecord : Records.VssRecordBase
    {
        public const string SIGNATURE = "FD";

        private readonly List<DeltaOperation> operations = [];

        public override string Signature => SIGNATURE;
        public IEnumerable<DeltaOperation> Operations => operations;

        public static bool ReadCheckForMissingStopCommands { get; set; } = false;
        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            int dataStartOffset = Header.Offset + Records.RecordHeader.LENGTH;
            int dataEndOffset = dataStartOffset + Header.Length;
#if DEBUG
            bool encounteredStop = false;
#endif // DEBUG

            for (int offset = reader.Offset; offset < dataEndOffset; offset = reader.Offset)
            {
                DeltaOperation operation = new();
                operation.Read(reader);
                if (operation.Command == DeltaCommand.Stop)
                {
#if DEBUG
                    encounteredStop = true;
#endif // DEBUG
                    break;
                }
                operations.Add(operation);
            }

#if DEBUG
            if (ReadCheckForMissingStopCommands && !encounteredStop)
            {
                "".ToString(); // place a breakpoint as needed
            }
#endif // DEBUG
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            foreach (DeltaOperation operation in operations)
            {
                operation.Dump(textDumper);
            }
        }
    };
}
