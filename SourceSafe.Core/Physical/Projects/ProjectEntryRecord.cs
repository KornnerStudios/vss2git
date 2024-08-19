
namespace SourceSafe.Physical.Projects
{
    /// <summary>
    /// VSS record for representing an item stored in particular project.
    /// </summary>
    /// <seealso cref="VssScanChild"/>
    public sealed class ProjectEntryRecord : Records.VssRecordBase
    {
        public const string SIGNATURE = "JP";
        public override string Signature => SIGNATURE;

        public VssItemType ItemType { get; private set; }
        public ProjectEntryFlags Flags { get; private set; }
        public VssName Name { get; private set; }
        public short PinnedVersion { get; private set; }
        /// <summary>
        /// Name of the database file name, in "aaaaaaaa" format.
        /// </summary>
        public string Physical { get; private set; } = "";

        public bool IsProject => ItemType == VssItemType.Project;
        public bool IsFile => ItemType == VssItemType.File;
        public string PhysicalNameAllUpperCase => Physical.ToUpperInvariant();

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            ItemType = (VssItemType)reader.ReadInt16();
            Flags = (ProjectEntryFlags)reader.ReadInt16();
            Name = reader.ReadName();
            PinnedVersion = reader.ReadInt16();
            Physical = reader.ReadPhysicalNameString10();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            textDumper.WriteLine($"Item Type: {ItemType} - Name: {Name.ShortName} ({Physical})");
            textDumper.WriteLine($"Flags: {Flags}");
            textDumper.WriteLine($"Pinned version: {PinnedVersion}");
        }
    };
}
