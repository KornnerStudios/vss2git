
namespace SourceSafe.IO
{
    /// <summary>
    /// A very simple .INI file reader that does not require or support sections.
    /// </summary>
    public sealed class SimpleIniReader
    {
        private readonly Dictionary<string, string> entries = [];

        public SimpleIniReader(string filename)
        {
            Filename = filename;
        }

        public string Filename { get; }

        public void Parse()
        {
            entries.Clear();

            using var reader = new StreamReader(Filename);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length > 0 && !line.StartsWith(';'))
                {
                    int separator = line.IndexOf('=');
                    if (separator > 0)
                    {
                        string key = line[..separator].Trim();
                        string value = line[(separator + 1)..].Trim();
                        entries[key] = value;
                    }
                }
            }
        }

        public string GetValue(string key, string defaultValue)
        {
            return entries.TryGetValue(key, out string? result)
                ? result
                : defaultValue;
        }
    };
}
