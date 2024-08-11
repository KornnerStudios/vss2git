using SourceSafe.Physical.Records;

namespace SourceSafe.Logical.Items
{
    /// <summary>
    /// Represents a VSS project.
    /// </summary>
    public sealed partial class VssProjectItem : VssItemBase
    {
        public string LogicalPath { get; init; }

        public IEnumerable<VssProjectItem> Projects =>
            new VssProjectsEnumeration(this);

        public IEnumerable<VssFileItem> Files =>
            new VssFilesEnumeration(this);

        [Obsolete("Unused")]
        public new IEnumerable<VssProjectItemRevision> Revisions =>
            new VssRevisionsEnumeration<VssProjectItem, VssProjectItemRevision>(this);

        [Obsolete("Unused")]
        public new VssProjectItemRevision GetRevision(int version)
        {
            return (VssProjectItemRevision)base.GetRevision(version);
        }

        public VssProjectItem? FindProject(string name)
        {
            foreach (VssProjectItem subproject in Projects)
            {
                if (name == subproject.Name)
                {
                    return subproject;
                }
            }
            return null;
        }

        public VssFileItem? FindFile(string name)
        {
            foreach (VssFileItem file in Files)
            {
                if (name == file.Name)
                {
                    return file;
                }
            }
            return null;
        }

        [Obsolete("Unused")]
        public VssItemBase? FindItem(string name)
        {
            VssProjectItem? project = FindProject(name);
            if (project != null)
            {
                return project;
            }
            return FindFile(name);
        }

        internal VssProjectItem(
            VssDatabase database,
            VssItemName itemName,
            string physicalPath,
            string logicalPath)
            : base(database, itemName, physicalPath)
        {
            LogicalPath = logicalPath;
        }

        protected override VssItemRevisionBase CreateRevision(
            Physical.Revisions.RevisionRecordBase revision,
            CommentRecord comment)
        {
            return new VssProjectItemRevision(this, revision, comment);
        }
    };
}
