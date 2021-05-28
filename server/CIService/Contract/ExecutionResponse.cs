using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIService.Contract
{
    class ExecutionResponse
    {
        [JsonProperty("error")]
        public string error { get; set; }
        [JsonProperty("executionID")]
        public string executionID { get; set; }

        [JsonProperty("status")]
        public string status { get; set; }

        [JsonProperty("files")]
        public List<FileInfo> files { get; set; }


    }
}
