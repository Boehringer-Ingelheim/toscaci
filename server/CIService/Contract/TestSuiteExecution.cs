using CIService.Enum;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIService.Contract
{
    public class TestSuiteExecution
    {
        public string executionListName { get; set; }

        public string executionPath { get; set; }

        public Process tboxProcess { get; set; }
        public Process videoRecordProcess { get; set; }
        public string aOFilePath { get; set; }
        public string aOResultFilePath { get; set; }

        public ExecutionStatus status { get; set; }
        public Exception error { get; set; }

        public void Cancel()
        {
            try
            {
                if (tboxProcess != null)
                {
                    tboxProcess.Kill();
                    tboxProcess.WaitForExit();
                }
            }
            catch
            {
            }
            try
            {
                if (videoRecordProcess != null)
                {
                    videoRecordProcess.Kill();
                    videoRecordProcess.WaitForExit();
                }
            }
            catch
            {
            }
        }
    }
}