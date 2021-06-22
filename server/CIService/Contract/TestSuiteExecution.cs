using CIService.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIService.Contract
{
    public class TestSuiteExecution
    {
        public Int32 tboxPID { get; internal set; }
        public string aOFilePath { get; internal set; }
        public string aOResultFilePath { get; internal set; }

        public ExecutionStatus status { get; set; }
        public Exception error { get; set; }
    }
}
