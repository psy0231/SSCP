using SuperSocket.SocketBase.Config;

namespace SuperSocket.SocketBase
{
    /// <summary>
    /// An item can be started and stopped
    /// </summary>
    public interface IWorkItemBase : IStatusInfoSource, ISystemEndPoint
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        string Name
        {
            get;
        }

        /// <summary>
        /// Gets the server's config.
        /// </summary>
        /// <value>
        /// The server's config.
        /// </value>
        IServerConfig Config
        {
            get;
        }

        /// <summary>
        /// Starts this server instance.
        /// </summary>
        /// <returns>return true if start successfull, else false</returns>
        bool Start();

        /// <summary>
        /// Reports the potential configuration change.
        /// </summary>
        /// <param name="config">The server config which may be changed.</param>
        void ReportPotentialConfigChange(IServerConfig config);

        /// <summary>
        /// Stops this server instance.
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Gets the total session count.
        /// </summary>
        int SessionCount
        {
            get;
        }
    }
    public interface IWorkltem : IWorkItemBase, IStatusInfoSource
    {
        bool Setup(IBootstrap bootstrap, IServerConfig config, ProviderFactortInfo[] factories);

        ServerState State
        {
            get;
        }
    }
}