using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SuperSocket.SocketBase.Protocol;

namespace SuperSocket.SocketBase
{
    public interface IAppServer : IWorkItem, ILoggerProvider
    {
        
    }
    /// <summary>
    /// The raw data processor
    /// </summary>
    /// <typeparam name="TAppSession">The type of the app session.</typeparam>
    public interface IRawDataProcessor<TAppSession>
        where TAppSession : IAppSession
    {
        /// <summary>
        /// Gets or sets the raw binary data received event handler.
        /// TAppSession: session
        /// byte[]: receive buffer
        /// int: receive buffer offset
        /// int: receive lenght
        /// bool: whether process the received data further
        /// </summary>
        event Func<TAppSession, byte[], int, int, bool> RawDataReceived;
    }

    /// <summary>
    /// The interface for AppServer
    /// </summary>
    /// <typeparam name="TAppSession">The type of the app session.</typeparam>
    /// <typeparam name="TRequestInfo">The type of the request info.</typeparam>
    public interface IAppServer<TAppSession, TRequestInfo> : IAppServer<TAppSession>
        where TRequestInfo : IRequestInfo
        where TAppSession : IAppSession, IAppSession<TAppSession, TRequestInfo>, new()
    {
        /// <summary>
        /// Occurs when [request comming].
        /// </summary>
        event RequestHandler<TAppSession, TRequestInfo> NewRequestReceived;
    }
    
    /// <summary>
    /// The interface for handler of session request
    /// </summary>
    /// <typeparam name="TRequestInfo">The type of the request info.</typeparam>
    public interface IRequestHandler<TRequestInfo>
        where TRequestInfo : IRequestInfo
    {
        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="requestInfo">The request info.</param>
        void ExecuteCommand(IAppSession session, TRequestInfo requestInfo);
    }
    
    /// <summary>
    /// SocketServer Accessor interface
    /// </summary>
    public interface ISocketServerAccessor
    {
        /// <summary>
        /// Gets the socket server.
        /// </summary>
        /// <value>
        /// The socket server.
        /// </value>
        ISocketServer SocketServer { get; }
    }
    
    /// <summary>
    /// The basic interface for RemoteCertificateValidator
    /// </summary>
    public interface IRemoteCertificateValidator
    {
        /// <summary>
        /// Validates the remote certificate
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="certificate">The certificate.</param>
        /// <param name="chain">The chain.</param>
        /// <param name="sslPolicyErrors">The SSL policy errors.</param>
        /// <returns></returns>
        bool Validate(IAppSession session, object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors);
    }
}