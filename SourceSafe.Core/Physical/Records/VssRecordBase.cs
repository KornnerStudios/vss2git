
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Base class for VSS records.
    /// </summary>
    public abstract class VssRecordBase
    {
        RecordHeader? mHeader;
        string? mSourceFileName;

        public abstract string Signature { get; }
        public RecordHeader Header
        {
            get { return mHeader ?? throw new InvalidOperationException($"Tried to access {GetType().Name} header before it was set"); }
        }

        /// <summary>
        /// This assumes you've already checked that this record came from a
        /// physical file, versus say names.dat or other higher level files.
        /// </summary>
        /// <returns>The physical name, in ALL UPPERCASE</returns>
        public string? TryAndGetSourcePhysicalFileName()
        {
            string? physicalFileName = mSourceFileName;
            if (physicalFileName != null)
            {
                physicalFileName = Path.GetFileName(physicalFileName);
                physicalFileName = physicalFileName.ToUpperInvariant();
            }
            return physicalFileName;
        }

        public void Read(IO.VssBufferReader reader, RecordHeader header)
        {
            mHeader = header;
            mSourceFileName = reader.FileName;
            ReadInternal(reader);
        }

        protected abstract void ReadInternal(IO.VssBufferReader reader);

        public abstract void Dump(Analysis.AnalysisTextDumper textDumper);
    };
}
