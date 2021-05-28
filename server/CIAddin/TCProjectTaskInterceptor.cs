using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tricentis.TCCore.BusinessObjects;
using Tricentis.TCCore.Persistency;
using Tricentis.TCCore.Persistency.AddInManager;
using Task = Tricentis.TCCore.Persistency.Task;
using TaskFactory = Tricentis.TCCore.Persistency.TaskFactory;

namespace CIAddin
{
    public class TCProjectTaskInterceptor : TaskInterceptor
    {

        public TCProjectTaskInterceptor(TCProject obj) { }

        public override void GetTasks(PersistableObject obj, List<Task> tasks)
        {
            TCProject project = obj as TCProject;
            if (project == null) { return; }
            tasks.Add(TaskFactory.Instance.GetTask(typeof(Tasks.ImportFullProjectSubsetTask)));
            tasks.Add(TaskFactory.Instance.GetTask(typeof(Tasks.ImportAutomationObjectResultsToExecutionLogTask)));
            tasks.Add(TaskFactory.Instance.GetTask(typeof(Tasks.AddLdapGroupTask)));
        }
    }
}
