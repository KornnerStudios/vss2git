
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Base class for VSS records.
    /// </summary>
    public abstract class VssRecordBase
    {
        public abstract string Signature { get; }
        public RecordHeader? Header { get; private set; }

        public virtual void Read(IO.VssBufferReader reader, RecordHeader header)
        {
            Header = header;
        }

        public abstract void Dump(TextWriter writer, int indent);
    };
}
