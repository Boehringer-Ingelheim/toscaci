using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tricentis.Common.Security.O_CF_NR;
using Tricentis.Data.Model;
using Tricentis.TCCore.Base;
using Tricentis.TCCore.Base.Attachments.FileHandling;
using Tricentis.TCCore.BusinessObjects;
using Tricentis.TCCore.BusinessObjects.ExecutionLists;
using Tricentis.TCCore.BusinessObjects.ExecutionLists.ExecutionLogs;
using Tricentis.TCCore.Persistency;
using Tricentis.TCCore.Persistency.Attachments;
using Tricentis.TCCore.Persistency.Commands;
using Tricentis.TCCore.Persistency.Tasks;

namespace CIAddin.Tasks
{
    [TCTask(ApplicableType = typeof(TCProject))]
    [TCTaskParam("AOResultFilePath", TCTaskParamType.StringValue)]
    public class ImportAutomationObjectResultsToExecutionLogTask : ThreadTask
    {
        public override string Name => "ImportAutomationObjectResultsToExecutionLogTask";

        public override bool RequiresChangeRights => false;

        protected override bool IsAPIOnlyTask => true;

        protected override bool SupressForGUIAccess => true;

        //protected override bool DoPauseAspectChangedHandling => false;

        // protected override bool IsAPIOnlyTask => false;

        // protected override bool SupressForGUIAccess => false;

        //public override bool AllowedInMode(PersistableObject obj, TCApplicationMode applMode)
        //{
        //    return base.AllowedInMode(obj, applMode);// || applMode == TCApplicationMode.AOS;
        //}
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
            string filePath = TaskContext.GetTaskParameter(new TaskParam("AOResultFilePath"));
            RunTask(filePath);
        }

        public static void RunTask(string filePath)
        {


            List<Tricentis.Automation.Contract.Results.ExecutionResult> results;

            FileAttributes fileAttributes = File.GetAttributes(filePath);
            if (fileAttributes.HasFlag(FileAttributes.Directory))
            {
                results = Directory.EnumerateFiles(filePath)
                                   .SelectMany(file => AutomationObjectsSerializer.FromFile<List<Tricentis.Automation.Contract.Results.ExecutionResult>>(file, CommonCrypto.Instance.CreateDecryptStream)).ToList();
            }
            else
            {
                results = AutomationObjectsSerializer.FromFile<List<Tricentis.Automation.Contract.Results.ExecutionResult>>(filePath, CommonCrypto.Instance.CreateDecryptStream);
            }
            ObjectHandler activeObjectHandler = ObjectHandler.ActiveObjectHandler;
            foreach (Tricentis.Automation.Contract.Results.ExecutionResult result in results)
            {
                ExecutionEntry executionEntry = activeObjectHandler.GetOrLoadObject(Surrogate.Create(result.SurrogateId)) as ExecutionEntry;

                using (CommandHandler.Instance.BeginTransaction(new CommandDescription("Import AO Results to ExecutionLog", "importing")))
                {
                    ExecutionTestCaseLog executionTestCaseLog = ExecutionResultMapper2.Instance.MapExecutionResult(executionEntry, result, false);
                    CompressLogs(new List<ExecutionTestCaseLog> { executionTestCaseLog }, true);
                }
            }
        }

        public static void CompressLogs(List<ExecutionTestCaseLog> mappedLogs, bool compressLogs)
        {
            if (!compressLogs)
            {
                if (!Workspace.FileServiceEnabledOnActiveWorkspace)
                {
                    compressLogs = true;
                }
                if (Tricentis.TCAddIns.XDefinitions.FeatureFlags.Instance.UploadToFileServiceDuringResultMapping && TryUploadSubPartsForAllLogs(mappedLogs))
                {
                    compressLogs = true;
                }
            }

            if (compressLogs)
            {
                CompressLogs(mappedLogs);
            }
        }

        private static bool TryUploadSubPartsForAllLogs(List<ExecutionTestCaseLog> mappedLogs)
        {
            Workspace activeWorkspace = Workspace.ActiveWorkspace;
            if (activeWorkspace == null)
            {
                return false;
            }

            FileUploader fileUploader = new FileUploader(activeWorkspace);
            return mappedLogs.All(log => TryUploadSubParts(log, fileUploader));
        }

        private static bool TryUploadSubParts(ExecutionTestCaseLog executionTestCaseLog, FileUploader fileUploader)
        {
            List<IFileServiceObject> fileContentItems = executionTestCaseLog.GetAllSubParts(false).OfType<IFileServiceObject>().ToList();
            return fileContentItems.All(item => TryUpload(item, fileUploader));
        }

        private static bool TryUpload(IFileServiceObject fileServiceObject, FileUploader fileUploader)
        {
            if (Uploaded(fileServiceObject))
            {
                return true;
            }

            return fileUploader.UploadFileToFileService(fileServiceObject);
        }

        private static bool Uploaded(IFileServiceObject fileServiceObject)
        {
            return !string.IsNullOrEmpty(fileServiceObject.ExternalFileRevision);
        }


        private static void CompressLogs(List<ExecutionTestCaseLog> mappedLogs)
        {
            using (PersistableObject.PauseAspectChangedHandling())
            {
                List<ExecutionTestCaseLog> testCaseLogs = mappedLogs;
                mappedLogs.ForEach(tcl => tcl.GenerateCompressedLogsIfNecessary());
            }
        }


        protected override void RunInObserverThread() { }
    }
}