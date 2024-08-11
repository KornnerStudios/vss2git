
namespace SourceSafe.Physical.Revisions
{
    /// <summary>
    /// Enumeration of physical VSS revision actions.
    /// </summary>
    public enum RevisionAction
    {
        // project actions
        Label = 0,
        CreateProject = 1,
        AddProject = 2,
        AddFile = 3,
        DestroyProject = 4,
        DestroyFile = 5,
        DeleteProject = 6,
        DeleteFile = 7,
        RecoverProject = 8,
        RecoverFile = 9,
        RenameProject = 10,
        RenameFile = 11,
        MoveFrom = 12,
        MoveTo = 13,
        ShareFile = 14,
        BranchFile = 15,

        // file actions
        CreateFile = 16,
        EditFile = 17,
        CheckInProject = 18, // Untested, from #vssnotes
        CreateBranch = 19, // #vssnotes says this is RollBack?

        ArchiveVersionFile = 20, // Untested, from #vssnotes
        RestoreVersionFile = 21, // Untested, from #vssnotes
        ArchiveFile = 22, // Untested, from #vssnotes

        // archive actions
        ArchiveProject = 23,
        RestoreFile = 24,
        RestoreProject = 25,

        PinFile = 26, // Untested, from #vssnotes
        UnpinFile = 27, // Untested, from #vssnotes
    };
}
