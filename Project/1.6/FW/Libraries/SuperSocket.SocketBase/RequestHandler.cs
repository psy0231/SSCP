using SuperSocket.SocketBase.Protocol;

namespace SuperSocket.SocketBase
{
    public delegate void RequestHandler<TAppSession, TRequestInfo>(TAppSession session, TRequestInfo requestInfo)
        where TAppSession : IAppSession, IAppSession<TAppSession, TRequestInfo>, new()
        where TRequestInfo : IRequestInfo;

}