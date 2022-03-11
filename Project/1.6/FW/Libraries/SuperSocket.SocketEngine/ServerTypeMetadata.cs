using System;
using SuperSocket.SocketBase.Metadata;

namespace SuperSocket.SocketEngine
{
    [Serializable]
    public class ServerTypeMetadata
    {
        public StatusInfoAttribute[] StatusInfoMegadata { get; set; }
        
        public bool IsServerManager { get; set; }
    }
}