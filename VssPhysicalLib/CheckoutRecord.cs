﻿/* Copyright 2009 HPDI, LLC
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

using System;
using System.IO;

namespace Hpdi.VssPhysicalLib
{
    /// <summary>
    /// VSS record representing a file checkout.
    /// </summary>
    /// <author>Trevor Robinson</author>
    /// <seealso cref="VssScanCheckout"/>
    public sealed class CheckoutRecord : VssRecord
    {
        public const string SIGNATURE = "CF";
        public override string Signature => SIGNATURE;

        // Note that these strings are only used after a file has been checked
        // out. For newly-created files, these strings are empty. (This also
        // appears to be true for a file after it has been shared -- the shared
        // link starts out with no check-out strings.) The strings are retained
        // after the file is checked in, providing a record of who made the most
        // recent change to the file.

        /// <summary>
        /// Name of the user who currently holds the check-out on the file, or
        /// performed the last check-in.
        /// </summary>
        public string User { get; private set; }
        /// <summary>
        /// This is the time at which the file was last checked-out. (Is it
        /// updated when the file is checked in?) This is a 32-bit time_t value.
        /// </summary>
        public DateTime CheckOutDateTime { get; private set; }
        /// <summary>
        /// Absolute path ("D:\foo\bar.h") at which the file is checked out.
        /// </summary>
        public string WorkingDir { get; private set; }
        /// <summary>
        /// Network name for the machine where the file is checked out.
        /// </summary>
        public string Machine { get; private set; }
        /// <summary>
        /// This stores the path to the file within VSS, which can be used to
        /// disambiguate which link is being used when the file is shared between
        /// multiple projects.
        ///
        /// This always starts with // "$/", which indicates the root of the VSS
        /// source tree.
        /// </summary>
        public string Project { get; private set; }
        /// <summary>
        /// When a file is checked out, the user is (usually) prompted to enter a
        /// comment. That string is stored here, and will serve as the default
        /// comment when the file is eventually checked in.
        /// </summary>
        public string Comment { get; private set; }
        /// <summary>
        /// If the file is checked out, this indicates the version at which the
        /// file was checked out.
        ///
        /// If the file is not checked out, this is zero.
        /// </summary>
        public short Revision { get; private set; }
        /// <summary>
        /// This field is always set to 0x40 when a file is checked out.
        /// If the file is not checked out, it is zero.
        ///
        /// No other values have been observed here, but there may be flags
        /// defined in case the file is currently checked out multiple times?
        /// </summary>
        public short Flags { get; private set; }
        public int PrevCheckoutOffset { get; private set; }
        public int ThisCheckoutOffset { get; private set; }
        /// <summary>
        /// The version number applied to the most recent check-in. For a
        /// newly-created file, this is zero. It looks like a file that has been
        /// created, checked out once, but not yet checked in, will also still
        /// have this value set to zero.
        /// </summary>
        public int Checkouts { get; private set; }

        public bool Exclusive => (Flags & 0x40) != 0;

        public override void Read(SourceSafe.IO.VssBufferReader reader, RecordHeader header)
        {
            base.Read(reader, header);

            User = reader.ReadString(32);
            CheckOutDateTime = reader.ReadDateTime();
            WorkingDir = reader.ReadString(260);
            Machine = reader.ReadString(32);
            Project = reader.ReadString(260);
            Comment = reader.ReadString(64);
            Revision = reader.ReadInt16();
            Flags = reader.ReadInt16();
            PrevCheckoutOffset = reader.ReadInt32();
            // #TODO this is actually two 16-bit values, see VssScanCheckout
            ThisCheckoutOffset = reader.ReadInt32();
            Checkouts = reader.ReadInt32();
        }

        public override void Dump(TextWriter writer, int indent)
        {
            string indentStr = DumpGetIndentString(indent);

            writer.Write(indentStr);
            writer.WriteLine($"User: {User} @ {CheckOutDateTime}");
            writer.Write(indentStr);
            writer.WriteLine($"Working: {WorkingDir}");
            writer.Write(indentStr);
            writer.WriteLine($"Machine: {Machine}");
            writer.Write(indentStr);
            writer.WriteLine($"Project: {Project}");
            if (!string.IsNullOrEmpty(Comment))
            {
                writer.Write(indentStr);
                writer.WriteLine($"Comment: {Comment}");
            }
            writer.Write(indentStr);
            writer.WriteLine($"Revision: #{Revision:D3}");
            writer.Write(indentStr);
            writer.WriteLine($"Flags: {Flags:X4}{(Exclusive ? " (exclusive)" : "")}");
            writer.Write(indentStr);
            writer.WriteLine($"Prev checkout offset: {PrevCheckoutOffset:X6}");
            writer.Write(indentStr);
            writer.WriteLine($"This checkout offset: {ThisCheckoutOffset:X6}");
            writer.Write(indentStr);
            writer.WriteLine($"Checkouts: {Checkouts}");
        }
    }
}
