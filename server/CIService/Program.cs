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

namespace CIService
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program));
        static void Main(string[] args)
        {
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
