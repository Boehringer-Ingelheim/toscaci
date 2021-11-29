using System.Collections.Generic;
using System.Linq;

using AutoMapper;
using Tricentis.TCCore.Base;
using Tricentis.TCCore.Persistency;
using ExecutionResult = Tricentis.Automation.Contract.Results.ExecutionResult;
using ExecutionTestCaseLog = Tricentis.TCCore.BusinessObjects.ExecutionLists.ExecutionLogs.ExecutionTestCaseLog;
using ExecutionEntry = Tricentis.TCCore.BusinessObjects.ExecutionLists.ExecutionEntry;
using ExecutionLog = Tricentis.TCCore.BusinessObjects.ExecutionLists.ExecutionLogs.ExecutionLog;
using Tricentis.TCAddIns.XDefinitions.ResultMappers;
using Tricentis.TCAddIns.XDefinitions.ResultMappers.Factory;

namespace CIAddin
{
    internal sealed class ExecutionResultMapper2
    {
        private static ExecutionResultMapper2 instance;

        public static ExecutionResultMapper2 Instance => instance ?? (instance = new ExecutionResultMapper2());

        /// <summary>
        /// Maps an Automation.Execution.Results.ExecutionResult to an ExecutionLog
        /// </summary>
        /// <param name="executionResult"></param>
        /// <param name="entry"></param>
        /// <param name="isScratchBookExecution"></param>
        /// <returns></returns>
        public ExecutionTestCaseLog MapExecutionResult(ExecutionEntry entry, ExecutionResult executionResult, bool isScratchBookExecution)
        {
            ExecutionTestCaseLog result = CreateExecutionTestCaseLog(entry, executionResult, isScratchBookExecution);
            if (result == null)
            {
                return null;
            }
            result.UserName = Workspace.ActiveWorkspace.User.Name;
            ExecutionLog actualExecutionLog = entry.MyExecutionList.GetOrCreateActualExecutionLog();
            actualExecutionLog.TestCaseLogs.Add(result);
            AddExecutionResultTcpsToExecutionEntry(executionResult, entry);
            return result;
        }

        private static ExecutionTestCaseLog CreateExecutionTestCaseLog(ExecutionEntry entry, ExecutionResult executionResult, bool isScratchBookExecution)
        {
            IResultMapper entryResultMapper = ResultMapperFactory.Instance.GetResultMapper(entry, isInScratchbook: isScratchBookExecution);
            ExecutionTestCaseLog result;
            using (PersistableObject.PauseAspectChangedHandling())
            {
                result = (ExecutionTestCaseLog)entryResultMapper.MapResult(executionResult, null);
            }
            return result;
        }

        private void AddExecutionResultTcpsToExecutionEntry(ExecutionResult executionResult, ExecutionEntry entry)
        {
            foreach (KeyValuePair<string, string> tcp in executionResult.ResultTcps)
            {
                entry.TestConfiguration.SetConfigurationParam(tcp.Key, tcp.Value);
            }
        }
    }
}