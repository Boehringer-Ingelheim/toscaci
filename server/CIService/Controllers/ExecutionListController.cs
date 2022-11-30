using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using CIService.Contract;
using CIService.Service;
using CIService.Tosca;
using CIService.Helper;
using log4net;

namespace CIService.Controllers
{
    [RoutePrefix("api/v2/execution")]
    public class ExecutionListController : ApiController
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ExecutionListController));

        [HttpPost]
        [Route("")]
        public HttpResponseMessage CreateExecution([FromBody] ExecutionRequest request)
        {
            AuthHelper.BindExecutionAuthentication(this.Request,request);
            ExecutionTracking executionTrack = null;
            try
            {
                log.InfoFormat("Request Test Execution on workspace session {0}", request.sessionID);
                List<ExecutionTracking> runningExecutions = ExecutionTrackerService.HaveExecutionRunning();
                if (runningExecutions != null && runningExecutions.Count()>0)
                {
                    log.WarnFormat("Execution Request on workspace {0} can not be placed because an execution is already running ", request.sessionID);
                    return CreateErrorResponseMessage(HttpStatusCode.Conflict,$"Could not create a new execution since the following execution(s) are still running: {String.Join(",",runningExecutions.Select(x => x.status.ToString()))}");
                }
                List<String> executionListIDs = new List<String>();
                List<ExecutionList> responseExecutionList = new List<ExecutionList>();
                using (WorkspaceSession session = new WorkspaceSession(request))
                {            
                    //Check Execution Lists exists and are not empty
                    var executionLists = session.SearchForExecutionList(request.ExecutionFilter);
                    if (executionLists == null || !executionLists.Any())
                    {
                        log.WarnFormat("No execution Lists founds, criteria {0}", request.ExecutionFilter);
                        return CreateErrorResponseMessage(HttpStatusCode.BadRequest, "NO execution Lists found");
                    }                    
                    foreach (var executionList in executionLists)
                    {
                        var resExecutionList = new ExecutionList();
                        resExecutionList.name = executionList.DisplayedName;
                        var executionEntries = executionList.Search($"=>SUBPARTS:ExecutionEntry");
                        if (executionEntries == null || !executionEntries.Any())
                        {
                            log.WarnFormat("Execution List {0}  no Test Cases found", executionList);
                            return CreateErrorResponseMessage(HttpStatusCode.BadRequest, string.Format("Execution List {0} is empty", executionList.DisplayedName));
                        }
                        resExecutionList.entries = executionEntries.Select(t => t.DisplayedName).ToList();
                        responseExecutionList.Add(resExecutionList);
                    }                    
                    executionListIDs = executionLists.Select(t => t.UniqueId).ToList();

                    //Check Report exists and it's in the proper folder
                    foreach(var report in request.Reports)
                    {
                        var tql = String.Format("->PROJECT->SUBPARTS:TCFolder[(Name=?\"{0}\")]->SUBPARTS:ReportDefinition[(Name=?\"{1}\")]", "Reporting", report);
                        if (session.SearchFor(tql).Count() == 0)
                        {
                            log.WarnFormat("Report {0}  not found", report);
                            return CreateErrorResponseMessage(HttpStatusCode.BadRequest, String.Format("Report {0} does not exists or it's not in the root Reporting folder, please check https://documentation.tricentis.com/tosca/1300/en/content/reporting/print_report.htm for more details", report));
                        }

                    }
                    
                }
                executionTrack = ExecutionTrackerService.CreateExecutionTracking(request);
                ExecutionHelper.TriggerExecution(executionTrack, executionListIDs);
                ExecutionResponse response = new ExecutionResponse();
                response.executionID = executionTrack.id;
                response.executionLists = responseExecutionList;
                return CreateResponseMessage(HttpStatusCode.Accepted, response);
            }catch(Tricentis.TCAPIObjects.Exceptions.TCApiLoginFailedException ex){
                return CreateErrorResponseMessage(HttpStatusCode.Unauthorized, ex.Message);
            }  catch (Exception ex)
            {
                if (executionTrack != null) { 
                    ExecutionTrackerService.FailExecutionTrackingStatus(executionTrack.id, ex);
                }
                return CreateErrorResponseMessage(HttpStatusCode.InternalServerError, ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        private HttpResponseMessage CreateResponseMessage(HttpStatusCode statusCode,string executionID)
        {
            ExecutionResponse response = new ExecutionResponse();
            response.executionID = executionID;
            return CreateResponseMessage(statusCode, response);
        }
        private HttpResponseMessage CreateErrorResponseMessage(HttpStatusCode statusCode,string errorMessage)
        {
            ExecutionResponse response = new ExecutionResponse();
            response.error = errorMessage;
            return CreateResponseMessage(statusCode, response);
        }

        private HttpResponseMessage CreateResponseMessage(HttpStatusCode statusCode, ExecutionResponse response)
        {  
            return new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
            };
        }
    }
}
