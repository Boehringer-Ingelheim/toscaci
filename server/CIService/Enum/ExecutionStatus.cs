using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CIService.Enum
{
    public enum ExecutionStatus
    {
        Pending = 0,
        Preparing = 1,
        Executing = 2,
        ImportingResults = 3,
        WaitingToImportResults = 4,
        GeneratingReports = 5,        
        Completed = 6,
        Failed = 7,
        Canceled = 8           
    }
}
