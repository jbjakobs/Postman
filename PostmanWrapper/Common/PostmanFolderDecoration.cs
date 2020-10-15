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
        public List<TestCase> TestCaseList;

        public PostmanFolderDecoration(Item item)
        {
            TestCaseList = new List<TestCase>();

            if (item != null && !string.IsNullOrEmpty(item.description) && item.description.Contains(metadataElementStart))
            {
                int start = item.description.IndexOf(metadataElementStart);
                int end = item.description.LastIndexOf(metadataElementEnd);
                if (start == -1) throw new Exception(string.Format("Could not find {0}  in {1}", metadataElementStart, item.name));
                if (end== -1) throw new Exception(string.Format("Could not find {0}  in {1}", metadataElementEnd, item.name));
                string description = item.description.Substring(start, end + metadataElementEnd.Length - start);

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
                return TestCaseList.Count > 0;
            }
        }

        private void Parse()
        {
            XmlNodeList nodes = doc.SelectNodes("/metadata/testcase");

            foreach (XmlNode node in nodes)
            {
                TestCase tc = new TestCase();
                if (node.Attributes["adoid"] != null) tc.Id = node.Attributes["adoid"].Value;
                if (node.Attributes["dataline"] != null) tc.Dataline = node.Attributes["dataline"].Value;
                TestCaseList.Add(tc);
            }
        }
    }

    public class TestCase
    {
        public TestCase()
        {
            Id = string.Empty;
            Dataline = string.Empty;
        }

        public string Id { get;set; }

        public string Dataline { get; set; }
    }
}
