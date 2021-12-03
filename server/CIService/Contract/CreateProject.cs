using CIService.Enum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIService.Contract
{
    class CreateProject : ProjectInformation
    {
        public string templateBranchName { get; set; }

        public string templateConnectionString { get; set; }
        public string templateConnectionWorkspaceUsername { get; set; }
        public string templateConnectionWorkspacePassword { get; set; }

        public WorkspaceCreationType templateType { get; set; }
        public ArrayList subsetFiles { get; set; }
        public string projectDefinition { get; set; }

    }
}
