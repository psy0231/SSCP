using SuperSocket.SocketBase.Protocol;

namespace SuperSocket.SocketBase
{
    public class AppServer
    {
        
    }

    public abstract class AppServer<TAppSession, TRequestInfo> : AppServerBase<TAppSession, TRequestInfo>
        where TRequestInfo : class, IRequestInfo
        where TAppSession : AppSession<TAppSession, TRequestInfo>, IAppSession, new()
    {
        
    }
}