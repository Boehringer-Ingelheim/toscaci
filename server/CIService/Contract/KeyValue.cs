using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIService.Contract
{
    public class KeyValue
    {
        [JsonProperty("key")]
        public string key { get; set; }
        [JsonProperty("value")]
        public string value { get; set; }
    }
}
