namespace SuperSocket.SocketBase.Command
{
    /// <summary>
    /// Command update action enum
    /// </summary>
    public enum CommandUpdateAction
    {
        /// <summary>
        /// Add command
        /// </summary>
        Add,
        
        /// <summary>
        /// Remove command
        /// </summary>
        Remove,
        
        /// <summary>
        /// Update command
        /// </summary>
        Update
    }
    public class CommandUpdateInfo<T>
    {
        public CommandUpdateAction UpdateAction
        {
            get;
            set;
        }

        public T Command
        {
            get;
            set;
        }
    }
}