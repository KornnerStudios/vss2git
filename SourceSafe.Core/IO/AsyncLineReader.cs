using System.Text;

namespace SourceSafe.IO
{
    /// <summary>
    /// Asynchronously reads complete lines of text from a stream as they become available.
    /// </summary>
    public sealed class AsyncLineReader
    {
        private const int DEFAULT_BUFFER_SIZE = 4096;
        private const int DEFAULT_MAX_LINE = 4096;

        private readonly Stream mStream;
        private readonly Decoder mDecoder;

        private readonly byte[] mReadBuffer; // circular
        private int mReadOffset;
        private int mDecodeOffset;
        private int mUnDecodedBytes;

        private readonly char[] mDecodeBuffer; // linear
        private int mCopyOffset;
        private int mCopyLimit;

        private readonly StringBuilder mLineBuilder = new();
        private int mMaxLineLength;

        private bool mReadPending;

        public bool EndOfFile { get; private set; }

        public event EventHandler? DataReceived;

        public AsyncLineReader(Stream stream)
            : this(stream, Encoding.Default, DEFAULT_BUFFER_SIZE, DEFAULT_MAX_LINE)
        {
        }

        public AsyncLineReader(Stream stream, Encoding encoding, int bufferSize, int maxLineLength)
        {
            mStream = stream;
            mDecoder = encoding.GetDecoder();
            mReadBuffer = new byte[bufferSize];
            mDecodeBuffer = new char[bufferSize];
            mMaxLineLength = maxLineLength;
            StartRead();
        }

        public string? ReadLine()
        {
            bool found = false;
            lock (mReadBuffer)
            {
                do
                {
                    while (mCopyOffset < mCopyLimit)
                    {
                        char c = mDecodeBuffer[mCopyOffset++];
                        mLineBuilder.Append(c);
                        if (c == '\n' || mLineBuilder.Length == mMaxLineLength)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found && mUnDecodedBytes > 0)
                    {
                        // undecoded bytes may wrap around buffer, in which case two decodes are necessary
                        int readTailCount = mReadBuffer.Length - mDecodeOffset;
                        int decodeCount = Math.Min(mUnDecodedBytes, readTailCount);

                        // need to leave room for an extra char in case one is flushed out from the last decode
                        decodeCount = Math.Min(decodeCount, mDecodeBuffer.Length - 1);

                        mCopyOffset = 0;
                        mCopyLimit = mDecoder.GetChars(
                            mReadBuffer, mDecodeOffset, decodeCount, mDecodeBuffer, mCopyOffset, EndOfFile);

                        mUnDecodedBytes -= decodeCount;
                        mDecodeOffset += decodeCount;
                        if (mDecodeOffset == mReadBuffer.Length)
                        {
                            mDecodeOffset = 0;
                        }
                    }
                }
                while (!found && mCopyOffset < mCopyLimit);

                if (!mReadPending && !EndOfFile)
                {
                    StartRead();
                }

                if (EndOfFile && mLineBuilder.Length > 0)
                {
                    mLineBuilder.Append(Environment.NewLine);
                    found = true;
                }
            }

            string? result = null;
            if (found)
            {
                result = mLineBuilder.ToString();
                mLineBuilder.Length = 0;
            }
            return result;
        }

        /*protected virtual*/ void OnDataReceived()
        {
            EventHandler? handler = DataReceived;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        // Assumes buffer lock is held or called from constructor.
        private void StartRead()
        {
            int readCount = 0;
            if (mDecodeOffset > mReadOffset)
            {
                readCount = mDecodeOffset - mReadOffset;
            }
            else if (mReadOffset > mDecodeOffset || mUnDecodedBytes == 0)
            {
                readCount = mReadBuffer.Length - mReadOffset;
            }
            if (readCount > 0)
            {
                mStream.BeginRead(mReadBuffer, mReadOffset, readCount, ReadCallback, null);
                mReadPending = true;
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            lock (mReadBuffer)
            {
                try
                {
                    mReadPending = false;

                    int count = mStream.EndRead(ar);
                    if (count > 0)
                    {
                        mUnDecodedBytes += count;
                        mReadOffset += count;
                        if (mReadOffset == mReadBuffer.Length)
                        {
                            mReadOffset = 0;
                        }

                        StartRead();
                    }
                    else
                    {
                        // zero-length read indicates end of file
                        EndOfFile = true;
                    }
                }
                catch
                {
                    // simulate end of file on read error
                    EndOfFile = true;
                }
            }

            OnDataReceived();
        }
    };
}
