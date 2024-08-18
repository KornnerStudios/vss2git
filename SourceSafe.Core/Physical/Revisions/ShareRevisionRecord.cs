
namespace SourceSafe.Physical.Revisions
{
    public sealed class ShareRevisionRecord : RevisionRecordBase
    {
        short unkShort;

        /// <summary>
        /// This field is used for VssOpcode_SharedFile and VssOpcode_CheckedIn
        /// operations. Note that this is a path within the VSS database, and
        /// will start with "$/...". Any path that starts with "$" will be a
        /// reference to a project or file within the database.
        ///
        /// For VssOpcode_SharedFile, this contains the path of the file being
        /// shared.
        ///
        /// For VssOpcode_CheckedIn, this is the path within the database from
        /// which the check-in was performed. This is really only relevant when
        /// a file is shared between multiple projects. <see cref="ProjectPath"/> will
        /// indicate the project from which the check-in was performed.
        /// It's not clear how this is useful, except perhaps for change auditing.
        /// Within VSS itself, this information does not appear to be used for
        /// anything.
        /// </summary>
        public string ProjectPath { get; private set; } = "";
        public VssName Name { get; private set; }
        public short UnpinnedRevision { get; private set; }
        public short PinnedRevision { get; private set; }
        public string Physical { get; private set; } = "";

        public override void Read(
            IO.VssBufferReader reader,
            Records.RecordHeader header)
        {
            base.Read(reader, header);

            ProjectPath = reader.ReadString(260);
            Name = reader.ReadName();
            UnpinnedRevision = reader.ReadInt16();
            PinnedRevision = reader.ReadInt16();
            unkShort = reader.ReadInt16(); // often seems to increment
            Physical = reader.ReadString(10);
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            base.Dump(textDumper);

            textDumper.WriteLine($"Project path: {ProjectPath}");
            textDumper.WriteLine($"Name: {Name.ShortName} ({Physical})");
            if (UnpinnedRevision == 0)
            {
                textDumper.WriteLine($"Pinned at revision {PinnedRevision}");
            }
            else if (UnpinnedRevision > 0)
            {
                textDumper.WriteLine($"Unpinned at revision {UnpinnedRevision}");
            }

            if (unkShort != 0)
            {
                textDumper.IncreaseIndent();
                textDumper.WriteLine($"Unknown: {unkShort}");
                textDumper.DecreaseIndent();
            }
        }
    };
}
