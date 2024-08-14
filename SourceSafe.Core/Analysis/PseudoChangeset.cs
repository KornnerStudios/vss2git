
namespace SourceSafe.Analysis
{
    /// <summary>
    /// Represents a set of revisions made by a particular person at a particular time.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Id} {DateTime} {User} {Revisions.Count} {TargetFiles.Count}")]
    public sealed class PseudoChangeset
    {
        public int Id { get; set; } = 0;
        public DateTime DateTime { get; set; }
        public string User { get; set; } = "";
        public List<string> Comment { get; set; } = [];
        public List<VssItemRevision> Revisions { get; } = [];
        public HashSet<string> TargetFiles { get; } = [];
#if DEBUG
        // I added this mainly for my own tracing purposes, for debugging Hpdi.Vss2Git.ChangesetBuilder.BuildChangesets
        private Dictionary<string, List<Logical.Actions.VssActionType>> TargetFileActions { get; } = [];
#endif // DEBUG

        public void AddTargetFile(
            string targetFile,
            Logical.Actions.VssActionType actionType)
        {
            if (TargetFiles.Add(targetFile))
#if DEBUG
            {
                TargetFileActions[targetFile] = [];
            }
            TargetFileActions[targetFile].Add(actionType);
#else // !DEBUG
            {
            }
#endif // DEBUG
        }

        public bool ContainsTargetFile(
            string targetFile)
        {
            return TargetFiles.Contains(targetFile);
        }
    };
}
