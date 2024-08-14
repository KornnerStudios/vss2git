using SourceSafe.Logical;
using SourceSafe.Logical.Actions;

namespace SourceSafe.Analysis
{
    /// <summary>
    /// Represents a single revision to a file or directory.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{DateTime} {Action} {Item}")]
    public sealed class VssItemRevision
    {
        public DateTime DateTime { get; }
        public string User { get; }
        public VssItemName Item { get; }
        public int Version { get; }
        public string Comment { get; }
        public VssActionBase Action { get; }

        public VssItemRevision(
            DateTime dateTime,
            string user,
            VssItemName item,
            int version,
            string comment,
            VssActionBase action)
        {
            DateTime = dateTime;
            User = user;
            Item = item;
            Version = version;
            Comment = comment;
            Action = action;
        }

        public static bool HaveSameComment(
            VssItemRevision rev1,
            VssItemRevision rev2)
        {
            return (!string.IsNullOrEmpty(rev1.Comment) &&
                    !string.IsNullOrEmpty(rev1.Comment
                    ) &&
                    rev1.Comment == rev2.Comment);
        }
    };
}
