namespace SourceSafe.Logical.Actions
{
    /// <summary>
    /// Enumeration of logical VSS revision actions.
    /// </summary>
    public enum VssActionType
    {
        Label,
        Create,
        Destroy,
        Add,
        Delete,
        Recover,
        Rename,
        MoveFrom,
        MoveTo,
        Share,
        Pin,
        Branch,
        Edit,
        Archive,
        Restore
    };
}
