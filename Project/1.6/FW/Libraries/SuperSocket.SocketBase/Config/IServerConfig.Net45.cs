namespace SuperSocket.SocketBase.Config
{
    /// <summary>
    /// IServerConfig, the part compatible with .Net 4.5 or higher
    /// </summary>
    public partial interface IServerConfig
    {
        /// <summary>
        /// Gets the default culture for this server.
        /// </summary>
        /// <value>
        /// The default culture.
        /// </value>
        string DefaultCulture
        {
            get;
        }
    }
}