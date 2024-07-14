using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceSafe.Physical.Records
{
    /// <summary>
    /// Flags enumeration for a VSS file.
    /// </summary>
    [Flags]
    public enum VssItemFileFlags
    {
        Locked = 0x01,
        Binary = 0x02,
        LatestOnly = 0x04,
        // #TODO 0x08?
        // #TODO 0x10?
        Shared = 0x20,
        CheckedOut = 0x40,
    };
}
