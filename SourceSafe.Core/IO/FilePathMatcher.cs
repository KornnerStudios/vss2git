using System.Text;
using System.Text.RegularExpressions;

namespace SourceSafe.IO
{
    /// <summary>
    /// Determines whether a path matches a set of glob patterns.
    /// </summary>
    public sealed class FilePathMatcher
    {
        public const string AnyPathPattern = "**";
        public const string AnyNamePattern = "*";
        public const string AnyNameCharPattern = "?";

        private readonly Regex regex;

        public FilePathMatcher(string pattern)
        {
            regex = new Regex(ConvertPattern(pattern),
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        public FilePathMatcher(string[] patterns)
        {
            regex = new Regex(ConvertPatterns(patterns),
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        public bool Matches(string path)
        {
            return regex.IsMatch(path);
        }

        private static string ConvertPattern(string glob)
        {
            var buf = new StringBuilder(glob.Length * 2);
            ConvertPatternInto(glob, buf);
            return buf.ToString();
        }

        private static string ConvertPatterns(string[] globs)
        {
            var buf = new StringBuilder();
            foreach (string glob in globs)
            {
                if (buf.Length > 0)
                {
                    buf.Append('|');
                }
                ConvertPatternInto(glob, buf);
            }
            return buf.ToString();
        }

        private static void ConvertPatternInto(string glob, StringBuilder buf)
        {
            for (int i = 0; i < glob.Length; ++i)
            {
                char c = glob[i];
                switch (c)
                {
                    case '$':
                    case '^':
                        if (i + 1 < glob.Length && glob[i + 1] == c)
                        {
                            buf.Append(c);
                            ++i;
                        }
                        else
                        {
                            // escape regex operators
                            buf.Append('\\');
                            buf.Append(c);
                        }
                        break;
                    case '.':
                    case '{':
                    case '[':
                    case '(':
                    case '|':
                    case ')':
                    case '+':
                        // escape regex operators
                        buf.Append('\\');
                        buf.Append(c);
                        break;
                    case '/':
                    case '\\':
                        // accept either directory separator
                        buf.Append(@"[/\\]");
                        break;
                    case '*':
                        if (i + 1 < glob.Length && glob[i + 1] == '*')
                        {
                            // match any path
                            buf.Append(".*");
                            ++i;
                        }
                        else
                        {
                            // match any name
                            buf.Append(@"[^/\\]*");
                        }
                        break;
                    case '?':
                        // match any name char
                        buf.Append(@"[^/\\]");
                        break;
                    default:
                        // passthrough char
                        buf.Append(c);
                        break;
                }
            }
        }
    };
}
