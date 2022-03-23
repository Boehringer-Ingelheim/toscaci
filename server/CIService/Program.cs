using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.SelfHost;

// TO BE DELETED
using System.IO;
using CIService.Contract;
using CIService.Enum;
using CIService.Service;
using CIService.Tosca;
using Tricentis.Automation.Contract;
using Tricentis.Common.Security.O_CF_NR;
using Tricentis.TCAPIObjects.DataObjects;
using Tricentis.TCAPIObjects.Objects;
using ExecutionList = Tricentis.TCAPIObjects.Objects.ExecutionList;
//

namespace CIService
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));

        static void ConsoleLog(string s)
        {
            Console.WriteLine(String.Format("{0}: {1}", DateTime.Now, s));
        }

        static void PrintWorkspaceReport(string workspaceFile)
        {
            try
            {
                ConsoleLog("Printing workspace report mode");
                try
                {
                    ConsoleLog(String.Format("Loading workspace: {0}", workspaceFile));

                    string sessionID = Convert.ToBase64String(Encoding.UTF8.GetBytes(workspaceFile));
                    using (WorkspaceSession session = new WorkspaceSession(sessionID))
                    {
                        foreach (string testType in new List<string>() { "Installation", "Integration", "Acceptance" })
                        {
                            ConsoleLog(String.Format("Trying to load {0} execution list", testType));

                            // Build a filter
                            List<KeyValue> executionFilter = new List<KeyValue>();
                            KeyValue kv = new KeyValue();
                            kv.key = "TestType";
                            kv.value = testType.ToLowerInvariant();
                            executionFilter.Add(kv);

                            try
                            {
                                // Search for an execution of the matching type
                                string executionListID = session.SearchForExecutionList(executionFilter).First().UniqueId;
                                var executionList = session.GetWorkspace().GetTCObject(executionListID) as ExecutionList;
                                ConsoleLog(String.Format("Found execution list ID: {0}", executionListID));

                                string toscaProjectDir = Directory.GetParent(Path.GetDirectoryName(workspaceFile)).FullName;
                                string reportName = String.Format("Tosca{0}Report", testType);
                                string reportDir = Path.Combine(toscaProjectDir, String.Format("execution_{0}", executionListID));
                                Directory.CreateDirectory(reportDir);
                                string reportPath = Path.Combine(reportDir, String.Format("{0}_Debug.pdf", reportName));
                                ConsoleLog(String.Format("Starting print ({0})", reportPath));                        
                                
                                executionList.PrintReport("ToscaIntegrationReport", reportPath);  // ToscaIntegrationReport is hardcoded!!!
                                ConsoleLog("Finished print");
                            }
                            catch (Exception e)
                            {
                                ConsoleLog(String.Format("Cannot print. Error is {0}", e.Message));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

            }
            finally
            {
                Console.Write("Press Enter to continue (start CIService ...)");
                Console.Read();
            }            
        }

        static void Main(string[] args)
        {
            // Sanity Check Process is 64bits
            if (!System.Environment.Is64BitProcess)
            {
                Console.WriteLine("This process must compiled and run in 64 bits architecture!");
                Environment.Exit(1);
            }

            if (args.Length >= 2)
            {
                // Print report
                if (args[0] == "-p")
                {
                    string workspaceFile = args[1];
                    PrintWorkspaceReport(workspaceFile);
                }
            }

            XmlConfigurator.Configure();
            Console.WriteLine(System.Security.Principal.WindowsIdentity.GetCurrent().Name);
            Uri myUri = new Uri(@"http://localhost:"+ ConfigurationManager.AppSettings["listenPort"]);
            var config = new System.Web.Http.SelfHost.HttpSelfHostConfiguration(myUri);
            config.MapHttpAttributeRoutes();
            config.MaxReceivedMessageSize = Int64.Parse(ConfigurationManager.AppSettings["maxSubsetSize"]); 
            config.Routes.MapHttpRoute(
                "API Default", "api/{controller}/{id}",
                new { id = RouteParameter.Optional });
            
            //hostConfig.Routes.MapHttpRoute(
            //    "Static", "{*url}",
            //    new { controller = "StaticFiles", action = "Index" });

            using (HttpSelfHostServer server = new HttpSelfHostServer(config))
            {

                server.OpenAsync().Wait();
                
                Console.WriteLine("ExecutionService hosted on " + myUri.AbsoluteUri + ". It can be used now.");
                Console.WriteLine("Press Enter to quit.");
                Console.ReadLine();
            }
        }
    }
}
