using System.Net;

namespace SuperSocket.SocketBase.Protocol
{
    /// <summary>
    /// Receive filter factory interface
    /// </summary>
    public interface IReceiverFilterFactory
    {
        
    }

    /// <summary>
    /// Receive filter factory interface
    /// </summary>
    /// <typeparam name="TRequestInfo">The type of the request info.</typeparam>
    public interface IReceiverFilterFactory<TRequestInfo> : IReceiverFilterFactory
        where TRequestInfo : IRequestInfo
    {
        /// <summary>
        /// Creates the Receive filter.
        /// </summary>
        /// <param name="appServer">The app server.</param>
        /// <param name="appSession">The app session.</param>
        /// <param name="remoteEndPoint">The remote end point.</param>
        /// <returns>
        /// the new created request filer assosiated with this socketSession
        /// </returns>
        IReceiverFilter<TRequestInfo> CreateFileter(IAppServer appServer, IAppSession appSession,
            IPEndPoint remoteEndPoint);
    }
}