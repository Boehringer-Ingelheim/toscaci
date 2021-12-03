using CIService.Contract;
using CIService.Enum;
using log4net;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using Tricentis.TCAPIObjects.DataObjects;
using Tricentis.TCAPIObjects.Objects;


namespace CIService.Service
{
    class WorkspaceService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WorkspaceService));
        public static void DeleteWorkspace(String workspaceID)
        {
            String ProjectPath = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(workspaceID));            
            Directory.Delete(Directory.GetParent(ProjectPath).Parent.FullName, true);
        }
        public static ProjectInformation CreateProject(CreateProject createProject)
        {
            ProjectInformation projectInfo = new ProjectInformation(createProject);            
            string tempPath = Path.Combine(Path.GetTempPath(), "tosca_project_" + new Random().Next());
            Directory.CreateDirectory(tempPath);
            projectInfo.workspacePath = Path.Combine(tempPath, createProject.name);
            projectInfo.sessionID = Convert.ToBase64String(Encoding.UTF8.GetBytes(projectInfo.workspacePath + "/" + projectInfo.name + ".tws"));
            log.InfoFormat("Creating Workspace {0} type {1} session {2}", projectInfo.name,createProject.templateType,projectInfo.sessionID);
            log.DebugFormat("Workspace path {0}", projectInfo.workspacePath);
            TCWorkspace workspace = null;
            
            try
            {
                if (createProject.templateType == WorkspaceCreationType.FROM_CONNECTION_STRING)
                {
                    string subsetPath = null;
                    string projectDefinition = null;
                    exportProject(createProject.templateConnectionString,createProject.templateConnectionWorkspaceUsername,createProject.templateConnectionWorkspacePassword, createProject.templateBranchName, tempPath, out projectDefinition, out subsetPath);
                    createProject.projectDefinition = projectDefinition;
                    createProject.subsetFiles.Add(subsetPath);
                }
                switch (createProject.dbType)
                {
                    case DBType.MSSQL_SERVER:
                        if (createProject.connectionString == null)
                        {
                            projectInfo.dbName = createToscaDatabase(createProject.name, createProject.ownerRoleName);
                            //todo connection string we should create the DB Schema aswell for full dynamic!!
                            createProject.connectionString = string.Format("Server={0};Database={1};Uid={2};Pwd={3};", ConfigurationManager.AppSettings["SQLServer"], projectInfo.dbName,createProject.templateConnectionWorkspaceUsername,createProject.templateConnectionWorkspacePassword);
                        }
                        workspace = TCAPIService.GetTCAPI().CreateMultiuserWorkspaceWithSQLServerCommon(projectInfo.workspacePath, createProject.connectionString);                        
                        break;
                    case DBType.LOCAL:
                        workspace = TCAPIService.GetTCAPI().CreateSingleuserWorkspaceSQLITEbased(projectInfo.workspacePath);
                        break;
                }

                TCProject project = workspace.GetProject();
                if (projectInfo.dbType == DBType.MSSQL_SERVER)
                {
                    project.CheckoutTree();
                }

                if (createProject.templateType != WorkspaceCreationType.EMPTY)
                {
                    importFromDefinition(workspace, createProject);
                }                

                if (projectInfo.ownerRoleName != null)
                {
                    addAddGroup(project, projectInfo.ownerRoleName, ToscaPermissionType.ADMIN);
                }
                if (projectInfo.viewerRoleName != null)
                {
                    addAddGroup(project, projectInfo.viewerRoleName, ToscaPermissionType.VIEWER);
                }

                workspace.SaveAndUnloadAllObjects();
                if (projectInfo.dbType == DBType.MSSQL_SERVER)
                {
                    project.CheckinTree("Pushing changes");
                }
            }
            finally
            {
                if (TCAPIService.GetTCAPI().IsWorkspaceOpen)
                {
                    TCAPIService.GetTCAPI()?.CloseWorkspace();
                }
            }
            return projectInfo;
        }

        private static void exportProject(string connectionString, String user, String password, string branch,string baseTempPath, out string projectDefinitionPath, out string subsetPath)
        {
            log.DebugFormat("exporting Project from {0}", connectionString);
            string tempPath = Path.Combine(baseTempPath, "tosca_project_" + new Random().Next());
            Directory.CreateDirectory(tempPath);
            subsetPath = Path.Combine(tempPath, "subset.tsu");
            projectDefinitionPath = Path.Combine(tempPath, "definition.tpr");
            Directory.CreateDirectory(tempPath);

            try
            {
                var workspacePath = Path.Combine(tempPath, "temporal");
                TCWorkspace workspace = null;                
                if (branch != null)
                {
                    workspace = TCAPIService.GetTCAPI().CreateMultiuserWorkspaceFromBranchWithSQLServerCommon(workspacePath, connectionString,user,password, branch);
                }
                else
                {
                    workspace = TCAPIService.GetTCAPI().CreateMultiuserWorkspaceWithSQLServerCommon(workspacePath, connectionString,user,password);
                }

                //workspace.GetProject().ExportProjectDefinitions("Ok", projectDefinitionPath);
                workspace.GetProject().ExportProjectDefinitions(projectDefinitionPath);
                workspace.GetProject().ExportSubset(subsetPath);
            }
            finally
            {                
                if (TCAPIService.GetTCAPI().IsWorkspaceOpen) { 
                    TCAPIService.GetTCAPI().CloseWorkspace();
                }
                
            }
        }

        private static void importFromDefinition(TCWorkspace workspace, CreateProject createProject)
        {
            log.DebugFormat("Importing Project definition on workspace {0}", createProject.name);
            TCProject project = workspace.GetProject();
            if (createProject.projectDefinition != null)
            {
                project.ImportProjectDefinitions(createProject.projectDefinition);
            }
            foreach (String subsetFilePath in createProject.subsetFiles)
            {
                log.DebugFormat("Importing Subset definition {0} on workspace {1}", subsetFilePath, createProject.name);
                TCTaskParams tcTaskParams = new TCTaskParams();
                tcTaskParams.AddParam("FilesToImport", subsetFilePath);
                //tcTaskParams.AddParam("ConfirmUseLargeSubsetImport", "Ok");
                //TODO ImportProjectSubsetWithoutImportFoldersTask to import withtout folder we need to create an addin :(
                project.ExecuteTask("CIAddin.Tasks.ImportFullProjectSubsetTask", tcTaskParams);
                //project.ImportSubset(subsetFilePath);                    
            }
        }

        private static string createToscaDatabase(string name, string groupName)
        {
            string host = ConfigurationManager.AppSettings["SQLServer"];
            string user = ConfigurationManager.AppSettings["SQLServerUsername"];
            string schema = ConfigurationManager.AppSettings["SQLSchema"];
            string pass = ConfigurationManager.AppSettings["SQLServerPassword"];
            //TODO
            SqlDataReader reader = null;
            SqlConnection conn = new SqlConnection($"Server=${host}\\{schema};Trusted_Connection=True");
            conn.Open();
            SqlCommand cmd = null;  

            //Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;
            //Connect to SQL Server
            //Create Database
            //Assign team AD Group
            return "";
        }

        private static void addAddGroup(TCProject project, string groupName, ToscaPermissionType permissionType)
        {
            TCTaskParams tcTaskParams = new TCTaskParams();
            tcTaskParams.AddParam("Continue", MsgBoxResult_OkCancel.Ok.ToString());
            tcTaskParams.AddParam("Group", groupName);
            if (!project.IsCustomTaskApplicable("CIAddin.Tasks.AddLdapGroupTask"))
            {
                throw new Exception("Maybe you don't have BI Custom Addon installed on tosca commander??");
            }

            //project.ExecuteTask("CIAddin.Tasks.AddLdapGroupTask", tcTaskParams);
            var groupTemp = project.CreateUserGroup();
            groupTemp.Name = groupName;

            TCUserGroup group = project.Groups.First(t => t.Name.Contains(groupName));
            if (permissionType.Equals(ToscaPermissionType.ADMIN))
            {
                project.AssignOwner(group);
            }
            else if (permissionType.Equals(ToscaPermissionType.VIEWER))
            {
                project.AssignViewer(group);
            }            
        }
        private static TCUserGroup createUserGroup(TCProject project, string groupName)
        {   
               
            TCUserGroup createdGroup = project.CreateUserGroup();
            createdGroup.Name = groupName;
            return createdGroup;
        }
    }    
}
