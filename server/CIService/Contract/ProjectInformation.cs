using CIService.Enum;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CIService.Contract
{
    class ProjectInformation
    {
        public ProjectInformation()
        {

        }
        public ProjectInformation(ProjectInformation projectInformation)
        {
            name = projectInformation.name;
            dbType = projectInformation.dbType;
            connectionString = projectInformation.connectionString;
            ownerRoleName = projectInformation.ownerRoleName;
            viewerRoleName = projectInformation.viewerRoleName;            
        }

        public string name { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public DBType dbType { get; set; }

        public string connectionString { get; set; }

        public string dbName { get; set; }

        public string ownerRoleName { get; set; }
        public string viewerRoleName { get; set; }

        public string workspacePath { get; set; }

        public string sessionID { get; set; }
    }
}
