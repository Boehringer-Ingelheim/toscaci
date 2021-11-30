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

namespace CIService.Controllers
{
    [RoutePrefix("api/v2/execution")]
    public class ExecutionListController : ApiController
    {      


        [HttpPost]
        [Route("")]
        public HttpResponseMessage CreateExecution([FromBody] ExecutionRequest request)
        {
            AuthHelper.BindExecutionAuthentication(this.Request,request);
            ExecutionTracking executionTrack = null;
            try
            {
                List<ExecutionTracking> runningExecutions = ExecutionTrackerService.HaveExecutionRunning();
                if (runningExecutions.Count()>0)
                {                    
                    return CreateErrorResponseMessage(HttpStatusCode.Conflict,$"Could not create a new execution since the following execution(s) are still running: {String.Join(",",runningExecutions.Select(x => x.status.ToString()))}");
                }
                List<String> executionListIDs = new List<String>();
                List<ExecutionList> responseExecutionList = new List<ExecutionList>();
                using (WorkspaceSession session = new WorkspaceSession(request))
                {            
                    //Check Execution Lists exists and are not empty
                    var executionLists = session.SearchForExecutionList(request.ExecutionFilter);
                    if (executionLists.Count() == 0)
                    {
                        return CreateErrorResponseMessage(HttpStatusCode.BadRequest, "NO execution Lists found");
                    }                    
                    foreach (var executionList in executionLists)
                    {
                        var resExecutionList = new ExecutionList();
                        resExecutionList.name = executionList.DisplayedName;
                        var executionEntries = executionList.Search($"=>SUBPARTS:ExecutionEntry");
                        if (executionEntries.Count() == 0)
                        {
                            return CreateErrorResponseMessage(HttpStatusCode.BadRequest, String.Format("Execution List {0} is empty", executionList.DisplayedName));
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
                            return CreateErrorResponseMessage(HttpStatusCode.BadRequest, String.Format("Report {0} does not exists or it's not in the root Reporting folder", report));
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
