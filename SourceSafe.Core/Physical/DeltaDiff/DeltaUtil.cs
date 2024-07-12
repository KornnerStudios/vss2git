
namespace SourceSafe.Physical.DeltaDiff
{
    /// <summary>
    /// Utility methods for merging and applying reverse-delta operations.
    /// </summary>
    public static class DeltaUtil
    {
        public static ICollection<DeltaOperation> Merge(
            IEnumerable<DeltaOperation> lastRevision,
            IEnumerable<DeltaOperation> priorRevision)
        {
            var result = new List<DeltaOperation>();
            using (var merger = new DeltaSimulator(lastRevision))
            {
                foreach (DeltaOperation operation in priorRevision)
                {
                    switch (operation.Command)
                    {
                        case DeltaCommand.WriteLog:
                        {
                            result.Add(operation);
                            break;
                        }
                        case DeltaCommand.WriteSuccessor:
                        {
                            merger.Seek(operation.Offset);
                            merger.Read(operation.Length,
                                (byte[] data, int offset, int count) =>
                                {
                                    result.Add(DeltaOperation.WriteLog(data, offset, count));
                                    return count;
                                },
                                (int offset, int count) =>
                                {
                                    result.Add(DeltaOperation.WriteSuccessor(offset, count));
                                    return count;
                                });
                            break;
                        }
                    }
                }
            }
            return result;
        }

        public static void Apply(
            IEnumerable<DeltaOperation> operations,
            Stream input,
            Stream output)
        {
            const int COPY_BUFFER_SIZE = 4096;
            byte[]? copyBuffer = null;
            foreach (DeltaOperation operation in operations)
            {
                switch (operation.Command)
                {
                    case DeltaCommand.WriteLog:
                    {
                        output.Write(operation.Data.Array!,
                            operation.Data.Offset, operation.Data.Count);
                        break;
                    }
                    case DeltaCommand.WriteSuccessor:
                    {
                        input.Seek(operation.Offset, SeekOrigin.Begin);
                        if (copyBuffer == null)
                        {
                            copyBuffer = new byte[COPY_BUFFER_SIZE];
                        }
                        int remaining = operation.Length;
                        int offset = 0;
                        while (remaining > 0)
                        {
                            int count = input.Read(copyBuffer, offset, remaining);
                            if (count <= 0)
                            {
                                throw new IOException("Unexpected end of current revision file");
                            }
                            offset += count;
                            remaining -= count;
                        }
                        output.Write(copyBuffer, 0, offset);
                        break;
                    }
                }
            }
            output.Flush();
        }
    };
}
