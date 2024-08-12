using System.Globalization;
using System.Text;

namespace SourceSafe.IO
{
    /// <summary>
    /// Writes log messages to an optional stream.
    /// </summary>
    public sealed class SimpleLogger : IDisposable
    {
        public static readonly SimpleLogger Null = new((Stream?)null);

        private const string SectionSeparator = "------------------------------------------------------------";

        private readonly Stream? mBaseStream;
        private readonly Encoding mEncoding;
        private readonly IFormatProvider mFormatProvider;

        public TextWriter? EchoWriter { get; set; }

        public SimpleLogger(string filename)
            : this(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
        }

        public SimpleLogger(Stream? baseStream)
            : this(baseStream, Encoding.Default, CultureInfo.InvariantCulture)
        {
        }

        public SimpleLogger(Stream? baseStream, Encoding encoding, IFormatProvider formatProvider)
        {
            mBaseStream = baseStream;
            mEncoding = encoding;
            mFormatProvider = formatProvider;
        }

        public void Dispose()
        {
            mBaseStream?.Dispose();
            EchoWriter?.Dispose();
            // Ensure we don't hang on to the echo writer,
            // in the event someone modifies the 'Null' logger's echo
            EchoWriter = null;
        }

        public void Flush()
        {
            mBaseStream?.Flush();
            EchoWriter?.Flush();

        }

        public void Write(bool value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }
        }

        public void Write(char value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }

            EchoWriter?.Write(value);
        }

        public void Write(char[] buffer)
        {
            if (mBaseStream != null && buffer != null)
            {
                Write(buffer, 0, buffer.Length);
            }
        }

        public void Write(decimal value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }

            EchoWriter?.Write(value);
        }

        public void Write(double value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }

            EchoWriter?.Write(value);
        }

        public void Write(float value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }

            EchoWriter?.Write(value);
        }

        public void Write(int value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }

            EchoWriter?.Write(value);
        }

        public void Write(long value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }

            EchoWriter?.Write(value);
        }

        public void Write(object value)
        {
            if (mBaseStream != null && value != null)
            {
                Write(value.ToString()!);
            }

            EchoWriter?.Write(value);
        }

        public void Write(string value)
        {
            if (mBaseStream != null && value != null)
            {
                WriteInternal(value);
                mBaseStream.Flush();
            }

            EchoWriter?.Write(value);
        }

        public void Write(uint value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }

            EchoWriter?.Write(value);
        }

        public void Write(ulong value)
        {
            if (mBaseStream != null)
            {
                Write(value.ToString());
            }

            EchoWriter?.Write(value);
        }

        public void Write(string format, params object[] arg)
        {
            if (mBaseStream != null && arg != null)
            {
                Write(string.Format(mFormatProvider, format, arg));
            }

            if (arg != null)
            {
                EchoWriter?.Write(string.Format(mFormatProvider, format, arg));
            }
        }

        public void Write(char[] buffer, int index, int count)
        {
            if (mBaseStream != null && buffer != null)
            {
                WriteInternal(buffer, index, count);
                mBaseStream.Flush();
            }

            EchoWriter?.Write(buffer!, index, count);
        }

        public void WriteLine()
        {
            Write(Environment.NewLine);

            EchoWriter?.WriteLine();
        }

        public void WriteLine(bool value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(char value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(char[] buffer)
        {
            if (mBaseStream != null && buffer != null)
            {
                WriteInternal(buffer, 0, buffer.Length);
                WriteLine();
            }

            EchoWriter?.WriteLine(buffer);
        }

        public void WriteLine(decimal value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(double value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(float value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(int value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(long value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(object value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString()!);
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(string value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value);
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(uint value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(ulong value)
        {
            if (mBaseStream != null)
            {
                WriteInternal(value.ToString());
                WriteLine();
            }

            EchoWriter?.WriteLine(value);
        }

        public void WriteLine(string format, params object[] arg)
        {
            if (mBaseStream != null && arg != null)
            {
                WriteInternal(string.Format(mFormatProvider, format, arg));
                WriteLine();
            }

            EchoWriter?.WriteLine(format, arg ?? []);
        }

        public void WriteLine(char[] buffer, int index, int count)
        {
            if (mBaseStream != null && buffer != null)
            {
                WriteInternal(buffer, index, count);
                WriteLine();
            }

            EchoWriter?.WriteLine(buffer!, index, count);
        }

        public void WriteSectionSeparator()
        {
            WriteLine(SectionSeparator);
            EchoWriter?.WriteLine(SectionSeparator);
        }

        private void WriteInternal(string value)
        {
            byte[] bytes = mEncoding.GetBytes(value);
            mBaseStream!.Write(bytes, 0, bytes.Length);
        }

        private void WriteInternal(char[] buffer, int index, int count)
        {
            byte[] bytes = mEncoding.GetBytes(buffer, index, count);
            mBaseStream!.Write(bytes, 0, bytes.Length);
        }
    };
}
