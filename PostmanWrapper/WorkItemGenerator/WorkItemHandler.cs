using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;
using Postman.Common;
using Microsoft.VisualStudio.Services.Client;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Postman.Wrapper;
using System.Diagnostics;
using System.Xml;

/// <summary>
/// Automatic creation and update of ADO work items of type Test Case, wrapping Postman test cases into MSTest based test cases.
/// </summary>
public class WorkItemHandler
{
    readonly Setup setup;
    readonly string project;
    readonly string areaPath;
    readonly List<KeyValuePair<string, string>> customFields;
    readonly VssConnection connection;
    readonly WorkItemTrackingHttpClient witClient;

    public WorkItemHandler()
    {
        XmlDocument doc = new XmlDocument();
        doc.Load("AzureDevops.xml");
        string azureDevOpsUrl = doc.DocumentElement.SelectSingleNode("/Data/Connection/Url").InnerText;
        project = doc.DocumentElement.SelectSingleNode("/Data/Connection/Project").InnerText;
        areaPath = doc.DocumentElement.SelectSingleNode("/Data/TestCase/AreaPath").InnerText;
        customFields = new List<KeyValuePair<string, string>>();
        XmlNodeList fieldList = doc.DocumentElement.SelectNodes("/Data/TestCase/CustomFields/CustomField");
        foreach (XmlNode field in fieldList)
            customFields.Add(field.Attributes["id"].Value, field.Attributes["defaultvalue"].Value);

        setup = new Setup();
        if (setup.IsTestAgentRun)
        {
            string accessToken = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
            Assert.IsTrue(!string.IsNullOrEmpty(accessToken), "Cannot retrieve access token in pipeline");
            VssBasicCredential credentials = new VssBasicCredential("", accessToken);
            connection = new VssConnection(new Uri(azureDevOpsUrl), credentials);
        }
        else
        {
            VssClientCredentials credentials = new VssClientCredentials();
            connection = new VssConnection(new Uri(azureDevOpsUrl), credentials);
        }
        witClient = connection.GetClient<WorkItemTrackingHttpClient>();
    }

    public void UpdateProjectTestCases()
    {
        // Retrieve list of postman test cases in Wrapper project
        List<MethodInfo> testMethods = GetTestMethods(Assembly.GetAssembly(typeof(PostmanWrapper)));

        // Create/update postman test cases in ADO project
        foreach (MethodInfo mi in testMethods)
        {
            int workItemId = -1;

            foreach (CustomAttributeData cad in  mi.CustomAttributes)
            {
                if (cad.ConstructorArguments.Count == 2 && cad.ConstructorArguments[0].Value.ToString() == "AdoId")
                {
                    workItemId = int.Parse(cad.ConstructorArguments[1].Value.ToString());
                    break;
                }
            }

            if (workItemId > 0)
            {
                Wiql query = new Wiql()
                {
                    Query = string.Format("SELECT [Id] FROM workitems WHERE [System.TeamProject] = '{0}' AND [System.WorkItemType] = '{1}' AND [System.Id] = '{2}'",
                    project, ADOTestCaseWorkItemType(), workItemId)
                };
                WorkItemQueryResult result = witClient.QueryByWiqlAsync(query, project).Result;
                if (result.WorkItems.Count() == 0) throw new Exception(string.Format("Linked Test Case with prescribed id {0} could not be found",workItemId));

                WorkItemReference wir = result.WorkItems.First();
                WorkItem wi = witClient.GetWorkItemAsync(workItemId).Result;
                JsonPatchDocument patchDoc = GetPatchDocumentAutomation(mi, wi);
                if (patchDoc.Count > 0)
                {
                    Task<WorkItem> item = witClient.UpdateWorkItemAsync(patchDoc, project, wir.Id);
                    item.GetAwaiter().GetResult();
                    Console.WriteLine("Update manually linked Test Case : " + workItemId);
                }
                else
                {
                    Console.WriteLine("Manually linked Test Case already up to date : " + workItemId);
                }
            }
            else
            {
                Wiql query = new Wiql() 
                { 
                    Query = string.Format("SELECT [Id] FROM workitems WHERE [System.TeamProject] = '{0}' AND [System.WorkItemType] = '{1}' AND [System.Title] = '{2}' AND [Microsoft.VSTS.TCM.AutomatedTestType] =  '{3}'", 
                    project, ADOTestCaseWorkItemType(), ADOTestCaseTitle(mi), ADOTestCaseAutomatedTestType()) 
                };
                WorkItemQueryResult result = witClient.QueryByWiqlAsync(query, project).Result;
                if (result.WorkItems.Count() == 0)
                {
                    JsonPatchDocument patchDoc = GetPatchDocumentFull(mi, null);
                    Task<WorkItem> item = witClient.CreateWorkItemAsync(patchDoc, project, ADOTestCaseWorkItemType());
                    var res = item.GetAwaiter().GetResult();
                    workItemId = (int)res.Id;
                    Console.WriteLine("Create automatically linked Test Case : " + workItemId);
                }
                else if (result.WorkItems.Count() == 1)
                {
                    WorkItemReference wir = result.WorkItems.First();
                    workItemId = wir.Id;
                    WorkItem wi = witClient.GetWorkItemAsync(workItemId).Result;
                    JsonPatchDocument patchDoc = GetPatchDocumentFull(mi, wi);
                    if (patchDoc.Count > 0)
                    {
                        Task<WorkItem> item = witClient.UpdateWorkItemAsync(patchDoc, project, wir.Id);
                        item.GetAwaiter().GetResult();
                        Console.WriteLine("Update automatically linked Test Case : " + workItemId);
                    }
                    else
                    {
                        Console.WriteLine("Automatically linked Test Case already up to date : " + workItemId);
                    }
                }
                else
                {
                    // For now, we ignore multiple instances of the same test case representation in ADO. 
                    // Most likely multiple instances exist in ADO because the test case has been copied.
                }
            }
        }
    }

    private JsonPatchDocument GetPatchDocumentFull(MethodInfo mi, WorkItem wi)
    {
        JsonPatchDocument patchDocument = GetPatchDocumentAutomation(mi, wi);
        AddJsonPatchOperation(patchDocument, "System.Title", ADOTestCaseTitle(mi), wi);
        AddJsonPatchOperation(patchDocument, "System.Description", ADOTestCaseDescription(), wi);
        AddJsonPatchOperation(patchDocument, "System.AreaPath", ADOTestCaseAreaPath(), wi);
        foreach (var pair in customFields) AddJsonPatchOperation(patchDocument, pair.Key, pair.Value, wi);
        return patchDocument;
    }

    private JsonPatchDocument GetPatchDocumentAutomation(MethodInfo mi, WorkItem wi)
    {
        JsonPatchDocument patchDocument = new JsonPatchDocument();
        AddJsonPatchOperation(patchDocument, "Microsoft.VSTS.TCM.AutomatedTestName", ADOTestCaseAutomatedTestName(mi), wi);
        AddJsonPatchOperation(patchDocument, "Microsoft.VSTS.TCM.AutomatedTestStorage", ADOTestCaseAutomatedTestStorage(mi), wi);
        AddJsonPatchOperation(patchDocument, "Microsoft.VSTS.TCM.AutomatedTestType", ADOTestCaseAutomatedTestType(), wi);
        AddJsonPatchOperation(patchDocument, "Microsoft.VSTS.TCM.AutomatedTestId", GetGuid(), wi, false);
        return patchDocument;
    }

    private void AddJsonPatchOperation(JsonPatchDocument patchDocument,  string fieldName, string value, WorkItem existingWorkItem, bool checkEquality = true)
    {
        if (existingWorkItem == null || !existingWorkItem.Fields.ContainsKey(fieldName) || (checkEquality && existingWorkItem.Fields[fieldName].ToString() != value))
        {
            patchDocument.Add(
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = string.Format("/fields/{0}", fieldName),
                    Value = value
                }
            );
        }

    }

    private string ADOTestCaseWorkItemType()
    {
        return "Test Case";
    }

    private string ADOTestCaseTitle(MethodInfo mi)
    {
        return string.Format("{0} - {1}", mi.ReflectedType.Name, mi.Name);
    }

    private string ADOTestCaseAreaPath()
    {
        return areaPath;
    }

    private string ADOTestCaseDescription()
    {
        return "Autogenerated wrapper for postman test case.";
    }

    private string ADOTestCaseTestCaseTestType()
    {
        return "Functional";
    }

    private string ADOTestCaseTestCaseAutoStatus()
    {
        return "Automated";
    }

    private string ADOTestCaseTestCaseAutomationStatus()
    {
        return "Automated";
    }

    private string ADOTestCaseAutomatedTestName(MethodInfo mi)
    {
        return string.Format("{0}.{1}", mi.ReflectedType.FullName,mi.Name);
    }

    private string ADOTestCaseAutomatedTestStorage(MethodInfo mi)
    {
        return mi.Module.Name;
    }

    private string ADOTestCaseAutomatedTestType()
    {
        return "Postman Test Case";
    }

    private string GetGuid()
    {
        return Guid.NewGuid().ToString();
    }
    private List<MethodInfo> GetTestMethods(Assembly assembly)
    {
        var methods = assembly.GetTypes()
                              .SelectMany(t => t.GetMethods())
                              .Where(m => m.GetCustomAttributes(typeof(TestMethodAttribute), false).Length > 0)
                              .ToList<MethodInfo>();
        return methods;
    }
}
