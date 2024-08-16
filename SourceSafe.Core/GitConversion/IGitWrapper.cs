
namespace SourceSafe.GitConversion
{
    public interface IGitWrapper
    {
        bool IncludeIgnoredFiles { get; set; }

        string GetRepoPath();
        bool NeedsCommit();
        void SetNeedsCommit();
        TimeSpan ElapsedTime();
        bool FindExecutable();

        void Init(bool resetRepo);
        void Exit();

        void Configure();

        string GetCheckoutBranch();

        bool Add(string path);
        bool Add(IEnumerable<string> paths);
        bool AddDir(string path);
        bool AddAll();

        void RemoveFile(string path);
        void RemoveDir(string path, bool recursive);
        void RemoveEmptyDir(string path);

        void MoveFile(
            string sourcePath,
            string destinationPath);
        void MoveDir(
            string sourcePath,
            string destinationPath);
        void MoveEmptyDir(
            string sourcePath,
            string destinationPath);

        bool Commit(
            string authorName,
            string authorEmail,
            string comment,
            DateTime utcTime);
        void Tag(
            string name,
            string taggerName,
            string taggerEmail,
            string comment,
            DateTime utcTime);
    };
}
