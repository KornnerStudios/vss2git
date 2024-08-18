
namespace SourceSafe.Physical.Revisions
{
    /// <summary>
    /// VSS record representing a project/file revision.
    /// </summary>
    /// <seealso cref="VssScanLogEntry"/>
    public class RevisionRecordBase : Records.VssRecordBase
    {
        public const string SIGNATURE = "EL";
        public override string Signature => SIGNATURE;

        /// <summary>
        /// This field indicates the absolute file offset of the previous log
        /// entry chunk in the file. Scanning the log of a file generally runs
        /// backwards in time. Start with the last log entry in the file, and
        /// use m_PreviousOffset to seek to the start of the preceding chunk.
        /// </summary>
        public int PrevRevOffset { get; private set; }
        public RevisionAction Action { get; private set; }
        /// <summary>
        /// Each log entry is tagged with an incrementing version number.
        /// The first entry starts at 1, and each subsequent entry increments.
        /// This is the version number value that VSS displays when showing the
        /// log of a file.
        /// </summary>
        public int Revision { get; private set; }
        /// <remarks>
        /// From VssScanLog:
        ///     Also note: on my system, I have to use gmtime() to recover the
        ///     correct local time at which an operation occurred.  This implies
        ///     that all timestamps are in local time, not GMT, which could cause
        ///     problems when accessing VSS from machines that are in different
        ///     time zones.
        /// </remarks>
        public DateTime DateTime { get; private set; }
        /// <summary>
        /// Name of user who performed the operation.
        /// </summary>
        public string? User { get; private set; }
        /// <summary>
        /// This is only used for VssOpcode_Labeled operations. It will contain
        /// the label assigned to this file.
        ///
        /// Note that a label is only applied to the selected file or directory.
        /// VSS will logically display that label when showing the change log of
        /// child files/directories, but the label itself is not written into
        /// any other files. The exception is the "data\labels" directory,
        /// which contains a file for every label ever created. This appears to
        /// be the information that is used when VSS shows labels in the history
        /// dialog. These small text files contain the path of the file that was
        /// tagged, and a timestamp (this timestamp is a 32-bit time_t value, the
        /// same as <see cref="DateTime"/>).
        /// </summary>
        public string? Label { get; private set; }
        /// <summary>
        /// Absolute offset at which the comment chunk is located.  Most types of
        /// log entries will require a comment. Even if the user did not type
        /// one in, it will still create a comment chunk. This field stores the
        /// offset of the comment.
        ///
        /// Note: If a user edited a comment at a later date, this will not modify
        /// the existing comment chunk. Instead, a new comment chunk will be
        /// appended to the end of the file, and <see cref="CommentOffset"/> will be updated
        /// to point to the new comment chunk.
        ///
        /// In the few cases where there is no comment, this will be the offset of
        /// the next chunk in the file. However, since this value will be modified
        /// when editing comments, you cannot rely upon this value to point to the
        /// start of the next chunk.
        ///
        /// This field is only meaningful if <see cref="CommentLength"/> is non-zero.
        /// </summary>
        public int CommentOffset { get; private set; }
        /// <summary>
        /// A label operation is always followed by a comment chunk. These two
        /// fields indicate the position of the comment chunk for the label.
        /// This comment chunk will normally occur immediately following the
        /// label operation. However, if someone edited the comment at a later
        /// time, the offset will be that of the edited comment.
        ///
        /// Label comments are separate from regular comments, since a label may
        /// have both types of comments. For non-label operations, these fields
        /// appear to always be zero.
        /// </summary>
        public int LabelCommentOffset { get; private set; }
        /// <summary>
        /// The length of the comment contained in the chunk referenced by the
        /// <see cref="CommentOffset"/> field. If <see cref="CommentLength"/> is zero, then there is no
        /// comment. However, most operations do require a comment, so a comment
        /// chunk will always exist for them. If the user did not type in a
        /// comment when checking in the file, a 1-byte comment will be created
        /// that contains the string "\0".
        ///
        /// This length appears to always include the '\0' terminator at the end
        /// of the comment chunk.
        /// </summary>
        public int CommentLength { get; private set; }
        public int LabelCommentLength { get; private set; }

        public static RevisionAction PeekAction(
            IO.VssBufferReader reader)
        {
            int saveOffset = reader.Offset;
            try
            {
                reader.Skip(4);
                return (RevisionAction)reader.ReadInt16();
            }
            finally
            {
                reader.Offset = saveOffset;
            }
        }

        public override void Read(
            IO.VssBufferReader reader,
            Records.RecordHeader header)
        {
            base.Read(reader, header);

            PrevRevOffset = reader.ReadInt32();
            Action = (RevisionAction)reader.ReadInt16();
            Revision = reader.ReadInt16();
            DateTime = reader.ReadDateTime();
            User = reader.ReadString(32);
            Label = reader.ReadString(32);
            CommentOffset = reader.ReadInt32();
            LabelCommentOffset = reader.ReadInt32();
            CommentLength = reader.ReadInt16();
            LabelCommentLength = reader.ReadInt16();
        }

        public override void Dump(Analysis.AnalysisTextDumper textDumper)
        {
            textDumper.WriteLine($"Prev rev offset: {PrevRevOffset:X6}");
            textDumper.WriteLine($"#{Revision:D3} {Action} by '{User}' at {DateTime}");
            if (textDumper.VerboseFilter(!string.IsNullOrEmpty(Label)))
            {
                textDumper.WriteLine($"Label: {Label}");
            }
            if (textDumper.VerboseFilter(CommentLength > 0))
            {
                textDumper.WriteLine($"Comment: length {CommentLength}, offset {CommentOffset:X6}");
            }
            if (textDumper.VerboseFilter(LabelCommentLength > 0))
            {
                textDumper.WriteLine($"Label comment: length {LabelCommentLength}, offset {LabelCommentOffset:X6}");
            }
        }
    };
}
