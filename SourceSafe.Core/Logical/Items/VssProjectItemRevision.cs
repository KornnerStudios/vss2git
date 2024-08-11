using SourceSafe.Physical.Records;

namespace SourceSafe.Logical.Items
{
    /// <summary>
    /// Represents a revision of a VSS project.
    /// </summary>
    public sealed class VssProjectItemRevision : VssItemRevisionBase
    {
        public VssProjectItem Project => (VssProjectItem)Item;

        internal VssProjectItemRevision(
            VssProjectItem item,
            Physical.Revisions.RevisionRecordBase revision,
            CommentRecord comment)
            : base(item, revision, comment)
        {
        }
    };
}
