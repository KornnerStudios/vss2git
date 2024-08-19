﻿
namespace SourceSafe.Physical.Files.Names
{
    /// <summary>
    /// VSS header record for the name file.
    /// </summary>
    internal class NamesHeaderRecord : Records.VssRecordBase
    {
        public const string SIGNATURE = "HN";

        public override string Signature => SIGNATURE;

        public int EofOffset { get; private set; }

        protected override void ReadInternal(IO.VssBufferReader reader)
        {
            reader.SkipAssumedToBeAllZeros(16); // reserved; always 0
            EofOffset = reader.ReadInt32();
            reader.SkipAssumedToBeAllZeros(60); // remaining reserved; always 0
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            textDumper.WriteLine($"EOF offset: {EofOffset:X6}");
        }
    };
}
