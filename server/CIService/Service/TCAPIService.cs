using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tricentis.TCAPI;
using Tricentis.TCAPIObjects;

namespace CIService.Service
{
    class TCAPIService
    {
        public static TCAPI GetTCAPI()
        {            
            if (TCAPI.Instance == null) { 
                return TCAPI.CreateInstance(TCAPILicenseMode.Full);
            }
            return TCAPI.Instance;
        }
       
    }
}
