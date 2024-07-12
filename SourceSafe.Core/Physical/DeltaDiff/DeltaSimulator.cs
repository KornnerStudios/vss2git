
namespace SourceSafe.Physical.DeltaDiff
{
    delegate int FromLogCallback(byte[] data, int offset, int count);
    delegate int FromSuccessorCallback(int offset, int count);

    /// <summary>
    /// Simulates stream-like traversal over a set of revision delta operations.
    /// </summary>
    sealed class DeltaSimulator : IDisposable
    {
        private IEnumerator<DeltaOperation>? enumerator;
        private int operationOffset;
        private bool eof;

        public IEnumerable<DeltaOperation> Operations { get; }

        public int FileOffset { get; private set; }

        public DeltaSimulator(IEnumerable<DeltaOperation> operations)
        {
            Operations = operations;
            Reset();
        }

        public void Dispose()
        {
            if (enumerator != null)
            {
                enumerator.Dispose();
                enumerator = null;
            }
        }

        public void Seek(int offset)
        {
            if (offset != FileOffset)
            {
                if (offset < FileOffset)
                {
                    Reset();
                }
                while (FileOffset < offset && !eof)
                {
                    int seekRemaining = offset - FileOffset;
                    int operationRemaining = enumerator!.Current.Length - operationOffset;
                    if (seekRemaining < operationRemaining)
                    {
                        operationOffset += seekRemaining;
                        FileOffset += seekRemaining;
                    }
                    else
                    {
                        FileOffset += operationRemaining;
                        eof = !enumerator.MoveNext();
                        operationOffset = 0;
                    }
                }
            }
        }

        public void Read(int length, FromLogCallback fromLog, FromSuccessorCallback fromSuccessor)
        {
            while (length > 0 && !eof)
            {
                DeltaOperation operation = enumerator!.Current;
                int operationRemaining = operation.Length - operationOffset;
                int count = Math.Min(length, operationRemaining);
                int bytesRead;
                if (operation.Command == DeltaCommand.WriteLog)
                {
                    // #REVIEW: this was refactored from the original code:
                        //bytesRead = fromLog(operation.Data.Array, operation.Data.Offset + operationOffset, count);
                    ArraySegment<byte> readSlice = operation.GetWriteLogDataSlice(operationOffset, count);
                    bytesRead = fromLog(readSlice.Array!, readSlice.Offset, readSlice.Count);
                }
                else
                {
                    bytesRead = fromSuccessor(operation.Offset + operationOffset, count);
                }
                if (bytesRead == 0)
                {
                    break;
                }
                operationOffset += bytesRead;
                FileOffset += bytesRead;
                if (length >= operationRemaining)
                {
                    eof = !enumerator.MoveNext();
                    operationOffset = 0;
                }
                length -= bytesRead;
            }
        }

        private void Reset()
        {
            enumerator?.Dispose();
            enumerator = Operations.GetEnumerator();
            eof = !enumerator.MoveNext();
            operationOffset = 0;
            FileOffset = 0;
        }
    };
}
