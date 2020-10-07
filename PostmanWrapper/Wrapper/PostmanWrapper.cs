using System.IO;
using System.Linq;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Reflection;
using System;
using Postman.Common;
using System.Collections.Generic;

namespace Postman.Wrapper
{
    public class PostmanWrapper
    {
        string collectionName;
        string folder;
        string cmdOutput;
        string cmdErr;
        string cmdLine;
        string collectionFileFolder;
        string dataline;
        string datafilePath;
        Setup setup;

        public PostmanWrapper(string myCollectionName, string myFolder)
        {
            collectionName = myCollectionName;
            folder = myFolder;
            setup = new Setup();
            collectionFileFolder = GetCollectionFileFolder();
        }

        public PostmanWrapper(string myCollectionName, string myFolder, string myDataline) : this(myCollectionName, myFolder)
        {
            dataline = myDataline;
        }

        private string OutputFileName
        {
            get
            {
                return string.Format("output_{0}_{1}.json", collectionName, folder);
            }
        }

        private string OutputFilePath
        {
            get
            {
                string folder = Path.GetDirectoryName(CollectionFilePath);
                return Path.Combine(folder, OutputFileName);
            }
        }

        private string CollectionFileName
        {
            get
            {
                return string.Format("{0}.postman_collection.json", collectionName);
            }
        }

        private string CollectionFilePath
        {
            get
            {
                return Path.Combine(collectionFileFolder, CollectionFileName);
            }
        }

        private string DataFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(datafilePath))
                {
                    string collectionName = CollectionFileName.Split(".".ToCharArray())[0];
                    string pathCsv = Path.Combine(collectionFileFolder, string.Format("{0}.data.csv", collectionName));
                    string pathJson = Path.Combine(collectionFileFolder, string.Format("{0}.data.json", collectionName));
                    if (File.Exists(pathCsv))
                        datafilePath = pathCsv;
                    else if (File.Exists(pathJson))
                        datafilePath = pathJson;
                    else
                        datafilePath = string.Empty;

                    if (!string.IsNullOrEmpty(datafilePath) && !string.IsNullOrEmpty(dataline))
                    {
                        if (Path.GetExtension(datafilePath) != ".csv") throw new NotImplementedException("Only support for csv files when using dataline selection");

                        string[] contentLines = File.ReadAllLines(datafilePath);
                        List<string> datalines = new List<string>(dataline.Split(";".ToCharArray()).ToArray<string>());
                        List<string> filteredLines = new List<string>();

                        filteredLines.Add(contentLines[0]);
                        for (int i=1; i< contentLines.Length; i++)
                        {
                            string columnValue = contentLines[i].Split(",".ToCharArray())[0];
                            foreach (string dl in datalines)
                            {
                                if (columnValue.Contains(dl))
                                {
                                    filteredLines.Add(contentLines[i]);
                                    break;
                                }
                            }
                        }

                        if (filteredLines.Count == 1) throw new Exception(string.Format("No datalines with {0} found in file {1}", dataline, Path.GetFileName(datafilePath)));

                        datafilePath = Path.Combine(Path.GetDirectoryName(datafilePath), Path.GetFileNameWithoutExtension(datafilePath) + "." + new Random().Next(1000, 9999) + ".csv");
                        File.WriteAllLines(datafilePath, filteredLines.ToList());
                    }

                }

                return datafilePath;
            }
        }

        private string EnvironmentFilePath
        {
            get
            {
                string collectionName = CollectionFileName.Split(".".ToCharArray())[0];

                string pathJson = Path.Combine(collectionFileFolder, string.Format("{0}.postman_environment.json", collectionName));
                if (File.Exists(pathJson)) return pathJson;

                pathJson = Path.Combine(collectionFileFolder, string.Format("{0}.environment.json", collectionName));
                if (File.Exists(pathJson)) return pathJson;

                return string.Empty;
            }
        }
        
        private string GlobalsFilePath
        {
            get
            {
                string collectionName = CollectionFileName.Split(".".ToCharArray())[0];

                string path = Path.Combine(collectionFileFolder, string.Format("{0}.postman_globals.json", collectionName));
                if (File.Exists(path)) return path;

                path = Path.Combine(collectionFileFolder, string.Format("{0}.globals.json", collectionName));
                if (File.Exists(path)) return path;

                return string.Empty;
            }
        }

        public void Run(TestContext tc)
        {
            // Run postman
            Assert.IsTrue(File.Exists(CollectionFilePath), GetDebugInfo("Could not find Postman collection file."));
            cmdLine = GenerateNewmanCommand();
            CommandLineExecutor(cmdLine, out cmdOutput, out cmdErr);

            // Parse postman output 
            Assert.IsTrue(File.Exists(OutputFilePath), GetDebugInfo("Could not find Postman output file"));
            ParseOutputFile(tc);
        }

        private string GenerateNewmanCommand()
        {
            string environmentArg = string.IsNullOrEmpty(EnvironmentFilePath) ? string.Empty : string.Format("-e {0}", EnvironmentFilePath.EnquoteIfSpaces());
            string globalsArg = string.IsNullOrEmpty(GlobalsFilePath) ? string.Empty : string.Format("-g {0}", GlobalsFilePath.EnquoteIfSpaces());
            string dataArg = string.IsNullOrEmpty(DataFilePath) ? string.Empty : string.Format("-d {0}", DataFilePath.EnquoteIfSpaces());
            return string.Format("newman run \"{0}\" --folder {1} {2} {3} {4} --reporters cli,json --reporter-json-export \"{5}\" -n 1", CollectionFilePath, folder, globalsArg, dataArg, environmentArg, OutputFilePath);
        }

        private string GetCollectionFileFolder()
        {
            string searchFolder = setup.IsTestAgentRun ? setup.SystemWorkFolder : setup.GitRootFolder;
            string[] files = Directory.GetFiles(searchFolder, CollectionFileName, SearchOption.AllDirectories);
            if (files.Length == 1)
            {
                return Path.GetDirectoryName(files[0]);
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Could not locate unique collection file: " + files.Length);
                foreach (string f in files) sb.AppendLine("  File: " + f);
                throw new Exception(sb.ToString());
            }
        }

        private void ParseOutputFile(TestContext tc)
        {
            string content = File.ReadAllText(OutputFilePath);
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<Output>(content);
            foreach (var s in result.Run.Failures) tc.WriteLine(s.ToString());
            File.Delete(OutputFilePath);
            Assert.IsTrue(result.Run.Failures.Count() == 0, GetDebugInfo("Errors occured in Postman test execution"));
        }

        private void CommandLineExecutor(string cmdLine, out string output, out string err)
        {
            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardError = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine(cmdLine);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            output = cmd.StandardOutput.ReadToEnd();
            err = cmd.StandardError.ReadToEnd();
            cmd.WaitForExit();
        }

        private string GetDebugInfo(string additionalInfo = "")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Empty);
            sb.AppendLine("Debug information...");
            if (!string.IsNullOrEmpty(additionalInfo)) sb.AppendLine(additionalInfo);
            sb.AppendLine("Execution folder     : " + Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            sb.AppendLine("Collection file path : " + CollectionFilePath);
            sb.AppendLine("Output file path     : " + OutputFilePath);
            sb.AppendLine("System_WorkFolder    : " + setup.SystemWorkFolder);
            sb.AppendLine("Command line input   : " + cmdLine);
            sb.AppendLine("Command line output  : " + cmdOutput);
            sb.AppendLine("Command line error   : " + cmdErr);
            return sb.ToString();
        }
    }
}
