using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CIService.Contract
{
    public class ExecutionRequest
    {

        [JsonProperty("sessionID")]
        public string sessionID { get; set; }
    
        public string WorkspaceUsername { get; set; } = "Admin";      
        public string WorkspacePassword { get; set; } = "";

        [JsonProperty("parameters")]
        public List<KeyValue> TestParameters { get; set; }

        [JsonProperty("selectors")]
        public List<KeyValue> ExecutionFilter { get; set; }
        [JsonProperty("reports")]
        public List<String> Reports { get; set; }

        [JsonProperty("videoRecord")]
        public bool VideoRecord { get; set; }

        [JsonProperty("unattendedMode")]
        public bool UnattendedMode { get; set; }

        [JsonProperty("preserveWorkspaces")]
        public bool PreserveWorkspaces { get; set; }
    }
}
