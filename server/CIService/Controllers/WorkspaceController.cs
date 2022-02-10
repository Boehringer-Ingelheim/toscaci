using CIService.Contract;
using CIService.Enum;
using CIService.Service;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace CIService.Controllers
{    

    [RoutePrefix("api/v2/workspace")]
    public class WorkspaceController : ApiController
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WorkspaceController));

        [HttpPost]
        //[RequestSizeLimit(10L * 1024L * 1024L * 1024L)]
        //[RequestFormLimits(MultipartBodyLengthLimit = 10L * 1024L * 1024L * 1024L)]
        //[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        [Route("")]
        public async Task<HttpResponseMessage> CreateWorkspace() // return status
        {
            log.Debug("Creating Workspace Request...");
            if (!this.Request.Content.IsMimeMultipartContent())
            {
                return CreateErrorResponseMessage("Content is not Multipart Content.", HttpStatusCode.UnsupportedMediaType);
            }
            var tempPath = Path.Combine(Path.GetTempPath(), "tosca_" + new Random().Next());
            log.DebugFormat("Workspce Temp Path {0}...", tempPath);
            Directory.CreateDirectory(tempPath);            
            var provider = new MultipartFormDataStreamProvider(tempPath);
            try
            {
                // Read the form data.
                await this.Request.Content.ReadAsMultipartAsync(provider);
                var createProject = new CreateProject();
                createProject.name = provider.FormData.Get("name");
                
                createProject.templateType = EnumHelper.GetWorkspaceCreationTypeFromString(provider.FormData.Get("templateType"));
                createProject.templateBranchName = provider.FormData.Get("templateBranchName");
                createProject.templateConnectionString = provider.FormData.Get("templateConnectionString");
                createProject.templateConnectionWorkspaceUsername = provider.FormData.Get("templateConnectionWorkspaceUsername");
                createProject.templateConnectionWorkspacePassword = provider.FormData.Get("templateConnectionWorkspacePassword");


                createProject.dbType = EnumHelper.getDBTypeFromString(provider.FormData.Get("dbType"));                
                createProject.connectionString = provider.FormData.Get("connectionString");
    
                createProject.ownerRoleName = provider.FormData.Get("ownerRoleName");
                createProject.viewerRoleName = provider.FormData.Get("viewerRoleName");
                

                // This illustrates how to get the file names.
                ArrayList subsetFiles = new ArrayList();
                string projectDefintion = null;
                foreach (MultipartFileData file in provider.FileData)
                {
                    var cleanFileName = file.Headers.ContentDisposition.FileName.Replace("\"", "");
                    if (cleanFileName == "") { continue; }
                    if(!cleanFileName.EndsWith(".tsu") && !cleanFileName.EndsWith(".tpr")) {
                        return CreateErrorResponseMessage("Only Subset(.tsu) or Project Definition(.tpr) import files are allowed.", HttpStatusCode.BadRequest);                            
                    }
                    var destPath = Path.Combine(Path.GetDirectoryName(file.LocalFileName), cleanFileName);                   
                    if (cleanFileName.EndsWith(".tsu"))
                    {
                        subsetFiles.Add(destPath);
                    }
                    if (cleanFileName.EndsWith(".tpr"))
                    {
                        projectDefintion = destPath;
                    }
                    File.Move(file.LocalFileName, destPath);
                    
                }
                createProject.subsetFiles = subsetFiles;
                createProject.projectDefinition = projectDefintion;
                
                var projectInfo = WorkspaceService.CreateProject(createProject);
                
                return CreateResponseMessage(projectInfo, HttpStatusCode.OK);
            }
            catch (System.Exception e)
            {
                log.Error("Error when creating workspace", e);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, e);
            }
            finally
            {
                Directory.Delete(tempPath,true);
            }
        }

        [HttpDelete]
        //[RequestSizeLimit(10L * 1024L * 1024L * 1024L)]
        //[RequestFormLimits(MultipartBodyLengthLimit = 10L * 1024L * 1024L * 1024L)]
        //[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
        [Route("{workspaceID}")]
        public HttpResponseMessage DeleteWorkspace(string workspaceID)
        {
            log.InfoFormat("Deleting workspace {0}", workspaceID);
            List<ExecutionTracking> runningExecution = ExecutionTrackerService.HaveExecutionRunning(workspaceID);
            if (runningExecution.Count() > 0)
            {
                return CreateErrorResponseMessage("Can not be deleted because running executions", HttpStatusCode.Conflict);
            }
            try {
                List<ExecutionTracking> executions = ExecutionTrackerService.GetExecutionsByWorkspace(workspaceID);
                foreach (ExecutionTracking execution in executions)
                {
                    if (!execution.request.PreserveWorkspaces)
                    {
                        log.InfoFormat("Deleting workspace directory {0}", execution.workspaceDirectory);
                        //WorkspaceService.DeleteWorkspace(workspaceID);
                    }
                    else
                    {
                        log.InfoFormat("Preserving workspace directory {0}", execution.workspaceDirectory);
                    }
                }

                ExecutionTrackerService.CleanExecutionsByWorkspace(workspaceID);
                return new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK
                };
            }
            catch(Exception ex)
            {
                log.Error("Error when deleting workspace", ex);
                return CreateErrorResponseMessage(ex.Message, HttpStatusCode.InternalServerError);
            }            
        }

        private HttpResponseMessage CreateResponseMessage(ProjectInformation project, HttpStatusCode statusCode)
        {
            CreateWorkspaceResponse response = new CreateWorkspaceResponse();
            response.project = project;
            return new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
            };
        }
        private HttpResponseMessage CreateErrorResponseMessage(string errorMessage, HttpStatusCode statusCode)
        {
            CreateWorkspaceResponse response = new CreateWorkspaceResponse();
            response.error = errorMessage;
            return new HttpResponseMessage()
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonConvert.SerializeObject(response), Encoding.UTF8, "application/json")
            };
        }
    }
}
