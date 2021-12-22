namespace SuperSocket.SocketBase.Command
{
    public interface ICommandLoader
    {
        
    }

    public interface ICommandLoader<TCommand> : ICommandLoader
        where TCommand : ICommand
    {
        
    }
}