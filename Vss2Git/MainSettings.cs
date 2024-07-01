using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Hpdi.Vss2Git
{
    public class MainSettings
    {
        public string VssDirectory { get; set; } = string.Empty;
        public string VssProject { get; set; } = string.Empty;
        public string VssExcludePaths { get; set; } = string.Empty;
        public int Encoding { get; set; } = -1;
        public string GitDirectory { get; set; } = string.Empty;
        public string DefaultEmailDomain { get; set; } = string.Empty;
        public string EmailDictionaryFile { get; set; } = string.Empty;
        public string DefaultComment { get; set; } = string.Empty;
        public string LogFile { get; set; } = string.Empty;
        public bool TranscodeComments { get; set; } = false;
        public bool ForceAnnotatedTags { get; set; } = false;
        public bool IgnoreGitErrors { get; set; } = false;
        public int AnyCommentSeconds { get; set; } = 0;
        public int SameCommentSeconds { get; set; } = 0;
        public bool IncludeVssMetaDataInComments { get; set; } = false;
        public bool IncludeIgnoredFiles { get; set; } = false;
        public bool DryRun { get; set; } = false;

        public MainSettings()
        {
        }

        public MainSettings(string filePath)
        {
            ParseSettingsFile(filePath);
        }
        /// <summary>
        /// Parse a text file into settings
        /// The expected format is "=" separated field/value pairs
        /// where the field name is the class property name converted to
        /// UNDERSCORE_CASE
        ///
        /// Example settings file:
        /// VSS_DIRECTORY=C:\Users\admin\vssbackup
        /// VSS_PROJECT=$/Project_1.root
        /// VSS_EXCLUDE_PATHS=
        /// ENCODING=1252
        /// GIT_DIRECTORY=C:\Users\admin\conversions\Project_1
        /// DEFAULT_EMAIL_DOMAIN=company.domain.com
        /// DEFAULT_COMMENT=
        /// LOG_FILE=C:\Users\admin\conversions\Project_1\Vss2Git.log
        /// TRANSCODE_COMMENTS = True
        /// FORCE_ANNOTATED_TAGS=True
        /// IGNORE_GIT_ERRORS = True
        /// ANY_COMMENT_SECONDS=15
        /// SAME_COMMENT_SECONDS=600
        ///
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>List of tuples where Item 1 is error type, and Item 2 is offending data</returns>
        public List<Tuple<string, string>> ParseSettingsFile(string filePath)
        {
            List<Tuple<string, string>> resultList = new List<Tuple<string, string>>();
            System.Reflection.PropertyInfo[] classFields = typeof(MainSettings).GetProperties();
            List<string> foundProps = new List<string>();

            using (var reader = new StreamReader(filePath))
            {
                while (!reader.EndOfStream)
                {
                    bool knownProp = false;
                    string line = reader.ReadLine();
                    string[] parts = line.Split('=');
                    string value = parts[1].Trim();

                    string importField = Regex.Replace(parts[0].Trim().ToLower(), "_([a-z])", match => match.Groups[1].Value.ToUpper());
                    importField = importField.Substring(0, 1).ToUpper() + importField.Substring(1);

                    foreach (System.Reflection.PropertyInfo classField in classFields)
                    {
                        if (classField.Name == importField)
                        {
                            Type propType = classField.PropertyType;
                            object parsedValue;

                            if (propType == typeof(bool))
                            {
                                parsedValue = bool.Parse(value);
                            }
                            else if (propType == typeof(int))
                            {
                                parsedValue = int.Parse(value);
                            }
                            else
                            {
                                parsedValue = value;
                            }

                            classField.SetValue(this, parsedValue);
                            foundProps.Add(classField.Name);
                            knownProp = true;
                            break;
                        }
                    }

                    if (!knownProp)
                    {
                        resultList.Add(new Tuple<string, string>("Unknown Field", parts[0].Trim()));
                    }

                }

                // Check if we are missing any properties
                foreach (string prop in classFields.Select(f => f.Name))
                {
                    if (!foundProps.Contains(prop))
                    {
                        resultList.Add(new Tuple<string, string>("Missing Field", prop));
                    }
                }
            }
            return resultList;
        }
    };
}
