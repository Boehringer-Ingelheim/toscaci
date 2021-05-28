using CIService.Contract;
using CIService.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tricentis.TCAPIObjects.Objects;

namespace CIService.Tosca
{
    class WorkspaceSession : IDisposable
    {
        private string workspacePath;
        private string workspaceSessionID;
        private TCWorkspace workspace;


        public WorkspaceSession(string sessionID )
        {
            InitializeWorkspace(sessionID, "Admin", "");
        }

        public WorkspaceSession(string sessionID,String userName,String password)
        {
            InitializeWorkspace(sessionID, userName, password);
        }

        public WorkspaceSession(ExecutionRequest execution)
        {
            InitializeWorkspace(execution.sessionID, execution.WorkspaceUsername, execution.WorkspacePassword);
        }

        private void InitializeWorkspace(string sessionID, String userName, String password)
        {
            workspaceSessionID = sessionID;
            workspacePath = Encoding.UTF8.GetString(Convert.FromBase64String(sessionID));
            try
            {
                workspace = TCAPIService.GetTCAPI().OpenWorkspace(workspacePath, userName, password);
                if (!workspace.IsSingleUser)
                {                    
                    workspace.UpdateAll();
                    workspace.GetProject().CheckoutTree();               
                }
            }
            catch (Exception ex)
            {
                Dispose();
                throw ex;
            }
        }

        public List<TCObject> SearchForExecutionList(List<KeyValue> executionFilter)
        {
            List<string> searchParams = executionFilter.Select(p => $"{p.key}==\"{p.value}\"").ToList();
            string searchFilter = string.Join(" AND ", searchParams);         
            List<TCObject> executionLists = workspace.GetProject().Search($"=>SUBPARTS:ExecutionList[{searchFilter}]");
            return executionLists;
            
        }

        public string GetAutomationObjectFromExecutionList(String workingDirectory, string executionListId)
        {   
            ExecutionList executionList = workspace.GetTCObject(executionListId) as ExecutionList;
            //List<Tricentis.TCAPIObjects.Objects.TCObject> executionLists = session.GetWorkspace().GetProject().Search($"=>SUBPARTS:ExecutionList[(UniqueId==\"{executionListId}\")]");
            //var executionList = (ExecutionList)executionLists[0];
            string AOFileName = Path.Combine(workingDirectory, executionList.DisplayedName + ".tas");            
            //executionList.WriteAutomationObjects(AOFileName, "false", "false");
            executionList.WriteAutomationObjects(AOFileName);
            return AOFileName;            
        }

        public void Dispose()
        {
            if (TCAPIService.GetTCAPI().IsWorkspaceOpen)
            {
                if (!workspace.IsSingleUser)
                    workspace.CheckInAll("Auto Push");
                TCAPIService.GetTCAPI().CloseWorkspace();
            }
        }

        internal TCWorkspace GetWorkspace()
        {
            return workspace;
        }

        internal void Save()
        {              
            workspace.Save();            
        }
    }
}
