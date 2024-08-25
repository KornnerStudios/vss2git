using System.Text.Json.Serialization;

namespace SourceSafe.Logical
{
    partial class VssDatabaseConfig
    {
        public class FilesConfig
        {
            #region ExcludedFromValidateAssumedToBeAllZerosAreAllZeros
            /// <summary>
            /// Relative paths of files which should be excluded from
            /// <see cref="VssBufferReader.ValidateAssumedToBeAllZerosAreAllZeros"/>
            /// </summary>
            public HashSet<string>? ExcludedFromValidateAssumedToBeAllZerosAreAllZeros { get; set; }

            [JsonIgnore]
            private bool AllExcludedFromValidateAssumedToBeAllZerosAreAllZeros =>
                ExcludedFromValidateAssumedToBeAllZerosAreAllZeros != null &&
                ExcludedFromValidateAssumedToBeAllZerosAreAllZeros.Count == 0;

            public bool IsExcludedFromValidateAssumedToBeAllZerosAreAllZeros(
                string relativePath)
            {
                bool isExcluded = AllExcludedFromValidateAssumedToBeAllZerosAreAllZeros;

                if (!isExcluded && ExcludedFromValidateAssumedToBeAllZerosAreAllZeros != null)
                {
                    if (ExcludedFromValidateAssumedToBeAllZerosAreAllZeros.Contains(relativePath.ToUpperInvariant()))
                    {
                        isExcluded = true;
                    }
                }

                return isExcluded;
            }
            #endregion

            #region ExcludedFromValidateCommentRecord
            /// <summary>
            /// Relative paths of file which should be excluded from
            /// <see cref="VssRecordFileBase.mValidateCommentRecord"/>
            /// </summary>
            public HashSet<string>? ExcludedFromValidateCommentRecord { get; set; }

            [JsonIgnore]
            private bool AllExcludedFromValidateCommentRecord =>
                ExcludedFromValidateCommentRecord != null &&
                ExcludedFromValidateCommentRecord.Count == 0;

            public bool IsExcludedFromValidateCommentRecord(
                string relativePath)
            {
                bool isExcluded = AllExcludedFromValidateCommentRecord;

                if (!isExcluded && ExcludedFromValidateCommentRecord != null)
                {
                    if (ExcludedFromValidateCommentRecord.Contains(relativePath.ToUpperInvariant()))
                    {
                        isExcluded = true;
                    }
                }

                return isExcluded;
            }
            #endregion

            #region ExcludedFromRecordHeaderCrcCheck
            /// <summary>
            /// Relative paths of file which should be excluded from
            /// <see cref="RecordHeader.CheckCrc"/>
            /// </summary>
            public HashSet<string>? ExcludedFromRecordHeaderCrcCheck { get; set; }

            [JsonIgnore]
            private bool AllExcludedFromRecordHeaderCrcCheck =>
                ExcludedFromRecordHeaderCrcCheck != null &&
                ExcludedFromRecordHeaderCrcCheck.Count == 0;

            public bool IsExcludedFromRecordHeaderCrcCheck(
                string relativePath)
            {
                bool isExcluded = AllExcludedFromRecordHeaderCrcCheck;

                if (!isExcluded && ExcludedFromRecordHeaderCrcCheck != null)
                {
                    if (ExcludedFromRecordHeaderCrcCheck.Contains(relativePath.ToUpperInvariant()))
                    {
                        isExcluded = true;
                    }
                }

                return isExcluded;
            }
            #endregion

            internal void ForcePathCollectionsToExpectedFormat()
            {
                ExcludedFromValidateAssumedToBeAllZerosAreAllZeros =
                    CollectionUtils.ConvertForwardSlashesToBackSlashAndToUpper(ExcludedFromValidateAssumedToBeAllZerosAreAllZeros);

                ExcludedFromValidateCommentRecord =
                    CollectionUtils.ConvertForwardSlashesToBackSlashAndToUpper(ExcludedFromValidateCommentRecord);

                ExcludedFromRecordHeaderCrcCheck =
                    CollectionUtils.ConvertForwardSlashesToBackSlashAndToUpper(ExcludedFromRecordHeaderCrcCheck);
            }
        };

        // Populate, so values are read into the new'd object below
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public FilesConfig ConfigFiles { get; init; } = new();
    };
}
