using CIService.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CIService.Helper
{
    class AuthHelper
    {
        internal static void BindExecutionAuthentication(HttpRequestMessage httpRequest, ExecutionRequest toscaRequest)
        {
            string authoritzation = null;
            try { 
            authoritzation = httpRequest.Headers.GetValues("Authorization").First();
            }catch(Exception)
            {
                return;
            }

            if (authoritzation!=null)
            {
                if (authoritzation.StartsWith("Basic")) { 
                    string encodedUsernamePassword = authoritzation.Substring("Basic ".Length).Trim();
                
                    string usernamePassword = Encoding.Default.GetString(Convert.FromBase64String(encodedUsernamePassword));
                    string[] userPassArray = usernamePassword.Split(':');
                    toscaRequest.WorkspaceUsername = userPassArray[0];
                    toscaRequest.WorkspacePassword = userPassArray[1];
                }
                else
                {
                    throw new Tricentis.TCAPIObjects.Exceptions.TCApiLoginFailedException($"Invalid Authoritzation method {authoritzation.Split(' ')[0]}, expected Basic");
                }
            }
        }
    }
}
