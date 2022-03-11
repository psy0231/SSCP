using System;
using System.Net;
using System.Net.Sockets;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;

namespace SuperSocket.SocketEngine
{
    public delegate void ErrorHandler(ISocketListener listener, Exception e);

    public delegate void NewClientAcceptHandler(ISocketListener listener, Socket client, object state);
    
    /// <summary>
    /// The interface for socket listener
    /// </summary>
    public interface ISocketListener
    {
        /// <summary>
        /// Gets the info of listener
        /// </summary>
        ListenerInfo Info { get; }
        
        /// <summary>
        /// Gets the end point the listener is working on
        /// </summary>
        IPEndPoint EndPoint { get; }

        /// <summary>
        /// Starts to listen
        /// </summary>
        /// <param name="config">The server config.</param>
        /// <returns></returns>
        bool Start(IServerConfig config);

        /// <summary>
        /// Stops listening
        /// </summary>
        void Stop();

        /// <summary>
        /// Occurs when new client accepted.
        /// </summary>
        event NewClientAcceptHandler NewClientAccepted;

        /// <summary>
        /// Occurs when error got.
        /// </summary>
        event ErrorHandler Error;

        
        /// <summary>
        /// Occurs when [stopped].
        /// </summary>
        event EventHandler Stopped;
    }
}