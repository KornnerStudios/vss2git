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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hpdi.VssLogicalLib;
using Hpdi.VssPhysicalLib;

namespace Hpdi.VssDump
{
    /// <summary>
    /// Dumps pretty much everything in the VSS database to the console.
    /// </summary>
    /// <author>Trevor Robinson</author>
    class Program
    {
        private const string Separator = "------------------------------------------------------------";
        private static bool DumpFileHierarchy { get; set; }
            = false;
        private static bool DumpNameFileContents { get; set; }
            = false;
        private static string OutputFileName { get; set; }
            = @"";
        private static readonly List<string> LimitLogFilesList =
        [
        ];

        static void Main(string[] args)
        {
            Encoding outputEncoding = Encoding.Default;

            bool invalidArg = false;
            int argIndex = 0;
            while (argIndex < args.Length && args[argIndex].StartsWith('/'))
            {
                string[] option = args[argIndex].Substring(1).Split(':');
                switch (option[0])
                {
                    case "encoding":
                    {
                        string encodingName;
                        if (option.Length > 1)
                        {
                            encodingName = option[1];
                        }
                        else if (argIndex + 1 < args.Length)
                        {
                            encodingName = args[++argIndex];
                        }
                        else
                        {
                            invalidArg = true;
                            goto InvalidArg;
                        }

                        Encoding encoding;
                        try
                        {
                            if (int.TryParse(encodingName, out int codePage))
                            {
                                encoding = Encoding.GetEncoding(codePage);
                            }
                            else
                            {
                                encoding = Encoding.GetEncoding(encodingName);
                            }
                        }
                        catch
                        {
                            Console.WriteLine("Invalid encoding: {0}", encodingName);
                            invalidArg = true;
                            goto InvalidArg;
                        }

                        outputEncoding = encoding;

                        break;
                    }

                    case "encodings":
                    {
                        EncodingInfo[] encodings = Encoding.GetEncodings();
                        Console.WriteLine("{0,-6} {1,-25} {2}", "CP", "IANA", "Description");
                        foreach (EncodingInfo encoding in encodings)
                        {
                            int codePage = encoding.CodePage;
                            switch (codePage)
                            {
                                case 1200:
                                case 1201:
                                case 12000:
                                case 12001:
                                    // UTF-16 and 32 are managed-only
                                    continue;
                            }
                            Console.WriteLine("{0,-6} {1,-25} {2}", codePage, encoding.Name, encoding.DisplayName);
                        }
                        return;
                    }
                }
                ++argIndex;
            }

        InvalidArg:
            if (invalidArg || argIndex >= args.Length)
            {
                Console.WriteLine("Syntax: VssDump [options] <vss-base-path>");
                Console.WriteLine("Options:");
                Console.WriteLine("  /encoding:<encoding>    Output encoding IANA name or code page");
                Console.WriteLine("  /encodings              List supported encodings and terminate");
                return;
            }

            TextWriter outputWriter;
            if (!string.IsNullOrEmpty(OutputFileName))
            {
                outputWriter = new StreamWriter(OutputFileName, false, outputEncoding);
            }
            else
            {
                outputWriter = Console.Out;
                Console.OutputEncoding = outputEncoding;
            }

            string repoPath = args[argIndex];
            var df = new VssDatabaseFactory(repoPath);
            VssDatabase db = df.Open();

            TreeDumper tree = null;
            if (DumpFileHierarchy)
            {
                tree = new TreeDumper(outputWriter)
                {
                    IncludeRevisions = false,
                };

                outputWriter.WriteLine("File hierarchy:");
                outputWriter.WriteLine(Separator);
                tree.DumpProject(db.RootProject);
                outputWriter.WriteLine();
            }

            outputWriter.WriteLine("Log file contents:");
            if (LimitLogFilesList.Count == 0)
            {
                for (char c = 'a'; c <= 'z'; ++c)
                {
                    string[] dataPaths = Directory.GetFiles(
                        Path.Combine(db.DataPath, c.ToString()), "*.");
                    foreach (string dataPath in dataPaths)
                    {
                        string dataFile = Path.GetFileName(dataPath).ToUpper();
                        outputWriter.WriteLine(Separator);
                        if (tree != null)
                        {
                            bool orphaned = !tree.PhysicalNames.Contains(dataFile);
                            outputWriter.WriteLine("{0}{1}", dataPath, orphaned ? " (orphaned)" : "");
                        }
                        else
                        {
                            outputWriter.WriteLine(dataPath);
                        }
                        DumpLogFile(outputWriter, dataPath);
                    }
                }
            }
            else
            {
                foreach (string specificDataFile in LimitLogFilesList)
                {
                    string dataPath = Path.Combine(db.DataPath, specificDataFile[0].ToString(), specificDataFile);

                    outputWriter.WriteLine(Separator);
                    outputWriter.WriteLine(dataPath);
                    DumpLogFile(outputWriter, dataPath);
                }
            }
            outputWriter.WriteLine();

            if (DumpNameFileContents)
            {
                outputWriter.WriteLine("Name file contents:");
                outputWriter.WriteLine(Separator);
                string namePath = Path.Combine(db.DataPath, "names.dat");
                DumpNameFile(outputWriter, namePath);
                outputWriter.WriteLine();
            }

            outputWriter.WriteLine(Separator);
            outputWriter.WriteLine("Project actions: {0}", FormatCollection(projectActions));
            outputWriter.WriteLine("File actions: {0}", FormatCollection(fileActions));

            if (outputWriter != Console.Out)
            {
                outputWriter.Close();
            }
        }

        private static readonly HashSet<VssPhysicalLib.Action> projectActions = [];
        private static readonly HashSet<VssPhysicalLib.Action> fileActions = [];

        private static string FormatCollection<T>(IEnumerable<T> collection)
        {
            StringBuilder buf = new();
            foreach (T item in collection)
            {
                if (buf.Length > 0)
                {
                    buf.Append(", ");
                }
                buf.Append(item);
            }
            return buf.ToString();
        }

        private static void DumpLogFile(TextWriter outputWriter, string filename)
        {
            const int kDumpIndent = 0;

            try
            {
                var itemFile = new ItemFile(filename, Encoding.Default);
                itemFile.Header.Header.Dump(outputWriter, kDumpIndent);
                itemFile.Header.Dump(outputWriter, kDumpIndent);
                SourceSafe.Physical.Records.VssRecordBase record = itemFile.GetNextRecord(true);
                int revisionIndex = -1;
                while (record != null)
                {
                    revisionIndex++;
                    record.Header.Dump(outputWriter, kDumpIndent + 1);
                    record.Dump(outputWriter, kDumpIndent + 2);
                    if (record is RevisionRecord revision)
                    {
                        if (itemFile.Header.IsProject)
                        {
                            projectActions.Add(revision.Action);
                        }
                        else
                        {
                            fileActions.Add(revision.Action);
                        }
                    }
                    record = itemFile.GetNextRecord(true);
                }
            }
            catch (Exception e)
            {
                WriteException(outputWriter, e);
            }
        }

        private static void DumpNameFile(TextWriter outputWriter, string filename)
        {
            const int kDumpIndent = 0;

            try
            {
                var nameFile = new NameFile(filename, Encoding.Default);
                nameFile.Header.Header.Dump(outputWriter, kDumpIndent);
                nameFile.Header.Dump(outputWriter, kDumpIndent);
                NameRecord name = nameFile.GetNextName();
                while (name != null)
                {
                    name.Header.Dump(outputWriter, kDumpIndent + 1);
                    name.Dump(outputWriter, kDumpIndent + 2);
                    name = nameFile.GetNextName();
                }
            }
            catch (Exception e)
            {
                WriteException(outputWriter, e);
            }
        }

        private static void WriteException(TextWriter outputWriter, Exception e)
        {
            string message = $"ERROR: {e.Message}";
            outputWriter.WriteLine(message);
            if (outputWriter != Console.Out)
            {
                Console.WriteLine(message);
                Console.WriteLine(e.StackTrace);
            }
        }
    };
}
