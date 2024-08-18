
namespace SourceSafe.IO
{
    /// <summary>
    /// A very simple .INI file reader that does not require or support sections.
    /// </summary>
    public sealed class SimpleIniReader
    {
        public string FileName { get; }
        public Dictionary<string, string> Entries { get; } = [];

        public SimpleIniReader(string fileName)
        {
            FileName = fileName;
        }

        public void Parse()
        {
            Entries.Clear();

            using var reader = new StreamReader(FileName);
            for (string? line; (line = reader.ReadLine()) != null; )
            {
                line = line.Trim();
                if (line.Length > 0 && !line.StartsWith(';'))
                {
                    int separator = line.IndexOf('=');
                    if (separator > 0)
                    {
                        string key = line[..separator].Trim();
                        string value = line[(separator + 1)..].Trim();
                        Entries[key] = value;
                    }
                }
            }
        }

        public string GetValue(string key, string defaultValue)
        {
            return Entries.TryGetValue(key, out string? result)
                ? result
                : defaultValue;
        }
    };
}
