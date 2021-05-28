using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIService.Contract
{
    class CreateWorkspaceResponse
    {
        public string error { get; set; }
        public ProjectInformation project { get; set; }
    }
}
