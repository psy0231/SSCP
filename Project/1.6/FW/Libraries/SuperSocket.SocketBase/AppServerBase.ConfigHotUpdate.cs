namespace SuperSocket.SocketBase
{
    interface IConfigValueChangeNotifier
    {
        bool Notify(string newValue);
    }
}