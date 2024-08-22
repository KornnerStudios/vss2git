﻿
namespace SourceSafe.Physical.Records
{
    // #REVIEW this can probably be a struct
    /// <summary>
    /// Represents the header of a VSS record.
    /// </summary>
    public sealed class RecordHeader
    {
        public const int LENGTH = 8;

        // #TODO add JSON flag to control this behavior
        public static bool IgnoreCrcErrors { get; set; }
            = false;

        public int Length { get; private set; }
        public string? Signature { get; private set; }
        public ushort FileCrc { get; private set; }

        public int Offset { get; private set; }
        public ushort ActualCrc { get; private set; }
        public bool IsCrcValid => FileCrc == ActualCrc;

        public void CheckSignature(string expected)
        {
            if (Signature != expected)
            {
                throw new RecordNotFoundException(
                    $"Unexpected record signature: expected={expected}, actual={Signature}");
            }
        }
        public void LogInvalidSignature(string expected, string fileName)
        {
            string message = $"Unexpected record signature: expected={expected}, actual={Signature} at {Offset:X8} in {fileName}";

            IO.VssBufferReader.GlobalTextDumperHack?.WriteLine(message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        public void CheckCrc(string fileName)
        {
            if (!IsCrcValid)
            {
                if (IgnoreCrcErrors)
                {
                    string message = $"CRC error in {Signature} record: expected={FileCrc}, actual={ActualCrc} at {Offset:X8} in {fileName}";

                    IO.VssBufferReader.GlobalTextDumperHack?.WriteLine(message);
                    System.Diagnostics.Debug.WriteLine(message);
                    return;
                }

                throw new RecordCrcException(this,
                    $"CRC error in {Signature} record: expected={FileCrc}, actual={ActualCrc} in {fileName}");
            }
        }

        private void CheckFileLength(IO.VssBufferReader reader)
        {
            if (Length > reader.RemainingSize)
            {
                throw new IO.EndOfBufferException(
                    $"Attempted read of {Length} bytes with only {reader.RemainingSize} bytes remaining in from {reader.FileNameAndSegment}");
            }
        }

        public void Read(IO.VssBufferReader reader)
        {
            Offset = reader.Offset;
            Length = reader.ReadInt32();
            Signature = reader.ReadSignature(2);
            FileCrc = (ushort)reader.ReadInt16();
            CheckFileLength(reader);
            ActualCrc = reader.Crc16(Length);
        }

        public void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            textDumper.WriteIndent();
            textDumper.Write($"Signature: {Signature} - Offset: {Offset:X8} - Length: {Length:X8}");
            if (!IsCrcValid)
            {
                textDumper.Write($" - INVALID CRC: expected={FileCrc:X4} actual={ActualCrc:X4})");
            }
            textDumper.WriteLine();
        }
    };
}
