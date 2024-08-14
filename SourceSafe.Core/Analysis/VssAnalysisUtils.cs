
namespace SourceSafe.Analysis
{
    enum AnalysisRecursionStatus
    {
        Continue,
        Skip,
        Abort
    };

    delegate AnalysisRecursionStatus VssProjectItemAnalysisCallback(
        Logical.Items.VssProjectItem project);

    delegate AnalysisRecursionStatus VssFileItemAnalysisCallback(
        Logical.Items.VssProjectItem project,
        Logical.Items.VssFileItem file);

    /// <summary>
    /// Helper methods for working with VSS objects.
    /// </summary>
    internal static class VssAnalysisUtils
    {
        public static AnalysisRecursionStatus RecurseItems(
            Logical.Items.VssProjectItem project,
            VssProjectItemAnalysisCallback? projectCallback,
            VssFileItemAnalysisCallback fileCallback)
        {
            if (projectCallback != null)
            {
                AnalysisRecursionStatus status = projectCallback(project);
                if (status != AnalysisRecursionStatus.Continue)
                {
                    return status;
                }
            }
            foreach (Logical.Items.VssProjectItem subproject in project.Projects)
            {
                AnalysisRecursionStatus status = RecurseItems(
                    subproject, projectCallback, fileCallback);
                if (status == AnalysisRecursionStatus.Abort)
                {
                    return status;
                }
            }
            foreach (Logical.Items.VssFileItem file in project.Files)
            {
                AnalysisRecursionStatus status = fileCallback(project, file);
                if (status == AnalysisRecursionStatus.Abort)
                {
                    return status;
                }
            }
            return AnalysisRecursionStatus.Continue;
        }
    };
}
