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
}