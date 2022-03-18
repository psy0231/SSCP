using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using SuperSocket.SocketBase;

namespace SuperSocket.SocketEngine
{
    static class SocketState
    {
        public const int Normal = 0;//0000 0000
        public const int InClosing = 16;//0001 0000 >= 16
        public const int Closed = 16777216;//256 * 256 * 256; 0x01 0x00 0x00 0x00
        public const int InSending = 1;//0000 0001 > 1
        public const int InReceiving = 2; //0000 0010 > 2
        public const int InSendingReceivingMask = -4;// ~(InSending | InReceiving); 0x0f 0xff 0xff 0xff
    }
    
    public class SocketSession : ISocketSession
    {
        public IAppSession AppSession { get; private set; }

        protected readonly object SyncRoot = new object();
        
        //0x00 0x00 0x00 0x00
        //1st byte: Closed(Y/N)
        public string SessionID { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public void Initialize(IAppSession appSession)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Close(CloseReason reason)
        {
            throw new NotImplementedException();
        }

        public bool TrySend(IList<ArraySegment<byte>> segments)
        {
            throw new NotImplementedException();
        }

        public bool TrySend(ArraySegment<byte> segment)
        {
            throw new NotImplementedException();
        }

        public void ApplySecureProtocol()
        {
            throw new NotImplementedException();
        }

        public Socket Client { get; }
        public IPEndPoint LocalEndPoint { get; }
        public SslProtocols SecureProtocol { get; set; }
        public Action<ISocketSession, CloseReason> Closed { get; set; }
        public int OrigReceiveOffset { get; }
    }
}