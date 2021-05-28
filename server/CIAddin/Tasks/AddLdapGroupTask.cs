using System.Collections.Generic;
using System.Linq;
using Tricentis.TCCore.Base.Ownership.LDAP;
using Tricentis.TCCore.Base.Tasks.LDAPTasks;
using Tricentis.TCCore.BusinessObjects;
using Tricentis.TCCore.Persistency;
using Tricentis.TCCore.Persistency.Tasks;

namespace CIAddin.Tasks
{
    [TCTask(ApplicableType = typeof(TCProject))]
    [TCTaskParam("Continue", TCTaskParamType.MsgBoxResult_OkCancel)]
    [TCTaskParam("Group", TCTaskParamType.StringValue)]
    public class AddLdapGroupTask : ThreadTask
    {
        public override string Name => "AddLdapGroupTask";
        
        public override bool RequiresChangeRights => false;
        protected override bool IsAPIOnlyTask => true;

        protected override bool SupressForGUIAccess => true;
        public override bool AllowedInMode(PersistableObject obj, TCApplicationMode applMode)
        {
            
            return true;
        }
        public static bool IsPossibleFor(PersistableObject persistableObject)
        {
            return persistableObject is TCProject;
        }
        protected override void RunInMainThread()
        {
            string groupName = TaskContext.GetStringValue(new TaskParam("Group"), "", false);


            var task = new SynchronizeLDAPObjectsTask();
            task.Execute(Object, new SubTaskContext(TaskContext, groupName));            
        }

        protected override void RunInObserverThread()
        {
        }

        internal class SubTaskContext : TaskContext
        {
            private readonly ITaskContext innerTaskContext;
            private readonly string groupName;

            public SubTaskContext(ITaskContext context, string groupName)
            {
                this.innerTaskContext = context;
                this.groupName = groupName;
            }

            public override MsgBoxResult AddLDAPGroupsForSynchronization(TaskParam taskParam, List<ILDAPObject> objectsToSync)
            {
                var ldapGroup = LDAPSearcher.SearchFor(groupName, LDAPSearchType.Group).FirstOrDefault();
                objectsToSync.Add(ldapGroup);
                //TODO HOW I INJECT THE ROLE? ADMIN / USER ???
                return MsgBoxResult.OK;
            }
        }

    }
}
