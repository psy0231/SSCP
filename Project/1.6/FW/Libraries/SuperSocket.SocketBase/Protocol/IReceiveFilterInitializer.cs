namespace SuperSocket.SocketBase.Protocol
{
    public interface IReceiveFilterInitializer
    {
        void Initialize(IAppServer appServer, IAppSession session);
    }
}