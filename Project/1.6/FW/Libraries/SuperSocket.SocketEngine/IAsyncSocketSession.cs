using System.Net.Sockets;
using SuperSocket.SocketBase;

namespace SuperSocket.SocketEngine
{
    public interface IAsyncSocketSessionBase : ILoggerProvider
    {
        SocketAsyncEventArgs SocketAsyncProxy { get; }
        
        Socket Client { get; }
    }

    public interface IAsyncSocketSession : IAsyncSocketSessionBase
    {
        void ProcessReceive(SocketAsyncEventArgs e);
    }
}