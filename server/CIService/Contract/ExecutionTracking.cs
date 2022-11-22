using CIService.Enum;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CIService.Contract
{
    public class ExecutionTracking
    {
        internal string artifactPath;

        public string id { get; set; }

        public string executionDirectory { get; set; }

        public string reportPath { get; set; }

        public string xunitPath { get; set; }

        public string workspaceID { get; set; }
        public string workspaceDirectory { get; set; }

        internal Boolean cancel { get; set; } = false;
        public List<TestSuiteExecution> executions  { get; set; }

        public string artifactsDirectory { get; set; }

        public ExecutionRequest request { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ExecutionStatus status { get; set; }
        public Exception error { get; set; }

        public ExecutionTracking(string id, ExecutionRequest request)
        {
            this.id = id;
            this.request = request;
            this.workspaceID = request.sessionID;
        }

        internal void AddExecution(TestSuiteExecution testSuiteExecution)
        {
            if(executions == null)
            {
                executions = new List<TestSuiteExecution>();
            }
            executions.Add(testSuiteExecution);
        }
    }
}
