using CIService.Contract;
using CIService.Enum;
using CIService.Service;
using CIService.Tosca;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Tricentis.Automation.Contract;
using Tricentis.Common.Security.O_CF_NR;
using Tricentis.TCAPIObjects.DataObjects;
using Tricentis.TCAPIObjects.Objects;
using ExecutionList = Tricentis.TCAPIObjects.Objects.ExecutionList;

namespace CIService.Helper
{
    public static class ExecutionHelper
    {
        private const string ARTIFACTS_PATH_NAME = "ARTIFACTS_PATH";
        private const string ARTIFACTS_DIR_NAME = "artifacts";
        private const string XUNITS_DIR_NAME = "xunits";
        private const string REPORTS_DIR_NAME = "Reports";
        private const string TBOX_HOME = "%TBOX_HOME%";
        private static string TBOX_AGENT_EXE = "Tricentis.Automation.Agent.exe";

        private static string TBOX_HOME_DIRECTORY = Environment.ExpandEnvironmentVariables(TBOX_HOME);

        private static void CreateDirectoryIfNotExists(string path)
        {
            bool exists = System.IO.Directory.Exists(path);

            if (!exists)
                System.IO.Directory.CreateDirectory(path);
        }

        public static void TriggerExecution(ExecutionTracking executionTracking, List<String> executionListIDs)
        {
            executionTracking.thread = new Thread(() => { RunExecution(executionTracking, executionListIDs); });
            executionTracking.thread.Start();            
        }

        public static void RunExecution(ExecutionTracking executionTracking, List<String> executionListIDs)
        {
            Process AgentProcess = null;
            try
            {
                var workspacePath = Encoding.UTF8.GetString(Convert.FromBase64String(executionTracking.request.sessionID));
                var workingDir = Path.GetDirectoryName(Path.GetDirectoryName(workspacePath));
                executionTracking.executionDirectory = Path.Combine(workingDir, "execution_" + executionTracking.id);
                CreateDirectoryIfNotExists(executionTracking.executionDirectory);
            }catch(Exception ex)
            {
                ExecutionTrackerService.FailExecutionTrackingStatus(executionTracking.id, ex);
            }

            //Initialize File Structure
            executionTracking.artifactPath = Path.Combine(executionTracking.executionDirectory, ARTIFACTS_DIR_NAME);
            CreateDirectoryIfNotExists(executionTracking.artifactPath);
            executionTracking.xunitPath = Path.Combine(executionTracking.executionDirectory, XUNITS_DIR_NAME);
            CreateDirectoryIfNotExists(executionTracking.xunitPath);
            executionTracking.reportPath = Path.Combine(executionTracking.executionDirectory, REPORTS_DIR_NAME);
            CreateDirectoryIfNotExists(executionTracking.reportPath);

            foreach (String executionListId in executionListIDs)
            {
                if (executionTracking.cancel)
                {
                    ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Canceled);
                    return;
                }
                TestSuiteExecution testSuiteExecution = new TestSuiteExecution();
                executionTracking.AddExecution(testSuiteExecution);
                try {
                    using (WorkspaceSession session = new WorkspaceSession(executionTracking.request))
                    {
                        ExecutionList executionList = session.GetWorkspace().GetTCObject(executionListId) as ExecutionList;
                        testSuiteExecution.executionPath = Path.Combine(executionTracking.executionDirectory, executionList.UniqueId);
                        testSuiteExecution.executionListName = executionList.DisplayedName;
                        testSuiteExecution.aOFilePath = Path.Combine(testSuiteExecution.executionPath, testSuiteExecution.executionListName + ".tas");
                        //executionList.WriteAutomationObjects(testSuiteExecution.executionPath, "false", "false");
                        Directory.CreateDirectory(testSuiteExecution.executionPath);
                        executionList.WriteAutomationObjects(testSuiteExecution.aOFilePath);                        
                        testSuiteExecution.aOResultFilePath = Path.Combine(testSuiteExecution.executionPath, "result_" + Path.GetFileName(testSuiteExecution.aOFilePath));
                    }                    
                
                    OverrideTcpsWithTestParameters(testSuiteExecution.aOFilePath, executionTracking.artifactPath, executionTracking.request.TestParameters);
                    AgentProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Combine(TBOX_HOME_DIRECTORY, TBOX_AGENT_EXE),
                            Arguments = " 12342 Slim " + testSuiteExecution.aOFilePath, // for tosca 14.0 -> SlimAgent, for older versions, use "Slim"
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                            CreateNoWindow = false,
                            WorkingDirectory = TBOX_HOME_DIRECTORY,                                                       
                        }
                    };
                    


                    AgentProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
                    {
                        Console.WriteLine(e.Data);
                    });
                    AgentProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
                    {
                        Console.WriteLine(e.Data);
                    });
                    AgentProcess.Start();
                    AgentProcess.BeginOutputReadLine();
                    AgentProcess.BeginErrorReadLine();

                    testSuiteExecution.tboxProcess = AgentProcess;
                    ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Executing);                    
                    AgentProcess.WaitForExit();
                    if (executionTracking.cancel)
                    {
                        ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Canceled);
                        return;
                    }
                    if (AgentProcess.ExitCode != 0)
                    {
                        throw new ApplicationException("TBOX Failed with exit code:" + AgentProcess.ExitCode);
                    }
                    //Copy xunit file  generated with tbox to delivery folder, as the name is based on executionList display name, we need to randomize name to avoid file overwritting
                    String xunitFileExecutionPath = Path.Combine(testSuiteExecution.executionPath, "junit_result_" + testSuiteExecution.executionListName + ".xml");
                    String xunitFileResultPath = Path.Combine(executionTracking.xunitPath, "junit_result_" + testSuiteExecution.executionListName + new Random().Next()+ ".xml");
                    File.Copy(xunitFileExecutionPath, xunitFileResultPath);
                   
                    using (WorkspaceSession session = new WorkspaceSession(executionTracking.request))
                    {
                        ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.ImportingResults);
                        ImportResults(session, testSuiteExecution.aOResultFilePath);
                        session.Save();
                    }

                    ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.GeneratingReports);
                    if (executionTracking.cancel)
                    {
                        ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Canceled);
                        return;
                    }
                    using (WorkspaceSession session = new WorkspaceSession(executionTracking.request))
                    {
                        foreach (String reportName in executionTracking.request.Reports)
                        {                            
                            PrintReports(session, executionListId, reportName, executionTracking.reportPath);
                        }                        
                    }
                    if (executionTracking.cancel)
                    {
                        ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Canceled);
                        return;
                    }
                    ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Completed);
                }
                catch (Exception ex)
                {
                    ExecutionTrackerService.FailExecutionTrackingStatus(executionTracking.id, ex);
                }
                finally
                {
                    testSuiteExecution.Cancel();                    
                }
            }   
        }

        private static void PrintReports(WorkspaceSession session, string executionListId,string reportName,String reportPath)
        {
            var executionList = session.GetWorkspace().GetTCObject(executionListId) as ExecutionList;            
            executionList.PrintReport(reportName, Path.Combine(reportPath, executionList.DisplayedName + "_" + reportName + ".pdf"));                                    
        }

        public static void OverrideTcpsWithTestParameters(string AOFilePath, String artifactPath, List<KeyValue> testParameters)
        {            
            List<ExecutionTask> executionTasks = AutomationObjectsSerializer.FromFile<List<ExecutionTask>>(AOFilePath, CommonCrypto.Instance.CreateDecryptStream);
           
            List<KeyValuePair<string, string>> testParams = testParameters.Select(t => new KeyValuePair<string, string>(t.key, t.value)).ToList();
            if (!testParams.Select(p=>p.Key).Contains(ARTIFACTS_PATH_NAME))
            {                
                testParams.Add(new KeyValuePair<string, string>(ARTIFACTS_PATH_NAME, artifactPath));
            }

            List<string> testParamKeys = testParams.Select(t => t.Key).ToList();            

            List<KeyValuePair<string, string>> existingTcps = executionTasks.FirstOrDefault().TestConfiguration.ToList();
            List<KeyValuePair<string, string>> newTcps = new List<KeyValuePair<string, string>>();

            foreach (KeyValuePair<string, string> existingTcp in existingTcps)
            {
                if (testParamKeys.Contains(existingTcp.Key)) // if the TCP is defined in the JSON env data, override the TCP with the test param
                {
                    string envDataValue = testParams.FirstOrDefault(p => p.Key == existingTcp.Key).Value;
                    KeyValuePair<string, string> overriddenTcp = new KeyValuePair<string, string>(existingTcp.Key, envDataValue);
                    newTcps.Add(overriddenTcp);
                }
                else
                {
                    newTcps.Add(existingTcp); // if not overridden, just copy over the existing TCP into the new TCP list
                }
            }
            List<string> tcpKeys = existingTcps.Select(p => p.Key).ToList();
            List<KeyValuePair<string, string>> extraTestParams = testParams.Where(p => !tcpKeys.Contains(p.Key)).ToList();
            newTcps.AddRange(extraTestParams);

            executionTasks.FirstOrDefault().TestConfiguration = newTcps;
            File.Delete(AOFilePath);
            AutomationObjectsSerializer.ToFile(AOFilePath, executionTasks, CommonCrypto.Instance.CreateEncryptStream);
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }
  
        public static void Empty(this DirectoryInfo directory)
        {
            foreach (System.IO.FileInfo file in directory.GetFiles()) file.Delete();
            foreach (DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
        }

        private static void ImportResults(WorkspaceSession session, string AOResultFile)
        {
                TCTaskParams tcTaskParams = new TCTaskParams();
                tcTaskParams.AddParam("AOResultFilePath", AOResultFile);
                session.GetWorkspace().GetProject().ExecuteTask("CIAddin.Tasks.ImportAutomationObjectResultsToExecutionLogTask", tcTaskParams);                          
                //session.GetWorkspace().GetProject().ImportAOResults(AOResultFile);
        }
    }
}
