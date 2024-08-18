
namespace SourceSafe.Analysis
{
    partial class AnalysisTextDumper
    {
        public sealed class DumpFileHierarchyAdditionalResults
        {
            public HashSet<string>
                OutEnumeratedPhysicalNames { get; } = [];
        };
        public void DumpFileHierarchy(
            Logical.VssDatabase db,
            bool includeRevisions,
            DumpFileHierarchyAdditionalResults? additionalResults = null)
        {
            var treeDumper = new Logical.Items.VssItemTreeTextDumper(mWriter)
            {
                IncludeRevisions = includeRevisions,
            };

            mWriter.WriteLine("File hierarchy:");
            WriteSeparator();
            treeDumper.DumpProject(db.RootProject);
            mWriter.WriteLine();

            if (additionalResults != null)
            {
                additionalResults.OutEnumeratedPhysicalNames.Clear();
                additionalResults.OutEnumeratedPhysicalNames.UnionWith(treeDumper.PhysicalNames);
            }
        }
    };
}
