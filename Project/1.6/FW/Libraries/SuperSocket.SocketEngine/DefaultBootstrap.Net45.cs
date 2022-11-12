using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperSocket.SocketEngine
{
    public partial class DefaultBootstrap
    {
        partial void SetDefaultCulture(SocketBase.Config.IRootConfig rootConfig)
        {
            if (!string.IsNullOrEmpty(rootConfig.DefaultCulture))
            {
                CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(rootConfig.DefaultCulture);
            }
        }
    }
}
