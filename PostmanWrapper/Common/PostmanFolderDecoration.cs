using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Postman.Common
{
    public class PostmanFolderDecoration
    {
        string metadataElementStart = "<metadata>";
        string metadataElementEnd = "</metadata>";
        XmlDocument doc;
        public List<DataTestCase> DataTestCaseList;

        public PostmanFolderDecoration(Folder folder)
        {
            DataTestCaseList = new List<DataTestCase>();

            if (folder != null && !string.IsNullOrEmpty(folder.Description) && folder.Description.Contains(metadataElementStart))
            {
                int start = folder.Description.IndexOf(metadataElementStart);
                int end = folder.Description.LastIndexOf(metadataElementEnd);
                if (start == -1) throw new Exception(string.Format("Could not find {0}  in {1}", metadataElementStart, folder.Name));
                if (end== -1) throw new Exception(string.Format("Could not find {0}  in {1}", metadataElementEnd, folder.Name));
                string description = folder.Description.Substring(start, end + metadataElementEnd.Length - start);

                doc = new XmlDocument();
                try
                {
                    doc.LoadXml(description);
                    Parse();
                }
                catch (Exception e) { throw new Exception("Error reading metadata xml : " + e.Message); }
            }
        }

        public bool InUse
        {
            get
            {
                return DataTestCaseList.Count > 0;
            }
        }

        private void Parse()
        {
            XmlNodeList nodes = doc.SelectNodes("/metadata/testcase");

            foreach (XmlNode node in nodes)
            {
                DataTestCase tc = new DataTestCase();
                if (node.Attributes["adoid"] != null) tc.Id = node.Attributes["adoid"].Value;
                if (node.Attributes["dataline"] != null) tc.Dataline = node.Attributes["dataline"].Value;
                DataTestCaseList.Add(tc);
            }
        }
    }

    public class DataTestCase
    {
        public DataTestCase()
        {
            Id = string.Empty;
            Dataline = string.Empty;
        }

        public string Id { get;set; }

        public string Dataline { get; set; }
    }
}
