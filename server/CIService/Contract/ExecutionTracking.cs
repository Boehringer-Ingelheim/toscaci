using CIService.Enum;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace CIService.Contract
{
    public class ExecutionTracking
    {
        internal string artifactPath;

        public string id { get; set; }

        public string executionDirectory { get; set; }

        public string reportPath { get; set; }

        public string workspaceDirectory { get; set; }
        public string aOFilePath { get; internal set; }
        public string aOResultFilePath { get; internal set; }

        public string artifactsDirectory { get; set; }

        public ExecutionRequest request { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public ExecutionStatus status { get; set; }
        public Exception error { get; set; }
   

        public ExecutionTracking(string id, ExecutionRequest request)
        {
            this.id = id;
            this.request = request;
        }
    }
}
