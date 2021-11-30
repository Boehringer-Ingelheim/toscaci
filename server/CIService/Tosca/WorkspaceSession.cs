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
            return SearchFor($"=>SUBPARTS:ExecutionList[{searchFilter}]");            
        }

        public List<TCObject> SearchFor(String tql)
        {
            List<TCObject> tCObjects = workspace.GetProject().Search(tql);
            return tCObjects;
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
