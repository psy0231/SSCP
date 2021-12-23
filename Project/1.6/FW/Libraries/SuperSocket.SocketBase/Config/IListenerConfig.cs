namespace SuperSocket.SocketBase.Config
{
    public interface IListenerConfig
    {
        string Ip
        {
            get;
        }

        int Port
        {
            get;
        }

        int Backlog
        {
            get;
        }

        string Security
        {
            get;
        }
    }
}