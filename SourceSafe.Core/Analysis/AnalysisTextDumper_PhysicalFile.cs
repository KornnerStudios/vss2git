
using System.Text;

namespace SourceSafe.Analysis
{
    partial class AnalysisTextDumper
    {
        public sealed class DumpPhysicalFileAdditionalResults
        {
            public HashSet<Physical.Revisions.RevisionAction>
                InOutProjectRevisionActions { get; } = [];
            public HashSet<Physical.Revisions.RevisionAction>
                InOutFileRevisionActions { get; } = [];

            public void Dump(AnalysisTextDumper analysisTextDumper)
            {
                analysisTextDumper.WriteSeparator();
                analysisTextDumper.mWriter.WriteLine("Project actions: {0}",
                    CollectionUtils.FormatCollection(InOutProjectRevisionActions));
                analysisTextDumper.mWriter.WriteLine("File actions: {0}",
                    CollectionUtils.FormatCollection(InOutFileRevisionActions));
            }
        };

        public void DumpPhysicalFile(
            string physicalFilePath,
            DumpPhysicalFileAdditionalResults? additionalResults = null)
        {
            var physicalFile = new Physical.Files.VssPhysicalFile(
                physicalFilePath,
                Encoding.Default);

            if (DumpRecordHeaders)
            {
                physicalFile.Header.Header.Dump(this);
            }
            physicalFile.Header.Dump(this);

            IncreaseIndent();
            Physical.Records.VssRecordBase? record = physicalFile.GetNextRecord(true);
            int revisionIndex = -1;
            while (record != null)
            {
                revisionIndex++;
                if (DumpRecordHeaders)
                {
                    record.Header.Dump(this);
                }

                IncreaseIndent();
                record.Dump(this);
                DecreaseIndent();

                #region Track encountered revision actions
                if (additionalResults != null)
                {
                    if (record is Physical.Revisions.RevisionRecordBase revision)
                    {
                        if (physicalFile.Header.IsProject)
                        {
                            additionalResults.InOutProjectRevisionActions.Add(revision.Action);
                        }
                        else
                        {
                            additionalResults.InOutFileRevisionActions.Add(revision.Action);
                        }
                    }
                }
                #endregion

                record = physicalFile.GetNextRecord(true);
            }
            DecreaseIndent();
        }
    };
}
