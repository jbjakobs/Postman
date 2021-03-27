using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace Postman.Common
{
    public class CollectionFile
    {
        public string Name;
        public List<Folder> Folders;

        public CollectionFile(string path)
        {
            Folders = new List<Folder>();

            string content = File.ReadAllText(path);
            XNode xNode = JsonConvert.DeserializeXNode(content, "root");
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xNode.ToString());

            XmlNode node = doc.SelectSingleNode("/root/info/name");
            Name = node.InnerText.RemoveSpecialCharacters();

            XmlNodeList list = doc.GetElementsByTagName("item");
            foreach (XmlNode xmlNode in list)
            {
                AddTestCase(xmlNode);
            }
        }

        private void AddTestCase(XmlNode node)
        {
            if (node.Name != "item") return;

            XmlNode childItem = null;
            XmlNode childName = null;
            XmlNode childDesciption = null;
            foreach (XmlNode n in node.ChildNodes)
            {
                if (n.Name == "item") childItem = n;
                if (n.Name == "name") childName = n;
                if (n.Name == "description") childDesciption = n;
            }
            if (childItem == null || childName == null) return;

            XmlNode childChildName = null;
            XmlNode childChildRequest = null;
            foreach (XmlNode n in childItem.ChildNodes)
            {
                if (n.Name == "name") childChildName = n;
                if (n.Name == "request") childChildRequest = n;
            }
            if (childChildName == null || childChildRequest == null) return;

            string folderName = childName.InnerText.RemoveSpecialCharacters();
            string description = string.Empty;
            if (childDesciption != null) description = childDesciption.InnerText;

            Folder folder = new Folder(folderName, description);
            Folders.Add(folder);
        }
    }

    public class Folder
    {
        public string Name;
        public string Description;

        public Folder(string myName, string myDescription)
        {
            Name = myName;
            Description = myDescription;
        }
    }
}
