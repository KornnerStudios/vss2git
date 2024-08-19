
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Base class for VSS records.
    /// </summary>
    public abstract class VssRecordBase
    {
        RecordHeader? mHeader;

        public abstract string Signature { get; }
        public RecordHeader Header
        {
            get { return mHeader ?? throw new InvalidOperationException($"Tried to access {GetType().Name} header before it was set"); }
        }

        public void Read(IO.VssBufferReader reader, RecordHeader header)
        {
            mHeader = header;
            ReadInternal(reader);
        }

        protected abstract void ReadInternal(IO.VssBufferReader reader);

        public abstract void Dump(Analysis.AnalysisTextDumper textDumper);
    };
}
