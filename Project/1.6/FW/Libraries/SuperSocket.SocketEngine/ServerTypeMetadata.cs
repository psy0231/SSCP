using System;
using SuperSocket.SocketBase.Metadata;

namespace SuperSocket.SocketEngine
{
    [Serializable]
    class ServerTypeMetadata
    {
        public StatusInfoAttribute[] StatusInfoMetadata { get; set; }
        
        public bool IsServerManager { get; set; }
    }
}