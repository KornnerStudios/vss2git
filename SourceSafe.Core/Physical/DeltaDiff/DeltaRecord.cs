
namespace SourceSafe.Physical.DeltaDiff
{
    /// <summary>
    /// VSS record representing a reverse-delta for a file revision.
    /// </summary>
    public sealed class DeltaRecord : Records.VssRecordBase
    {
        public const string SIGNATURE = "FD";

        private readonly List<DeltaOperation> mOperations = [];

        public override string Signature => SIGNATURE;
        public IEnumerable<DeltaOperation> Operations => mOperations;

        public bool EncounteredInvalidOperation { get; private set; }

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            int dataStartOffset = Header.Offset + Records.RecordHeader.LENGTH;
            int dataEndOffset = dataStartOffset + Header.Length;
#if DEBUG
            bool checkForMissingStopCommands =
                reader.Database.Config.ConfigRecords.DeltaRecordsReadCheckForMissingStopCommands;
            bool encounteredStop = false;
#endif // DEBUG

            for (int offset = reader.Offset; offset < dataEndOffset; offset = reader.Offset)
            {
                DeltaOperation operation = new();
                try
                {
                    operation.Read(reader);
                }
                catch (Records.InvalidRecordDataException ex)
                {
                    EncounteredInvalidOperation = true;
                    reader.TextDumperHack?.ErrorWriteLine(ex.Message);
                    break;
                }

                if (operation.Command == DeltaCommand.Stop)
                {
#if DEBUG
                    encounteredStop = true;
#endif // DEBUG
                    break;
                }
                mOperations.Add(operation);
            }

#if DEBUG
            if (checkForMissingStopCommands && !encounteredStop)
            {
                "".ToString(); // place a breakpoint as needed
            }
#endif // DEBUG
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            if (EncounteredInvalidOperation)
            {
                textDumper.ErrorWriteLine("Encountered invalid delta operation(s)");
                textDumper.WriteLine($"Valid Operations #{mOperations.Count}");
            }

            if (textDumper.Config.DumpDeltaRecordOperations)
            {
                foreach (DeltaOperation operation in mOperations)
                {
                    operation.Dump(textDumper);
                }
            }
        }
    };
}
