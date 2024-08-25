
namespace SourceSafe.Analysis
{
    public partial class AnalysisTextDumperConfig
    {
        /// <summary>
        /// Some fields are skipped for brevity, e.g. empty strings.
        /// This will instead force the output of those fields.
        /// </summary>
        public bool DumpVerboseData { get; set; }
            = false;

        public bool DumpRecordHeaders { get; set; }
            = true;

        public bool DumpDeltaRecordOperations { get; set; }
            = false;
        public bool DumpDeltaOperationDataBytes { get; set; }
            = false;
    };
}
