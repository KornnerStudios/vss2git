
namespace SourceSafe.IO
{
    public static class OutputUtil
    {
        private static readonly string[] CommonIndentStrings =
        [
            "",
            new string('\t', 1),
            new string('\t', 2),
            new string('\t', 3),
            new string('\t', 4),
            new string('\t', 5),
            new string('\t', 6),
            new string('\t', 7),
            new string('\t', 8),
            new string('\t', 9),
            new string('\t', 10),
        ];
        public static string GetIndentString(int indent)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(indent, 0, nameof(indent));

            if (indent < CommonIndentStrings.Length)
            {
                return CommonIndentStrings[indent];
            }

            return new string('\t', indent);
        }
    };
}
