using System.Collections.Generic;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketBase.Metadata;
using SuperSocket.SocketBase.Provider;

namespace SuperSocket.SocketEngine
{
    public class WorkItemFactoryInfo
    {
        public string ServerType { get; set; }
        
        public bool IsServerManager { get; set; }
        
        public StatusInfoAttribute[] StatusInfoMetadata { get; set; }
        
        public IServerConfig Config { get; set; }
        
        public IEnumerable<ProviderFactoryInfo> ProviderFactories { get; set; }
        
        public ProviderFactoryInfo LogFactory { get; set; }
        
        public ProviderFactoryInfo SocketServerFactory { get; set; }
        
    }
}