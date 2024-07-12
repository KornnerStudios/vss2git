
namespace SourceSafe.Physical.DeltaDiff
{
    /// <summary>
    /// Provides a seekable input stream over a file revision based on the
    /// latest revision content and a set of reverse-delta operations.
    /// </summary>
    public sealed class DeltaStream : Stream
    {
        private readonly Stream baseStream;
        private readonly DeltaSimulator simulator;
        private int length = -1;

        public DeltaStream(Stream stream, IEnumerable<DeltaOperation> operations)
        {
            baseStream = stream;
            simulator = new DeltaSimulator(operations);
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                if (length < 0)
                {
                    length = 0;
                    foreach (DeltaOperation operation in simulator.Operations)
                    {
                        length += operation.Length;
                    }
                }
                return length;
            }
        }

        public override long Position
        {
            get => simulator.FileOffset;
            set => simulator.Seek((int)value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            simulator.Read(count,
                (byte[] opData, int opOffset, int opCount) =>
                {
                    Buffer.BlockCopy(opData, opOffset, buffer, offset, opCount);
                    offset += opCount;
                    count -= opCount;
                    bytesRead += opCount;
                    return opCount;
                },
                (int opOffset, int opCount) =>
                {
                    baseStream.Seek(opOffset, SeekOrigin.Begin);
                    int opBytesRead = baseStream.Read(buffer, offset, opCount);
                    offset += opBytesRead;
                    count -= opBytesRead;
                    bytesRead += opBytesRead;
                    return opBytesRead;
                });
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    simulator.Seek((int)offset);
                    break;
                case SeekOrigin.Current:
                    simulator.Seek((int)(Position + offset));
                    break;
                case SeekOrigin.End:
                    simulator.Seek((int)(Length + offset));
                    break;
                default:
                    throw new ArgumentException("Invalid origin", nameof(origin));
            }
            return Position;
        }

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override void Flush()
        {
            // does nothing
        }

        public override void Close()
        {
            base.Close();
            baseStream.Close();
        }
    };
}
