using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tricentis.TCCore.Base;
using Tricentis.TCCore.Persistency.AddInManager;
using Tricentis.TCCore.Persistency.Repository;

namespace CIAddin
{
    public class ToscaCommanderCIAddin : TCAddIn
    {
        public override string UniqueName => "CIAddin";

        public override string DisplayedName => "Continuous Integration Addin";

        private static ToscaCommanderCIAddin instance = null;
        public static ToscaCommanderCIAddin Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ToscaCommanderCIAddin();
                }
                return instance;
            }
        }

        public override void InitializeAfterOpenWorkspace()
        {            
            if (Workspace.ActiveWorkspace != null)
            {
                Workspace.ActiveWorkspace.MyWorkspaceLogger.AddLog(this.ToString(), LogLevel.Info);
            }
        }


    }
}
