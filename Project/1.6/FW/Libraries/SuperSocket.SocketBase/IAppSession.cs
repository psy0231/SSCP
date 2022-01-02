using SuperSocket.SocketBase.Protocol;

namespace SuperSocket.SocketBase
{
    public interface IAppSession : ISessionBase
    {
        // IAppServer AppServer
        // {
        //     get;
        // }
        //
        // ISocketSession SocketSession
        // {
        //     get;
        // }
    }

    public interface IAppSession<TAppSession, TRequestInfo> : IAppSession
        where TRequestInfo : IRequestInfo
        where TAppSession : IAppSession, IAppSession<TAppSession, TRequestInfo>, new()
    {
        void Initialize(IAppServer<TAppSession, TRequestInfo> server, ISocketSession socketSession);
    }
}