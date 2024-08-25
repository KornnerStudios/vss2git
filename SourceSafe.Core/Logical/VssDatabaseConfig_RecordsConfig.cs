using System.Text.Json.Serialization;

namespace SourceSafe.Logical
{
    partial class VssDatabaseConfig
    {
        public class RecordsConfig
        {
            public bool DeltaRecordsReadCheckForMissingStopCommands { get; set; }
                = false;

            #region PhysicalFileNamesWithBadCheckoutRecordData
            /// <summary>
            /// Names (ALL UPPERCASE) of physical files which have <see cref="CheckoutRecord"/>
            /// that contain garbled or otherwise junk bytes.
            /// </summary>
            public HashSet<string>? PhysicalFileNamesWithBadCheckoutRecordData { get; set; }

            [JsonIgnore]
            internal bool HasAnyPhysicalFileNamesWithBadCheckoutRecordData =>
                PhysicalFileNamesWithBadCheckoutRecordData != null &&
                PhysicalFileNamesWithBadCheckoutRecordData.Count > 0;

            public bool PhysicalFileHasBadCheckoutRecordData(
                string physicalName)
            {
                bool hasBadData = false;
                if (PhysicalFileNamesWithBadCheckoutRecordData != null)
                {
                    if (PhysicalFileNamesWithBadCheckoutRecordData.Contains(physicalName.ToUpperInvariant()))
                    {
                        hasBadData = true;
                    }
                }
                return hasBadData;
            }
            #endregion

            internal void ForcePathCollectionsToExpectedFormat()
            {
                PhysicalFileNamesWithBadCheckoutRecordData =
                    CollectionUtils.ConvertToUpper(PhysicalFileNamesWithBadCheckoutRecordData);
            }
        };

        // Populate, so values are read into the new'd object below
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public RecordsConfig ConfigRecords { get; init; } = new();
    };
}
