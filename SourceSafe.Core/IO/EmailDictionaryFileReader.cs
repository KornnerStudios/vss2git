
namespace SourceSafe.IO
{
    public sealed class EmailDictionaryFileReader
    {
        private static readonly char[] ValueSeparator = ['='];

        public string InputFilePath { get; init; }
        Dictionary<string, string>? mValues;

        public EmailDictionaryFileReader(string path)
        {
            if (File.Exists(path))
            {
                InputFilePath = path;
#if false // #TODO verify the correctness of the else conversion
                mValues = File.ReadLines(path)
                    .Where(line => (!String.IsNullOrWhiteSpace(line) && !line.StartsWith('#')))
                    .Select(line => line.Split(ValueSeparator, 2, 0))
                    .ToDictionary(parts => parts[0].Trim(), parts => parts.Length > 1 ? parts[1].Trim() : null);
#else
                IEnumerable<KeyValuePair<string, string>> keyValuePairs =
                    from line in File.ReadLines(path)
                    where
                    (
                        !String.IsNullOrWhiteSpace(line) &&
                        !line.StartsWith('#')
                    )
                    let parts = line.Split(ValueSeparator, 2, 0)
                    select new KeyValuePair<string, string?>
                    (
                        parts[0].Trim(),
                        parts.Length > 1
                            ? parts[1].Trim()
                            : null
                    );
                mValues = keyValuePairs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
#endif
            }
            else
            {
                InputFilePath = $"#PATH_NOT_FOUND({path})";
            }
        }

        public string? GetValue(string name, string? defaultValue = null)
        {
            if (mValues != null && mValues.TryGetValue(name, out string? value))
            {
                return value;
            }
            return defaultValue;
        }
    };
}
