using System.Diagnostics;

namespace SourceSafe.Physical.Files
{
    /// <summary>
    /// Represents a file containing VSS records.
    /// </summary>
    public abstract class VssRecordFileBase
    {
#if DEBUG
        public static bool DebugInMemoryFilePooling { get; set; }
            = false;
#endif
        public static bool UseInMemoryFilePooling { get; set; }
            = true;

        protected readonly IO.VssBufferReader mReader;
        private readonly bool mValidateCommentRecord;
        private readonly bool mValidateRecordHeaderCrc;

        /// <summary>
        /// The file path, before any path remapping
        /// </summary>
        public string FileName { get; }
        public string RelativeFileName { get; }
        /// <summary>
        /// The actual file on we're reading from, after any path remapping
        /// </summary>
        public string ActualFileName { get; }

        public VssRecordFileBase(
            Logical.VssDatabase vssDatabase,
            string fileName)
        {
            Logical.VssDatabaseConfig.FilesConfig filesConfig = vssDatabase.Config.ConfigFiles;

            FileName = fileName;
            RelativeFileName = vssDatabase.GetDatabaseRelativePath(fileName);
            ActualFileName = vssDatabase.GetActualFullFilePath(fileName);

            mValidateCommentRecord =
                !filesConfig.IsExcludedFromValidateCommentRecord(RelativeFileName);
            mValidateRecordHeaderCrc =
                !filesConfig.IsExcludedFromRecordHeaderCrcCheck(RelativeFileName);

            byte[] fileBytes = UseInMemoryFilePooling
                ? ReadFileOrAccessFromPool(ActualFileName)
                : ReadFile(ActualFileName);
            mReader = new IO.VssBufferReader(
                vssDatabase,
                new ArraySegment<byte>(fileBytes),
                ActualFileName)
            {
                ValidateAssumedToBeAllZerosAreAllZeros =
                    !filesConfig.IsExcludedFromValidateAssumedToBeAllZerosAreAllZeros(RelativeFileName),
            };
        }

        internal void ReadRecord(Records.VssRecordBase record)
        {
            try
            {
                Records.RecordHeader recordHeader = new();
                recordHeader.Read(mReader);

                if (!mValidateCommentRecord &&
                    record.Signature == Records.CommentRecord.SIGNATURE &&
                    // recordHeader.Length is likely bunk if the signature is wrong
                    recordHeader.Signature != Records.CommentRecord.SIGNATURE)
                {
                    recordHeader.LogInvalidSignature(record.Signature, ActualFileName);

                    // leave the CommentRecord as-is (empty)
                }
                else if (recordHeader.Length < 0)
                {
                    throw new Records.RecordTruncatedException(
                        $"Record length is negative: {recordHeader.Length} at {recordHeader.Offset:X8} in {ActualFileName}");
                }
                else
                {
                    IO.VssBufferReader recordReader = mReader.ReadBytesIntoNewBufferReader(recordHeader.Length);

                    // comment records always seem to have a zero CRC
                    if (recordHeader.Signature != Records.CommentRecord.SIGNATURE)
                    {
                        recordHeader.CheckCrc(ActualFileName, mValidateRecordHeaderCrc);
                    }

                    recordHeader.CheckSignature(record.Signature);

                    record.Read(recordReader, recordHeader);
                }
            }
            catch (IO.EndOfBufferException e)
            {
                throw new Records.RecordTruncatedException(e.Message);
            }
        }

        internal void ReadRecord(Records.VssRecordBase record, int offset)
        {
            mReader.Offset = offset;
            ReadRecord(record);
        }

        internal bool ReadNextRecord(Records.VssRecordBase record)
        {
            while (mReader.RemainingSize > Records.RecordHeader.LENGTH)
            {
                try
                {
                    Records.RecordHeader recordHeader = new();
                    recordHeader.Read(mReader);

                    IO.VssBufferReader recordReader = mReader.ReadBytesIntoNewBufferReader(recordHeader.Length);

                    // comment records always seem to have a zero CRC
                    if (recordHeader.Signature != Records.CommentRecord.SIGNATURE)
                    {
                        recordHeader.CheckCrc(ActualFileName, mValidateRecordHeaderCrc);
                    }

                    if (recordHeader.Signature == record.Signature)
                    {
                        record.Read(recordReader, recordHeader);
                        return true;
                    }
                }
                catch (IO.EndOfBufferException e)
                {
                    throw new Records.RecordTruncatedException(e.Message);
                }
            }
            return false;
        }

        protected delegate T? CreateRecordCallback<T>(
            Records.RecordHeader recordHeader,
            IO.VssBufferReader recordReader);

        protected T? GetRecord<T>(
            CreateRecordCallback<T> creationCallback,
            bool ignoreUnknown)
            where T : Records.VssRecordBase
        {
            Records.RecordHeader recordHeader = new();
            recordHeader.Read(mReader);

            IO.VssBufferReader recordReader = mReader.ReadBytesIntoNewBufferReader(recordHeader.Length);

            // comment records always seem to have a zero CRC
            if (recordHeader.Signature != Records.CommentRecord.SIGNATURE)
            {
                recordHeader.CheckCrc(ActualFileName, mValidateRecordHeaderCrc);
            }

            T? record = creationCallback(recordHeader, recordReader);
            if (record != null)
            {
                // double-check that the object signature matches the file
                recordHeader.CheckSignature(record.Signature);
                record.Read(recordReader, recordHeader);
            }
            else if (!ignoreUnknown)
            {
                throw new Records.UnrecognizedRecordException(recordHeader,
                    $"Unrecognized record signature {recordHeader.Signature} in item file");
            }
            return record;
        }

        protected T? GetRecord<T>(
            CreateRecordCallback<T> creationCallback,
            bool ignoreUnknown,
            int offset)
            where T : Records.VssRecordBase
        {
            mReader.Offset = offset;
            return GetRecord<T>(creationCallback, ignoreUnknown);
        }

        protected T? GetNextRecord<T>(
            CreateRecordCallback<T> creationCallback,
            bool skipUnknown)
            where T : Records.VssRecordBase
        {
            int startingOffset = mReader.Offset;
            int startingRemaining = mReader.RemainingSize;
            int iterationCount = 0;

            while (mReader.RemainingSize > Records.RecordHeader.LENGTH)
            {
                int recordOffset = mReader.Offset;
                T? record = GetRecord(creationCallback, skipUnknown);
                if (record != null)
                {
                    return record;
                }

                iterationCount++;
            }
            return null;
        }

        private static byte[] ReadFile(string filename)
        {
            byte[] data;
            using (var stream = new FileStream(filename,
                FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
            }
            return data;
        }

        private const long FilesPoolMaxSize =
            //6442450944 // 6GB
            4294967296 // 4GB
            ;
        private const int FilesPoolMaxEntries = 128;
        private static long FilesPoolTotalSize = 0;
        public static Dictionary<string, byte[]> FilesPool { get; } = [];
        public static List<string> FilesMostRecentlyAccessed { get; } = [];

        public static void ClearInMemoryFilePooling()
        {
            FilesPool.Clear();
            FilesMostRecentlyAccessed.Clear();
            FilesPoolTotalSize = 0;
        }

        private static byte[] ReadFileOrAccessFromPool(string filename)
        {
            if (FilesPool.TryGetValue(filename, out byte[]? data))
            {
                AccessFileInFilesPool(filename);
            }
            else
            {
                if (FilesMostRecentlyAccessed.Count >= FilesPoolMaxEntries)
                {
                    CleanUpFilesPoolForCount();
                }

                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
#if DEBUG
                    if (DebugInMemoryFilePooling && Debugger.IsAttached)
                    {
                        Debug.WriteLine($"FilesPoolAdd {stream.Length:X8} {filename}");
                    }
#endif

                    long nextFilesPoolTotalSize = FilesPoolTotalSize;
                    nextFilesPoolTotalSize += stream.Length;
                    if (nextFilesPoolTotalSize < 0 || nextFilesPoolTotalSize >= FilesPoolMaxSize)
                    {
                        CleanUpFilesPool(stream.Length);
                    }

                    data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                }

                FilesPoolTotalSize += data.LongLength;
                FilesPool.Add(filename, data);
                FilesMostRecentlyAccessed.Add(filename);
            }
            return data;
        }

        private static void AccessFileInFilesPool(string filename)
        {
#if DEBUG
            if (DebugInMemoryFilePooling && Debugger.IsAttached)
            {
                Debug.WriteLine($"AccessFileInFilesPool({filename})");
            }
#endif

            int index = FilesMostRecentlyAccessed.IndexOf(filename);
            if (index < 0)
            {
                throw new InvalidOperationException(filename);
            }
            FilesMostRecentlyAccessed.RemoveAt(index);
            FilesMostRecentlyAccessed.Add(filename);
        }

        private static void CleanUpFilesPool(long incomingFileSize)
        {
#if DEBUG
            bool debugLog = DebugInMemoryFilePooling && Debugger.IsAttached;
            if (debugLog)
            {
                Debug.WriteLine($"CleanUpFilesPool({incomingFileSize:X8}) on {FilesMostRecentlyAccessed.Count}");
            }
#endif

            long memoryToReclaimRemaining = incomingFileSize;
            for (int x = 0; x < FilesMostRecentlyAccessed.Count && memoryToReclaimRemaining > 0;)
            {
                string filename = FilesMostRecentlyAccessed[x];
                byte[] data = FilesPool[filename];

#if DEBUG
                if (debugLog)
                {
                    Debug.WriteLine($"\t{data.LongLength:X8} {memoryToReclaimRemaining:X8} {filename}");
                }
#endif

                FilesPoolTotalSize -= data.LongLength;
                FilesPool.Remove(filename);
                FilesMostRecentlyAccessed.RemoveAt(x);

                memoryToReclaimRemaining -= data.LongLength;
            }
        }

        private static void CleanUpFilesPoolForCount()
        {
#if DEBUG
            bool debugLog = DebugInMemoryFilePooling && Debugger.IsAttached;
            if (debugLog)
            {
                Debug.WriteLine($"CleanUpFilesPoolForCount() on {FilesMostRecentlyAccessed.Count}");
            }
#endif

            int fileEntriesToReclaim = 4 + Math.Max(0, FilesMostRecentlyAccessed.Count - FilesPoolMaxEntries);
            for (int x = 0; x < FilesMostRecentlyAccessed.Count && fileEntriesToReclaim > 0; fileEntriesToReclaim--)
            {
                string filename = FilesMostRecentlyAccessed[x];
                byte[] data = FilesPool[filename];

#if DEBUG
                if (debugLog)
                {
                    Debug.WriteLine($"\t{data.LongLength:X8} {fileEntriesToReclaim} {filename}");
                }
#endif

                FilesPoolTotalSize -= data.LongLength;
                FilesPool.Remove(filename);
                FilesMostRecentlyAccessed.RemoveAt(x);
            }
        }
    };
}
