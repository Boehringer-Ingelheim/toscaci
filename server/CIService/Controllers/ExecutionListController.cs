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
                using (WorkspaceSession session = new WorkspaceSession(request))
                {                
                    var executionLists = session.SearchForExecutionList(request.ExecutionFilter);
                    if (executionLists.Count() == 0)
                    {
                        //TODO JSON
                        return CreateErrorResponseMessage(HttpStatusCode.BadRequest, "NO execution Lists found");
                    }                                        
                    executionListIDs = executionLists.Select(t => t.UniqueId).ToList();
                }
                executionTrack = ExecutionTrackerService.CreateExecutionTracking(request);
                ExecutionHelper.TriggerExecution(executionTrack, executionListIDs);
                return CreateResponseMessage(HttpStatusCode.Accepted, executionTrack.id);
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
            return new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
            };
        }
        private HttpResponseMessage CreateErrorResponseMessage(HttpStatusCode statusCode,string errorMessage)
        {
            ExecutionResponse response = new ExecutionResponse();
            response.error = errorMessage;
            return new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
            };
        }
    }
}
