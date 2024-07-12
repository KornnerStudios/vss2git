
namespace SourceSafe.Logical
{
    /// <summary>
    /// Represents the name of a VSS item.
    /// </summary>
    public sealed class VssItemName
    {
        /// <summary>
        /// The current logical name of the item.
        /// Note that the logical name can change over the history of the item.
        /// </summary>
        public string LogicalName { get; }

        /// <summary>
        /// The physical name of the item (e.g. AAAAAAAA). This name never changes.
        /// </summary>
        public string PhysicalName { get; }

        /// <summary>
        /// Indicates whether this item is a project or a file.
        /// </summary>
        public bool IsProject { get; }

        public VssItemName(string logicalName, string physicalName, bool isProject)
        {
            LogicalName = logicalName;
            PhysicalName = physicalName;
            IsProject = isProject;
        }

        /// <summary>
        /// Parses a VSS item name from a string (same format as used in ToString).
        /// </summary>
        /// <param name="name"></param>
        /// <returns>Null or empty strings will return null.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static VssItemName? Parse(string name)
        {
            VssItemName? result = null;

            if (!string.IsNullOrEmpty(name))
            {
                bool isProject = name.StartsWith(SourceSafeConstants.RootProjectName);
                string logicalName = isProject ? name[SourceSafeConstants.RootProjectName.Length..] : name;
                string physicalName = logicalName;

                int openParenIndex = logicalName.IndexOf('(');
                int closeParenIndex = logicalName.IndexOf(')');
                if (openParenIndex >= 0 && closeParenIndex >= 0)
                {
                    logicalName = logicalName[..openParenIndex];
                    int physicalNameStart = openParenIndex + 1;
                    int physicalNameLength = closeParenIndex - openParenIndex - 1;
                    physicalName = physicalName.Substring(physicalNameStart, physicalNameLength);
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(name), name,
                        "Invalid item name format");
                }

                result = new VssItemName(logicalName, physicalName, isProject);
            }

            return result;
        }

        public override string ToString() =>
            $"{(IsProject ? SourceSafeConstants.RootProjectName : "")}{LogicalName}({PhysicalName})";
    };
}
