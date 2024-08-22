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

namespace Hpdi.VssDump
{
    public sealed class VssDumpOptions
    {
        public string RepositoryPath { get; set; }
            = @"";

        public string OutputFileName { get; set; }
            = @"";

        public string EncodingNameOrCodePage { get; set; }
            = null;

        public bool DumpRecordHeaders { get; set; }
            = true;
        public bool DumpDeltaRecordOperations { get; set; }
            = true;

        public bool DumpKnownUserNames { get; set; }
            = false;

        public bool DumpFileHierarchy { get; set; }
            = false;

        public bool DumpNameFileContents { get; set; }
            = false;

        public List<string> LimitPhysicalFilesList { get; set; } =
        [
            //string.Empty,
        ];

        public Encoding GetEncoding()
        {
            Encoding encoding = Encoding.Default;

            if (!string.IsNullOrEmpty(EncodingNameOrCodePage))
            {
                if (int.TryParse(EncodingNameOrCodePage, out int codePage))
                {
                    encoding = Encoding.GetEncoding(codePage);
                }
                else
                {
                    encoding = Encoding.GetEncoding(EncodingNameOrCodePage);
                }
            }
            return encoding;
        }
    };

    /// <summary>
    /// Dumps pretty much everything in the VSS database to the console.
    /// </summary>
    class Program
    {
        static bool ParseArgs(
            string[] args,
            ref VssDumpOptions dumpOptions)
        {
            bool invalidArg = false;
            int argIndex = 0;
            while (argIndex < args.Length && args[argIndex].StartsWith('/'))
            {
                string currentArg = args[argIndex].Substring(1);
                // Split on ':' and remove empty entries
                // file path may contain ':' so there should be a space after the option's ':' in those cases
                // so that the file path is read as a single argument
                // E.g. "/json: C:\path\to\file.json"
                string[] option = currentArg.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                switch (option[0])
                {
                    #region "json"
                    case "json":
                    {
                        string jsonPath;
                        if (option.Length > 1)
                        {
                            jsonPath = option[1];
                        }
                        else if (argIndex + 1 < args.Length)
                        {
                            jsonPath = args[++argIndex];
                        }
                        else
                        {
                            invalidArg = true;
                            goto InvalidArg;
                        }

                        try
                        {
                            dumpOptions = System.Text.Json.JsonSerializer.Deserialize<VssDumpOptions>(
                                File.ReadAllText(jsonPath),
                                new System.Text.Json.JsonSerializerOptions
                                {
                                    AllowTrailingCommas = true,
                                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                                })
                                !;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Invalid json file: {0}", jsonPath);
                            Console.WriteLine(ex.Message);
                            invalidArg = true;
                            goto InvalidArg;
                        }

                        return true; // json file should contain the full suite of options
                    }
                    #endregion

                    #region "encoding"
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

                        try
                        {
                            dumpOptions.EncodingNameOrCodePage = encodingName;
                            _ = dumpOptions.GetEncoding();
                        }
                        catch
                        {
                            Console.WriteLine("Invalid encoding: {0}", encodingName);
                            invalidArg = true;
                            dumpOptions.EncodingNameOrCodePage = null;
                            goto InvalidArg;
                        }

                        break;
                    }
                    #endregion

                    #region "encodings"
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
                        return false;
                    }
                    #endregion
                }
                ++argIndex;
            }

        InvalidArg:
            if (invalidArg || argIndex >= args.Length)
            {
                Console.WriteLine("Syntax: VssDump [options] <vss-base-path>");
                Console.WriteLine("Options:");
                Console.WriteLine("  /json:<file>            path to json file defining dump options");
                Console.WriteLine("  /encoding:<encoding>    Output encoding IANA name or code page");
                Console.WriteLine("  /encodings              List supported encodings and terminate");
                return false;
            }

            dumpOptions.RepositoryPath = args[argIndex];

            return true;
        }

        static void Main(string[] args)
        {
            var dumpOptions = new VssDumpOptions()
            {
                RepositoryPath = @"",
                OutputFileName = @"",
                EncodingNameOrCodePage = null,
                DumpRecordHeaders = true,
                DumpKnownUserNames = false,
                DumpFileHierarchy = false,
                DumpNameFileContents = false,
                LimitPhysicalFilesList =
                [
                    //string.Empty
                ],
            };

            if (!ParseArgs(args, ref dumpOptions))
            {
                return;
            }

            #region Setup output
            Encoding outputEncoding = dumpOptions.GetEncoding();

            TextWriter outputWriter;
            if (!string.IsNullOrEmpty(dumpOptions.OutputFileName))
            {
                outputWriter = new StreamWriter(dumpOptions.OutputFileName, false, outputEncoding);
            }
            else
            {
                outputWriter = Console.Out;
                Console.OutputEncoding = outputEncoding;
            }
            #endregion

            // GetFullPath will also normalize the path (namely, use backslashes)
            SourceSafe.Logical.VssDatabase db = new(System.IO.Path.GetFullPath(dumpOptions.RepositoryPath), Encoding.Default);

            #region Setup AnalysisTextDumper
            SourceSafe.Analysis.AnalysisTextDumper analysisTextDumper = new(outputWriter)
            {
                DumpRecordHeaders = dumpOptions.DumpRecordHeaders,
                DumpDeltaRecordOperations = dumpOptions.DumpDeltaRecordOperations,
                WriteExceptionCallback = WriteException,
            };
            SourceSafe.Analysis.AnalysisTextDumper.DumpFileHierarchyAdditionalResults
                dumpFileHierarchyAdditionalResults = null;
            SourceSafe.Analysis.AnalysisTextDumper.DumpPhysicalFileAdditionalResults
                dumpPhysicalFilesAdditionalResults = new();

            SourceSafe.IO.VssBufferReader.GlobalTextDumperHack = analysisTextDumper;
            #endregion

            if (dumpOptions.DumpKnownUserNames)
            {
                analysisTextDumper.DumpKnownUserNames(db);
            }

            if (dumpOptions.DumpFileHierarchy)
            {
                dumpFileHierarchyAdditionalResults = new();

                analysisTextDumper.DumpFileHierarchy(
                    db,
                    includeRevisions: false,
                    dumpFileHierarchyAdditionalResults);
            }

            #region Dump physical files
            outputWriter.WriteLine("Physical file contents:");
            if (dumpOptions.LimitPhysicalFilesList.Count == 0)
            {
                analysisTextDumper.DumpAllPossiblePhysicalFiles(
                    db.DataPath,
                    dumpFileHierarchyAdditionalResults?.OutEnumeratedPhysicalNames,
                    dumpPhysicalFilesAdditionalResults);
            }
            else
            {
                analysisTextDumper.DumpSelectPhysicalFiles(
                    db.DataPath,
                    dumpOptions.LimitPhysicalFilesList,
                    dumpPhysicalFilesAdditionalResults);
            }
            outputWriter.WriteLine();
            #endregion

            #region Dump names.dat
            if (dumpOptions.DumpNameFileContents)
            {
                outputWriter.WriteLine("Name file contents:");
                analysisTextDumper.WriteSeparator();
                string namePath = Path.Combine(db.DataPath, "names.dat");

                try
                {
                    analysisTextDumper.DumpNamesDatFile(namePath);
                }
                catch (Exception e)
                {
                    WriteException(outputWriter, e);
                }

                outputWriter.WriteLine();
            }
            #endregion

            if (dumpPhysicalFilesAdditionalResults != null)
            {
                dumpPhysicalFilesAdditionalResults.DumpFoundRevisionActions(analysisTextDumper);
                dumpPhysicalFilesAdditionalResults.DumpFoundUserAndMachineNames(analysisTextDumper);
            }

            if (outputWriter != Console.Out)
            {
                outputWriter.Close();
            }

            SourceSafe.IO.VssBufferReader.GlobalTextDumperHack = null;
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
