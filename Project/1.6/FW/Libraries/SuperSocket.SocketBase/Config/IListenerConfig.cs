namespace SuperSocket.SocketBase.Config
{
    public interface IListenerConfig
    {
        string ip
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