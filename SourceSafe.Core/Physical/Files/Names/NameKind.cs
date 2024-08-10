
namespace SourceSafe.Physical.Files.Names
{
    /// <summary>
    /// Enumeration of the kinds of VSS logical item names.
    /// </summary>
    internal enum NameKind : short
    {
        Dos = 1, // DOS 8.3 filename
        Long = 2, // Win95/NT long filename
        MacOS = 3, // Mac OS 9 and earlier 31-character filename
        Project = 10 // VSS project name
    };
}
