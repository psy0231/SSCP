using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketBase.Logging;
using SuperSocket.SocketBase.Protocol;

namespace SuperSocket.SocketBase
{
    public class AppSession<TAppSession, TRequestInfo> : IAppSession, IAppSession<TAppSession, TRequestInfo>
        where TAppSession : AppSession<TAppSession, TRequestInfo>, IAppSession, new()
        where TRequestInfo : class, IRequestInfo
    {
        #region Properties

        public virtual AppServerBase<TAppSession, TRequestInfo> AppServer
        {
            get;
            private set;
        }

        #endregion
        public void Initialize(IAppServer<TAppSession, TRequestInfo> server, ISocketSession socketSession)
        {
            throw new NotImplementedException();
        }

        public string SessionID { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public IAppServer AppServer { get; }
        public ISocketSession SocketSession { get; }
        public IDictionary<object, object> Items { get; }
        public IServerConfig Config { get; }
        public IPEndPoint LocalEndPoint { get; }
        public DateTime LastActiveTime { get; set; }
        public DateTime StartTime { get; }
        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Close(CloseReason reason)
        {
            throw new NotImplementedException();
        }

        public bool Connected { get; }
        public Encoding Charset { get; set; }
        public string PrevCommand { get; set; }
        public string CurrentCommand { get; set; }
        public ILog Logger { get; }
        public int ProcessRequest(byte[] readBuffer, int offset, int length, bool toBeCopied)
        {
            throw new NotImplementedException();
        }

        public void StartSession()
        {
            throw new NotImplementedException();
        }
    }
}