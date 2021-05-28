using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.SelfHost;
using System.Web.Http.SelfHost.Channels;

namespace CIService
{
    public class HttpSelfHostConfiguration : System.Web.Http.SelfHost.HttpSelfHostConfiguration
    {
        public HttpSelfHostConfiguration(string baseAddress) : base(baseAddress) { }
        public HttpSelfHostConfiguration(Uri baseAddress) : base(baseAddress) { }
        protected override BindingParameterCollection OnConfigureBinding(HttpBinding httpBinding)
        {
            httpBinding.Security.Mode = HttpBindingSecurityMode.Transport;
            return base.OnConfigureBinding(httpBinding);
        }
    }
}
