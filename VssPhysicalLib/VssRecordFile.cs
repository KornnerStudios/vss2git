/* Copyright 2009 HPDI, LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// Represents a file containing VSS records.
    /// </summary>
    /// <author>Trevor Robinson</author>
    public class VssRecordFile
    {
        private readonly string filename;
        protected readonly BufferReader reader;

        public string Filename
        {
            get { return filename; }
        }

        public VssRecordFile(string filename, Encoding encoding)
        {
            this.filename = filename;
            reader = new BufferReader(encoding, ReadFile(filename), filename);
        }

        public void ReadRecord(VssRecord record)
        {
            try
            {
                RecordHeader recordHeader = new RecordHeader();
                recordHeader.Read(reader);

                BufferReader recordReader = reader.Extract(recordHeader.Length);

                // comment records always seem to have a zero CRC
                if (recordHeader.Signature != CommentRecord.SIGNATURE)
                {
                    recordHeader.CheckCrc(Filename);
                }

                recordHeader.CheckSignature(record.Signature);

                record.Read(recordReader, recordHeader);
            }
            catch (EndOfBufferException e)
            {
                throw new RecordTruncatedException(e.Message);
            }
        }

        public void ReadRecord(VssRecord record, int offset)
        {
            reader.Offset = offset;
            ReadRecord(record);
        }

        public bool ReadNextRecord(VssRecord record)
        {
            while (reader.Remaining > RecordHeader.LENGTH)
            {
                try
                {
                    RecordHeader recordHeader = new RecordHeader();
                    recordHeader.Read(reader);

                    BufferReader recordReader = reader.Extract(recordHeader.Length);

                    // comment records always seem to have a zero CRC
                    if (recordHeader.Signature != CommentRecord.SIGNATURE)
                    {
                        recordHeader.CheckCrc(Filename);
                    }

                    if (recordHeader.Signature == record.Signature)
                    {
                        record.Read(recordReader, recordHeader);
                        return true;
                    }
                }
                catch (EndOfBufferException e)
                {
                    throw new RecordTruncatedException(e.Message);
                }
            }
            return false;
        }

        protected delegate T CreateRecordCallback<T>(
            RecordHeader recordHeader, BufferReader recordReader);

        protected T GetRecord<T>(
            CreateRecordCallback<T> creationCallback,
            bool ignoreUnknown)
            where T : VssRecord
        {
            RecordHeader recordHeader = new RecordHeader();
            recordHeader.Read(reader);

            BufferReader recordReader = reader.Extract(recordHeader.Length);

            // comment records always seem to have a zero CRC
            if (recordHeader.Signature != CommentRecord.SIGNATURE)
            {
                recordHeader.CheckCrc(Filename);
            }

            T record = creationCallback(recordHeader, recordReader);
            if (record != null)
            {
                // double-check that the object signature matches the file
                recordHeader.CheckSignature(record.Signature);
                record.Read(recordReader, recordHeader);
            }
            else if (!ignoreUnknown)
            {
                throw new UnrecognizedRecordException(recordHeader,
                    string.Format("Unrecognized record signature {0} in item file",
                    recordHeader.Signature));
            }
            return record;
        }

        protected T GetRecord<T>(
            CreateRecordCallback<T> creationCallback,
            bool ignoreUnknown,
            int offset)
            where T : VssRecord
        {
            reader.Offset = offset;
            return GetRecord<T>(creationCallback, ignoreUnknown);
        }

        protected T GetNextRecord<T>(
            CreateRecordCallback<T> creationCallback,
            bool skipUnknown)
            where T : VssRecord
        {
			int startingOffset = reader.Offset;
			int startingRemaining = reader.Remaining;
			int iters = 0;

            while (reader.Remaining > RecordHeader.LENGTH)
            {
				int recordOffset = reader.Offset;
                T record = GetRecord<T>(creationCallback, skipUnknown);
                if (record != null)
                {
                    return record;
                }

				iters++;
            }
            return null;
        }

		public static long FilesPoolMaxSize =
			//6442450944 // 6GB
			4294967296 // 4GB
			;
		public static int FilesPoolMaxEntries = 128;
		public static long FilesPoolTotalSize = 0;
		public static Dictionary<string, byte[]> FilesPool = new Dictionary<string, byte[]>();
		public static List<string> FilesMostRecentlyAccessed = new List<string>();

        private static byte[] ReadFile(string filename)
        {
            byte[] data;
			if (FilesPool.TryGetValue(filename, out data))
			{
				AccessFileInFilesPool(filename);
			}
			else
			{
				if (FilesMostRecentlyAccessed.Count >= FilesPoolMaxEntries)
					CleanUpFilesPoolForCount();

				using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
#if DEBUG
					if (Debugger.IsAttached)
						Debug.WriteLine("FilesPoolAdd {0} {1}",
							stream.Length.ToString("X8"), filename);
#endif

					long nextFilesPoolTotalSize = FilesPoolTotalSize;
					nextFilesPoolTotalSize += stream.Length;
					if (nextFilesPoolTotalSize < 0 || nextFilesPoolTotalSize >= FilesPoolMaxSize)
						CleanUpFilesPool(stream.Length);

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
			if (Debugger.IsAttached)
				Debug.WriteLine("AccessFileInFilesPool({0})", (object)filename);
#endif

			int index = FilesMostRecentlyAccessed.IndexOf(filename);
			if (index < 0)
				throw new System.InvalidOperationException(filename);
			FilesMostRecentlyAccessed.RemoveAt(index);
			FilesMostRecentlyAccessed.Add(filename);
		}

		private static void CleanUpFilesPool(long incomingFileSize)
		{
#if DEBUG
			bool debugLog = Debugger.IsAttached;
			if (debugLog)
				Debug.WriteLine("CleanUpFilesPool({0}) on {1}", incomingFileSize.ToString("X8"), FilesMostRecentlyAccessed.Count);
#endif

			long memoryToReclaimRemaing = incomingFileSize;
			for (int x = 0; x < FilesMostRecentlyAccessed.Count && memoryToReclaimRemaing > 0; )
			{
				string filename = FilesMostRecentlyAccessed[x];
				byte[] data = FilesPool[filename];

#if DEBUG
				if (debugLog)
					Debug.WriteLine("\t{0} {1} {2}",
						data.LongLength.ToString("X8"), memoryToReclaimRemaing.ToString("X8"), filename);
#endif

				FilesPoolTotalSize -= data.LongLength;
				FilesPool.Remove(filename);
				FilesMostRecentlyAccessed.RemoveAt(x);

				memoryToReclaimRemaing -= data.LongLength;
			}
		}

		private static void CleanUpFilesPoolForCount()
		{
#if DEBUG
			bool debugLog = Debugger.IsAttached;
			if (debugLog)
				Debug.WriteLine("CleanUpFilesPoolForCount() on {0}", FilesMostRecentlyAccessed.Count);
#endif

			int fileEntriesToReclaim = 4+System.Math.Max(0, FilesMostRecentlyAccessed.Count-FilesPoolMaxEntries);
			for (int x = 0; x < FilesMostRecentlyAccessed.Count && fileEntriesToReclaim > 0; fileEntriesToReclaim--)
			{
				string filename = FilesMostRecentlyAccessed[x];
				byte[] data = FilesPool[filename];

#if DEBUG
				if (debugLog)
					Debug.WriteLine("\t{0} {1} {2}",
						data.LongLength.ToString("X8"), fileEntriesToReclaim.ToString(), filename);
#endif

				FilesPoolTotalSize -= data.LongLength;
				FilesPool.Remove(filename);
				FilesMostRecentlyAccessed.RemoveAt(x);
			}
		}
	}
}
