using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CIService.Enum
{
    class EnumHelper
    {
        public static DBType getDBTypeFromString(string dbtypeString)
        {
            switch (dbtypeString)
            {
                case "local":
                    return DBType.LOCAL;                    
                case "mssql":
                    return DBType.MSSQL_SERVER;
            }
            return DBType.LOCAL;
        }

        public static WorkspaceCreationType GetWorkspaceCreationTypeFromString(string workspaceCreationTypeString)
        {
            switch (workspaceCreationTypeString)
            {
                case "empty":
                    return WorkspaceCreationType.EMPTY;
                case "fromDefinition":
                    return WorkspaceCreationType.FROM_DEFINITION;
                case "fromConnectionString":
                    return WorkspaceCreationType.FROM_CONNECTION_STRING;
            }
            return WorkspaceCreationType.EMPTY;
        }
    }
}
