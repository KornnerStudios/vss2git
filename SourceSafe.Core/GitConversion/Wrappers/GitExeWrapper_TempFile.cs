using System.Text;

namespace SourceSafe.GitConversion.Wrappers
{
    partial class GitExeWrapper
    {
        sealed class TempFile : IDisposable
        {
            private readonly FileStream fileStream;

            public string Name { get; }

            public TempFile()
            {
                Name = Path.GetTempFileName();
                fileStream = new FileStream(Name, FileMode.Truncate, FileAccess.Write, FileShare.Read);
            }

            public void Write(string text, Encoding encoding)
            {
                byte[] bytes = encoding.GetBytes(text);
                fileStream.Write(bytes, 0, bytes.Length);
                fileStream.Flush();
            }

            public void Dispose()
            {
                fileStream?.Dispose();
                if (Name != null)
                {
                    File.Delete(Name);
                }
            }
        };
    };
}
