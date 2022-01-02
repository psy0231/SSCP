using SuperSocket.SocketBase.Config;

namespace SuperSocket.SocketBase
{
    /// <summary>
    /// The bootstrap start result
    /// </summary>
    public enum StartResult
    {
        /// <summary>
        /// No appserver has been set in the bootstrap, so nothing was started
        /// </summary>
        None,
        /// <summary>
        /// All appserver instances were started successfully
        /// </summary>
        Success,
        /// <summary>
        /// Some appserver instances were started successfully, but some of them failed
        /// </summary>
        PartialSuccess,
        /// <summary>
        /// All appserver instances failed to start
        /// </summary>
        Failed
    }
    public interface IBootstrap
    {
        
    }

    public interface IDynamicBootstrap
    {
        /// <summary>
        /// Adds a new server into the bootstrap.
        /// </summary>
        /// <param name="config">The new server's config.</param>
        /// <returns></returns>
        bool Add(IServerConfig config);
        
        /// <summary>
        /// Adds a new server into the bootstrap and then start it.
        /// </summary>
        /// <param name="config">The new server's config.</param>
        /// <returns></returns>
        bool AddAndStart(IServerConfig config);
        
        /// <summary>
        /// Removes the server instance which is specified by name.
        /// </summary>
        /// <param name="name">The name of the server instance to be removed.</param>
        void Remove(string name);
        
    }
}