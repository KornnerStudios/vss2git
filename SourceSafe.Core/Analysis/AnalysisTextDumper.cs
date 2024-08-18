using System.Text;

namespace SourceSafe.Analysis
{
    public sealed partial class AnalysisTextDumper
    {
        public const string DefaultSeparator = "------------------------------------------------------------";

        public string Separator { get; set; }
            = DefaultSeparator;

        public bool DumpRecordHeaders { get; set; }
            = true;

        public bool DumpVerboseData { get; set; }
            = false;

        public Action<TextWriter, Exception>? WriteExceptionCallback { get; init; } = null;

        readonly TextWriter mWriter;

        public AnalysisTextDumper(TextWriter writer)
        {
            mWriter = writer;
        }

        public void WriteSeparator()
        {
            mWriter.WriteLine(Separator);
        }

        #region Indentation state and methods
        int mIndentLevel;

        public int IndentLevel => mIndentLevel;
        public void IncreaseIndent() => mIndentLevel++;
        public void DecreaseIndent() => mIndentLevel--;

        public void WriteIndent()
        {
            string indentStr = IO.OutputUtil.GetIndentString(IndentLevel);

            mWriter.Write(indentStr);
        }
        #endregion

        public void Write(string text)
        {
            mWriter.Write(text);
        }

        public void WriteLine(string line)
        {
            WriteIndent();
            mWriter.WriteLine(line);
        }

        public void WriteLine() => mWriter.WriteLine();

        public void WriteNonIndentedLine(string line)
        {
            mWriter.WriteLine(line);
        }

        public bool VerboseFilter(bool verboseCondition)
        {
            if (DumpVerboseData)
            {
                return true;
            }

            return verboseCondition;
        }

        public void DumpAllPossiblePhysicalFiles(
            string repositoryDataPath,
            HashSet<string>? knownPhysicalNames = null,
            DumpPhysicalFileAdditionalResults? additionalResults = null)
        {
            for (char c = 'a'; c <= 'z'; ++c)
            {
                string[] dataPaths = Directory.GetFiles(
                    Path.Combine(repositoryDataPath, c.ToString()), "*.");

                foreach (string dataPath in dataPaths)
                {
                    if (!File.Exists(dataPath))
                    {
                        mWriter.WriteLine($"Physical file not found: {dataPath}");
                        continue;
                    }

                    string dataFile = Path.GetFileName(dataPath).ToUpper();
                    WriteSeparator();
                    if (knownPhysicalNames != null && knownPhysicalNames.Count > 0)
                    {
                        bool orphaned = !knownPhysicalNames.Contains(dataFile);
                        mWriter.WriteLine("{0}{1}", dataPath, orphaned ? " (orphaned)" : "");
                    }
                    else
                    {
                        mWriter.WriteLine(dataPath);
                    }

                    IncreaseIndent();
                    DumpPhysicalFile(dataPath, additionalResults);
                    DecreaseIndent();
                }
            }
        }

        public void DumpSelectPhysicalFiles(
            string repositoryDataPath,
            IEnumerable<string> physicalNames,
            DumpPhysicalFileAdditionalResults? additionalResults = null)
        {
            foreach (string specificDataFile in physicalNames)
            {
                if (string.IsNullOrEmpty(specificDataFile))
                {
                    continue;
                }

                string dataPath = Path.Combine(repositoryDataPath, specificDataFile[0].ToString(), specificDataFile);
                if (!File.Exists(dataPath))
                {
                    mWriter.WriteLine($"{nameof(DumpSelectPhysicalFiles)} select physical file not found: {dataPath}");
                    continue;
                }

                WriteSeparator();
                mWriter.WriteLine(dataPath);
                try
                {
                    IncreaseIndent();
                    DumpPhysicalFile(dataPath, additionalResults);
                    DecreaseIndent();
                }
                catch (Exception e)
                {
                    if (WriteExceptionCallback != null)
                    {
                        WriteExceptionCallback(mWriter, e);
                    }
                }
            }
        }

        public void DumpKnownUserNames(
            Logical.VssDatabase db)
        {
            if (db.KnownUserNames.Count > 0)
            {
                mWriter.WriteLine($"Known user names: ({db.KnownUserNames.Count})");
                WriteSeparator();

                IncreaseIndent();
                foreach (string userName in db.KnownUserNames)
                {
                    WriteIndent();
                    mWriter.WriteLine(userName);
                }
                DecreaseIndent();

                mWriter.WriteLine();
            }
        }

        public void DumpNamesDatFile(
            string namesDatFilePath)
        {
            var namesDatFile = new Physical.Files.Names.VssNamesDatFile(
                namesDatFilePath,
                Encoding.Default);
            namesDatFile.ReadHeaderAndNames();

            if (DumpRecordHeaders)
            {
                namesDatFile.Header.Header.Dump(this);
            }
            namesDatFile.Header.Dump(this);

            IncreaseIndent();
            foreach (Physical.Files.Names.NamesRecord record in namesDatFile.GetRecords())
            {
                if (DumpRecordHeaders)
                {
                    record.Header.Dump(this);
                }

                IncreaseIndent();
                record.Dump(this);
                DecreaseIndent();
            }
            DecreaseIndent();
        }
    };
}
