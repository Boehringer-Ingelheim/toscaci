using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CIService.Contract
{
    public class ExecutionList
    {
        [JsonProperty("name")]
        public String name { get; set; }
        [JsonProperty("entries")]
        public List<String> entries { get; set; }

    }
}