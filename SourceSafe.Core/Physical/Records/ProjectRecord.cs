﻿
namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// VSS record representing a project file.
    /// </summary>
    public sealed class ProjectRecord : VssRecordBase
    {
        public const string SIGNATURE = "PF";

        public override string Signature => SIGNATURE;
        public int PrevProjectOffset { get; private set; }
        public string ProjectFile { get; private set; } = "";

        public override void Read(
            IO.VssBufferReader reader,
            RecordHeader header)
        {
            base.Read(reader, header);

            PrevProjectOffset = reader.ReadInt32();
            ProjectFile = reader.ReadString(12);
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            textDumper.WriteLine($"Project file: {ProjectFile}");
            textDumper.WriteLine($"Prev project offset: {PrevProjectOffset:X6}");
        }
    };
}
