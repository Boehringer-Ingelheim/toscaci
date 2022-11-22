using CIService.Contract;
using CIService.Enum;
using CIService.Service;
using CIService.Tosca;
using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(ExecutionHelper));
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
            log.DebugFormat("Test Execution Thread started", executionTracking.id);
            RunExecution(executionTracking, executionListIDs);
        }

        public static void RunExecution(ExecutionTracking executionTracking, List<String> executionListIDs)
        {
            try
            {
                var workspacePath = Encoding.UTF8.GetString(Convert.FromBase64String(executionTracking.request.sessionID));
                var workingDir = Path.GetDirectoryName(Path.GetDirectoryName(workspacePath));
                executionTracking.executionDirectory = Path.Combine(workingDir, "execution_" + executionTracking.id);
                log.DebugFormat("[Execution] Execution Directory {0}", executionTracking.executionDirectory);
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
                try
                { using (WorkspaceSession session = new WorkspaceSession(executionTracking.request))
                    {
                        ExecutionList executionList = session.GetWorkspace().GetTCObject(executionListId) as ExecutionList;
                        testSuiteExecution.executionPath = Path.Combine(executionTracking.executionDirectory, executionList.UniqueId);
                        testSuiteExecution.executionListName = executionList.DisplayedName;
                        testSuiteExecution.executionListNameWithoutSpaces = executionList.DisplayedName.Replace(" ","");
                        testSuiteExecution.aOFilePath = Path.Combine(testSuiteExecution.executionPath, testSuiteExecution.executionListNameWithoutSpaces + ".tas");
                        Directory.CreateDirectory(testSuiteExecution.executionPath);
                        testSuiteExecution.aOResultFilePath = Path.Combine(testSuiteExecution.executionPath, "result_" + Path.GetFileName(testSuiteExecution.aOFilePath));
                        log.Info("[Execution] Setting Test Config Parameters");
                        OverrideTcpsWithTestParameters(testSuiteExecution.aOFilePath,executionList, executionTracking.artifactPath, executionTracking.request.TestParameters);
                        session.Save();
                        //executionList.WriteAutomationObjects(testSuiteExecution.executionPath, "false", "false");
                        log.InfoFormat("[Execution] Generatting Automation Object file {0}", testSuiteExecution.aOFilePath);
                        executionList.WriteAutomationObjects(testSuiteExecution.aOFilePath);
                    }

                    // if (executionTracking.request.UnattendedMode)
                    // {
                    //     log.Info("[Execution] Switching to console mode");
                    //     String batPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "unattended.bat");
                    //     Process unattendedProcess = ExecuteProcess(batPath, executionTracking.artifactPath, []);
                    //     unattendedProcess.WaitForExit();
                    // }

                    if (executionTracking.request.VideoRecord)
                    {
                        log.Info("[Execution] Starting Video Record");
                        String ffmpegPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "ffmpeg.exe");
                        String ffmpegArgs = ConfigurationManager.AppSettings["ffmpegArgs"];
                        testSuiteExecution.videoRecordProcess = ExecuteProcess(ffmpegPath, executionTracking.artifactPath, String.Format("{0} {1}", ffmpegArgs, "video_evidence.mp4"));
                    }

                    // for tosca 14.0 -> SlimAgent, for older versions, use "Slim"
                    testSuiteExecution.tboxProcess = ExecuteProcess(Path.Combine(TBOX_HOME_DIRECTORY, TBOX_AGENT_EXE), TBOX_HOME_DIRECTORY, String.Format(" 12342 SlimAgent {0}", testSuiteExecution.aOFilePath));
                    ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Executing);
                    log.InfoFormat("[Execution] Waitting for Test Run to finish");
                    testSuiteExecution.tboxProcess.WaitForExit();
                    if (executionTracking.request.VideoRecord)
                    {
                        log.InfoFormat("[Execution] Stopping video record");
                        testSuiteExecution.videoRecordProcess.StandardInput.Write("q");
                        testSuiteExecution.videoRecordProcess.WaitForExit();
                    }
                    if (executionTracking.cancel)
                    {
                        ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Canceled);
                        return;
                    }
                    if (testSuiteExecution.tboxProcess.ExitCode != 0)
                    {
                        throw new ApplicationException("TBOX Failed with exit code:" + testSuiteExecution.tboxProcess.ExitCode);
                    }
                    //Copy xunit file  generated with tbox to delivery folder, as the name is based on executionList display name, we need to randomize name to avoid file overwritting
                    String xunitFileExecutionPath = Path.Combine(testSuiteExecution.executionPath, "junit_result_" + testSuiteExecution.executionListNameWithoutSpaces + ".xml");
                    String xunitFileResultPath = Path.Combine(executionTracking.xunitPath, "junit_result_" + testSuiteExecution.executionListNameWithoutSpaces + new Random().Next() + ".xml");
                    
                    if (File.Exists(xunitFileExecutionPath))
                        File.Copy(xunitFileExecutionPath, xunitFileResultPath);
                    else
                        log.WarnFormat("File execution path doesn't exist. Path: {0}", xunitFileExecutionPath);

                    using (WorkspaceSession session = new WorkspaceSession(executionTracking.request))
                    {
                        ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.ImportingResults);
                        ImportResults(session, testSuiteExecution.aOResultFilePath);
                        session.Save();

                        ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.GeneratingReports);
                        if (executionTracking.cancel)
                        {
                            ExecutionTrackerService.SetExecutionTrackingState(executionTracking.id, ExecutionStatus.Canceled);
                            return;
                        }

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
                    log.Error("[Execution] Error on execution", ex);
                    ExecutionTrackerService.FailExecutionTrackingStatus(executionTracking.id, ex);
                }
                finally
                {
                    testSuiteExecution.Cancel();
                }
            }
        }

        private static void PrintReports(WorkspaceSession session, string executionListId, string reportName, String reportPath)
        {
            log.InfoFormat("[Execution] Printing Report {0}", reportName);
            try
            {
                var executionList = session.GetWorkspace().GetTCObject(executionListId) as ExecutionList;
                executionList?.PrintReport(reportName,
                    Path.Combine(reportPath, executionList.DisplayedName + "_" + reportName + ".pdf"));
            }
            catch (Exception e)
            {
                log.Error(e.Message);
                throw;
            }
        }

        public static void OverrideTcpsWithTestParameters(string AOFilePath, ExecutionList executionList,
            String artifactPath, List<KeyValue> testParameters)
        {
            if (testParameters != null)
            {
                if (!testParameters.Select(p => p.key).Contains(ARTIFACTS_PATH_NAME))
                {
                    KeyValue artifactKV = new KeyValue();
                    artifactKV.key = ARTIFACTS_PATH_NAME;
                    artifactKV.value = artifactPath;
                    testParameters.Add(artifactKV);
                }

                foreach (KeyValue kv in testParameters)
                {
                    log.DebugFormat("[Execution] Setting Test Config Parameter {0}:{1}", kv.key, kv.value);
                    TestConfigurationHelper.SetTestConfigurationParameterValue(executionList, kv.key, kv.value);
                }
            }
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
            log.InfoFormat("[Execution] Importing Test Results");
            //TCTaskParams tcTaskParams = new TCTaskParams();
            //tcTaskParams.AddParam("AOResultFilePath", AOResultFile);
            //session.GetWorkspace().GetProject().ExecuteTask("CIAddin.Tasks.ImportAutomationObjectResultsToExecutionLogTask", tcTaskParams);
            session.GetWorkspace().GetProject().ImportAutomationObjectResultsToExecutionLog(AOResultFile); // Since Tosca 14.2+
            //session.GetWorkspace().GetProject().ImportAOResults(AOResultFile);
        }
        private static Process ExecuteProcess(String fileName, String workingDir, String arguments)
        {
            log.DebugFormat("[Execution] Execute Process {0} {1} workDir: {2}", fileName, arguments, workingDir);
            Process AgentProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments, // for tosca 14.0 -> SlimAgent, for older versions, use "Slim"
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = false,
                    WorkingDirectory = workingDir
                }
            };


            AgentProcess.OutputDataReceived += new DataReceivedEventHandler((s, e) => { Console.WriteLine(e.Data); });
            AgentProcess.ErrorDataReceived += new DataReceivedEventHandler((s, e) => { Console.WriteLine(e.Data); });
            AgentProcess.Start();
            AgentProcess.BeginOutputReadLine();
            AgentProcess.BeginErrorReadLine();
            log.DebugFormat("[Execution] Process {0} started, PID:{1}", fileName, AgentProcess.Id);
            return AgentProcess;
        }
    }
}