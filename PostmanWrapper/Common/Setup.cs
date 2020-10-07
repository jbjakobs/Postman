using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using EnvDTE;
using EnvDTE80;

namespace Postman.Common
{
    public class Setup
    {
        readonly string gitRootFolder;

        public Setup()
        {
            if (!IsTestAgentRun) gitRootFolder = GetGitRootFolder();
        }

        public string SolutionDirectory
        {
            get
            {
                DTE2 dte = DTEHandler.GetCurrent();
                string solutionDirectory = Path.GetDirectoryName(dte.Solution.FullName);
                return solutionDirectory;
            }
        }

        public bool IsTestAgentRun
        {
            get
            {
                return !string.IsNullOrEmpty(SystemWorkFolder);
            }
        }

        public string SystemWorkFolder
        {
            get
            {
                return Environment.GetEnvironmentVariable("System_WorkFolder");
            }
        }

        public string GitRootFolder
        {
            get
            {
                return gitRootFolder;
            }
        }

        public string CollectionFileNamePattern
        {
            get
            {
                return "*.postman_collection.json";
            }
        }

        public string[] GetCollectionFilePaths()
        {
            string searchFolder = IsTestAgentRun ? SystemWorkFolder : gitRootFolder;
            string[] files = Directory.GetFiles(searchFolder, CollectionFileNamePattern, SearchOption.AllDirectories);
            return files;
        }

        private string GetGitRootFolder()
        {
            DirectoryInfo rootDir = new DirectoryInfo(SolutionDirectory);
            do
            {
                DirectoryInfo gitDir = new DirectoryInfo(Path.Combine(rootDir.FullName, ".git"));
                if (gitDir.Exists) break;
                if (rootDir.Parent == null) throw new Exception("Git root folder could not be found - no parent folder");
                rootDir = rootDir.Parent;
            }
            while (rootDir != null);

            Console.WriteLine("Git root folder : " + rootDir.FullName);

            return rootDir.FullName;
        }
    }
}
