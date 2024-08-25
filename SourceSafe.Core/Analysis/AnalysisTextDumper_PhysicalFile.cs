using System.Collections.Immutable;
using System.Text;

namespace SourceSafe.Analysis
{
    partial class AnalysisTextDumper
    {
        public sealed class DumpPhysicalFileAdditionalResults
        {
            #region Found RevisionActions
            public HashSet<Physical.Revisions.RevisionAction>
                InOutProjectRevisionActions { get; } = [];
            public HashSet<Physical.Revisions.RevisionAction>
                InOutFileRevisionActions { get; } = [];

            public void DumpFoundRevisionActions(AnalysisTextDumper analysisTextDumper)
            {
                analysisTextDumper.WriteSeparator();
                analysisTextDumper.mWriter.WriteLine("Project actions: {0}",
                    CollectionUtils.FormatCollection(InOutProjectRevisionActions));
                analysisTextDumper.mWriter.WriteLine("File actions: {0}",
                    CollectionUtils.FormatCollection(InOutFileRevisionActions));
            }
            #endregion

            #region Found User and Machine names
            public HashSet<string>
                InOutUserNames { get; } = [];
            public HashSet<string>
                InOutMachineNames { get; } = [];
            public Dictionary<string, HashSet<string>>
                InOutUserNamesWithMachineNames { get; } = [];

            internal bool AddFoundUserName(string? userName)
            {
                if (!string.IsNullOrEmpty(userName))
                {
                    InOutUserNames.Add(userName);
                    return true;
                }

                return false;
            }

            internal bool AddFoundMachineName(string? machineName)
            {
                if (!string.IsNullOrEmpty(machineName))
                {
                    InOutMachineNames.Add(machineName);
                    return true;
                }

                return false;
            }

            internal void AddFoundUserAndMachineNames(string? userName, string? machineName)
            {
                if (AddFoundUserName(userName) && AddFoundMachineName(machineName))
                {
                    if (!InOutUserNamesWithMachineNames.TryGetValue(userName!, out HashSet<string>? machineNames))
                    {
                        machineNames = new HashSet<string>();
                        InOutUserNamesWithMachineNames.Add(userName!, machineNames);
                    }
                    machineNames.Add(machineName!);
                }
            }

            public void DumpFoundUserAndMachineNames(AnalysisTextDumper analysisTextDumper)
            {
                analysisTextDumper.WriteSeparator();
                if (InOutUserNames.Count > 0)
                {
                    analysisTextDumper.WriteLine($"Users: #{InOutUserNames.Count}");
                    analysisTextDumper.IncreaseIndent();
                    foreach (string userName in InOutUserNames.ToImmutableSortedSet())
                    {
                        analysisTextDumper.WriteLine(userName);
                    }
                    analysisTextDumper.DecreaseIndent();
                }

                if (InOutMachineNames.Count > 0)
                {
                    analysisTextDumper.WriteLine($"Machines: #{InOutMachineNames.Count}");
                    analysisTextDumper.IncreaseIndent();
                    foreach (string machineName in InOutMachineNames.ToImmutableSortedSet())
                    {
                        analysisTextDumper.WriteLine(machineName);
                    }
                    analysisTextDumper.DecreaseIndent();
                }

                if (InOutUserNamesWithMachineNames.Count > 0)
                {
                    analysisTextDumper.mWriter.WriteLine($"Users with machines: #{InOutUserNamesWithMachineNames.Count}");
                    analysisTextDumper.IncreaseIndent();
                    foreach (KeyValuePair<string, HashSet<string>> usersAndMachines in InOutUserNamesWithMachineNames.ToImmutableSortedDictionary())
                    {
                        analysisTextDumper.WriteLine(usersAndMachines.Key);
                        analysisTextDumper.IncreaseIndent();
                        foreach (string machineName in usersAndMachines.Value.ToImmutableSortedSet())
                        {
                            analysisTextDumper.WriteLine(machineName);
                        }
                        analysisTextDumper.DecreaseIndent();
                    }
                    analysisTextDumper.DecreaseIndent();
                }
            }
            #endregion
        };

        internal DumpPhysicalFileAdditionalResults? CurrentDumpPhysicalFileAdditionalResults { get; private set; }

        public void DumpPhysicalFile(
            Logical.VssDatabase vssDatabase,
            string physicalFilePath,
            DumpPhysicalFileAdditionalResults? additionalResults = null)
        {
            CurrentDumpPhysicalFileAdditionalResults = additionalResults;

            var physicalFile = new Physical.Files.VssPhysicalFile(
                vssDatabase,
                physicalFilePath);

            if (Config.DumpRecordHeaders)
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
                DumpPhysicalFileRecord(physicalFile, record);

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

            CurrentDumpPhysicalFileAdditionalResults = null;
        }

        private void DumpPhysicalFileRecord(
            Physical.Files.VssPhysicalFile physicalFile,
            Physical.Records.VssRecordBase record)
        {
            if (Config.DumpRecordHeaders)
            {
                record.Header.Dump(this);
            }

            IncreaseIndent();
            record.Dump(this);
            DecreaseIndent();
        }
    };
}
