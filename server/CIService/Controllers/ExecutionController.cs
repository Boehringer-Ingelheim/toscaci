using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http;
using CIService.Contract;
using CIService.Enum;
using CIService.Service;
using Newtonsoft.Json;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CIService.Controllers
{
    [RoutePrefix("api/v2/execution/{id}")]
    public class ExecutionController : ApiController
    {


        [HttpDelete]
        [Route("")]
        public HttpResponseMessage CancelExecution(string id)
        {
            var executionTrack = ExecutionTrackerService.GetExecutionTracking(id);
            if (executionTrack == null)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" was not found on the machine!");// return 404
            }
        }

        [HttpGet]
        [Route("")]
        public HttpResponseMessage Get(string id) // return status
        {
            var executionTrack = ExecutionTrackerService.GetExecutionTracking(id);
            if (executionTrack==null)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" was not found on the machine!");// return 404
            }
            return CreateResponseMessage(HttpStatusCode.OK, executionTrack);
        }


        [HttpGet]
        [Route("xunit")]
        public HttpResponseMessage GetXunitList(string id)
        {
            var executionTrack = ExecutionTrackerService.GetExecutionTracking(id);
            if (executionTrack == null)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" was not found on the machine!");// return 404
            }

            if (executionTrack.status != ExecutionStatus.Completed)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" is in {executionTrack.status}, can not extract xunit");
            }

            string[] reportList = Directory.GetFiles(executionTrack.xunitPath);
            return CreateResponseMessage(HttpStatusCode.OK, executionTrack.xunitPath, reportList);
        }

        [HttpGet]
        [Route("xunit/{xunitID}")]
        public HttpResponseMessage GetReport(string id, string xunitID)
        {
            var executionTrack = ExecutionTrackerService.GetExecutionTracking(id);
            if (executionTrack == null)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" was not found on the machine!");// return 404
            }

            if (executionTrack.status != ExecutionStatus.Completed)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" is in {executionTrack.status}, can not extract xunit");
            }

            string reportFilePath = Path.Combine(executionTrack.xunitPath, Encoding.UTF8.GetString(Convert.FromBase64String(xunitID)));
            var fileInfo = new System.IO.FileInfo(reportFilePath);
            FileStream filestream = fileInfo.OpenRead();
            HttpResponseMessage responseMsg = new HttpResponseMessage(HttpStatusCode.OK);
            responseMsg.Content = new StreamContent(filestream);
            responseMsg.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            responseMsg.Content.Headers.ContentDisposition.FileName = Path.GetFileName(reportFilePath);
            responseMsg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            responseMsg.Content.Headers.ContentLength = filestream.Length;
            return responseMsg;
        }


        [HttpGet]
        [Route("report")]
        public HttpResponseMessage GetReportList(string id)
        {
            var executionTrack = ExecutionTrackerService.GetExecutionTracking(id);
            if (executionTrack == null)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" was not found on the machine!");// return 404
            }

            if (executionTrack.status != ExecutionStatus.Completed)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" is in {executionTrack.status}, can not extract xunit");
            }

            string[] reportList = Directory.GetFiles(executionTrack.reportPath);
            return CreateResponseMessage(HttpStatusCode.OK, executionTrack.reportPath,reportList);
        }
 
        [HttpGet]
        [Route("report/{reportID}")]
        public HttpResponseMessage GetReport(string id,string reportID)
        {
            var executionTrack = ExecutionTrackerService.GetExecutionTracking(id);
            if (executionTrack == null)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" was not found on the machine!");// return 404
            }

            if (executionTrack.status != ExecutionStatus.Completed)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" is in {executionTrack.status}, can not extract xunit");
            }

            string reportFilePath = Path.Combine(executionTrack.reportPath, Encoding.UTF8.GetString(Convert.FromBase64String(reportID)));
            var fileInfo = new System.IO.FileInfo(reportFilePath);
            FileStream filestream = fileInfo.OpenRead();
            HttpResponseMessage responseMsg = new HttpResponseMessage(HttpStatusCode.OK);            
            responseMsg.Content = new StreamContent(filestream);
            responseMsg.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            responseMsg.Content.Headers.ContentDisposition.FileName = Path.GetFileName(reportFilePath);
            responseMsg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            responseMsg.Content.Headers.ContentLength = filestream.Length;
            return responseMsg;
        }

        [HttpGet]
        [Route("artifact")]
        public HttpResponseMessage GetArtifactList(string id)
        {
            var executionTrack = ExecutionTrackerService.GetExecutionTracking(id);
            if (executionTrack == null)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" was not found on the machine!");// return 404
            }

            if (executionTrack.status != ExecutionStatus.Completed)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" is in {executionTrack.status}, can not extract xunit");
            }

            string[] reportList = Directory.GetFiles(executionTrack.artifactPath);
            return CreateResponseMessage(HttpStatusCode.OK, executionTrack.artifactPath, reportList);
        }

        [HttpGet]
        [Route("artifact/{artifactID}")]
        public HttpResponseMessage GetArtifact(string id, string artifactID)
        {
            var executionTrack = ExecutionTrackerService.GetExecutionTracking(id);
            if (executionTrack == null)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" was not found on the machine!");// return 404
            }

            if (executionTrack.status != ExecutionStatus.Completed)
            {
                return CreateErrorResponseMessage(HttpStatusCode.NotFound, $"The execution \"{id}\" is in {executionTrack.status}, can not extract xunit");
            }

            string artifactFilePath = Path.Combine(executionTrack.artifactPath, Encoding.UTF8.GetString(Convert.FromBase64String(artifactID)));
            FileStream filestream = new System.IO.FileInfo(artifactFilePath).OpenRead();
            HttpResponseMessage responseMsg = new HttpResponseMessage(HttpStatusCode.OK);
            responseMsg.Content = new StreamContent(filestream);
            responseMsg.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
            responseMsg.Content.Headers.ContentDisposition.FileName = Path.GetFileName(artifactFilePath); ;
            responseMsg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            responseMsg.Content.Headers.ContentLength = filestream.Length;
            return responseMsg;
        }


        private HttpResponseMessage CreateResponseMessage(HttpStatusCode statusCode, ExecutionTracking executionTracking)
        {
            ExecutionResponse response = new ExecutionResponse();
            response.status = executionTracking.status.ToString();
            if (executionTracking.error != null) { 
                response.error = executionTracking.error.ToString();
            }
            return new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
            };
        }
        private HttpResponseMessage CreateErrorResponseMessage(HttpStatusCode statusCode, string errorMessage)
        {
            ExecutionResponse response = new ExecutionResponse();
            response.error = errorMessage;
            return new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
            };
        }

        private HttpResponseMessage CreateResponseMessage(HttpStatusCode statusCode,string baseDirectory, string[] fileList)
        {
            ExecutionResponse response = new ExecutionResponse();
            response.files = new List<CIService.Contract.FileInfo>();
            foreach (String file in fileList)
            {
                var fileInfo = new Contract.FileInfo();
                fileInfo.path=file.Replace(baseDirectory + "\\", "").Replace("\\","/");
                fileInfo.size = new System.IO.FileInfo(file).Length;
                fileInfo.id = Convert.ToBase64String(Encoding.UTF8.GetBytes(fileInfo.path));
                response.files.Add(fileInfo);
            }
            return new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
            };
        }
    }
}
