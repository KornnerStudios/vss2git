
namespace SourceSafe.Logical.Items
{
    /// <summary>
    /// Dumps the VSS project/file hierarchy to a text writer.
    /// </summary>
    public sealed class VssItemTreeTextDumper
    {
        private readonly TextWriter mWriter;
        public HashSet<string> PhysicalNames { get; } = [];

        public bool IncludeRevisions { get; set; }

        public VssItemTreeTextDumper(TextWriter writer)
        {
            mWriter = writer;
        }

        public void DumpProject(VssProjectItem project)
        {
            DumpProject(project, 0);
        }

        public void DumpProject(
            VssProjectItem project,
            int indent)
        {
            string indentStr = IO.OutputUtil.GetIndentString(indent);

            PhysicalNames.Add(project.PhysicalName);
            mWriter.Write(indentStr);
            mWriter.WriteLine($"({project.PhysicalName}) {project.Name}/");

            foreach (VssProjectItem subproject in project.Projects)
            {
                DumpProject(subproject, indent + 1);
            }

            foreach (VssFileItem file in project.Files)
            {
                PhysicalNames.Add(file.PhysicalName);
                mWriter.Write(indentStr);
                mWriter.WriteLine($"\t({file.PhysicalName}) {file.Name} - {file.GetPath(project)}");

                if (IncludeRevisions)
                {
                    foreach (VssFileItemRevision version in file.Revisions)
                    {
                        mWriter.Write(indentStr);
                        mWriter.WriteLine($"\t\t#{version.Version} {version.User} {version.DateTime}");
                    }
                }
            }
        }
    };
}
