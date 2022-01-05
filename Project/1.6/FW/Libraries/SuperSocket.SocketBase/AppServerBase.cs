using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.SocketBase.Command;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketBase.Logging;
using SuperSocket.SocketBase.Metadata;
using SuperSocket.SocketBase.Protocol;
using SuperSocket.SocketBase.Provider;
using SuperSocket.Common;

namespace SuperSocket.SocketBase.Security
{
    [AppServerMetadataType(typeof(DefaultAppServerMetadata))]
    public abstract partial class AppServerBase<TAppSession, TRequestInfo> : IAppServer<TAppSession, TRequestInfo>, 
        IRawDataProcessor<TAppSession>, IRequestHandler<TRequestInfo>, ISocketServerAccessor, IStatusInfoSource, 
        IRemoteCertificateValidator, IActiveConnector, ISystemEndPoint, IDisposable
        where TRequestInfo : class, IRequestInfo
        where TAppSession : AppSession<TAppSession, TRequestInfo>, IAppSession, new()
    {
        protected readonly TAppSession NullAppSession = default(TAppSession);

        public IServerConfig Config
        {
            get; 
            private set; 
            
        }

        private string m_Name;

        private int m_StateCode = ServerStateConst.NotInitialized;

        public ServerState State
        {
            get
            {
                return (ServerState)m_StateCode;
            }
        }

        public X509Certificate Certificate { get; private set; }
        
        public virtual IReceiveFilterFactory<TRequestInfo> ReceiveFilterFactory { get; protected set; }

        object IAppServer.ReceiveFilterFactory
        {
            get
            {
                return this.ReceiveFilterFactory;
            }
        }

        private List<ICommandLoader<ICommand<IAppSession, TRequestInfo>>> m_CommandLoaders = new List<ICommandLoader<ICommand<IAppSession, TRequestInfo>>>();

        private Dictionary<string, CommandInfo<ICommand<TAppSession, TRequestInfo>>> m_CommandContainer;

        private CommandFilterAttribute[] m_GlobalCommandFilters;

        private ISocketServerFactory m_SocketServerFactory;

        public SslProtocols BasicSecurity
        {
            get;
            private set;
        }


        protected IRootConfig RootConfig
        {
            get;
            private set;
        }

        public ILog Logger
        {
            get;
            private set;
        }

        protected IBootstrap BootStrap
        {
            get;
            private set;
        }

        private static bool m_ThreadPoolConfigured = false;

        private List<IConnectionFilter> m_ConnectionFilters;

        private long m_TotalHandledRequests = 0;

        protected long TotalHandledRequests
        {
            get
            {
                return m_TotalHandledRequests;
            }
        }

        private ListenerInfo[] m_Listeners;

        public ListenerInfo[] Listeners
        {
            get
            {
                return m_Listeners;
            }
        }

        public DateTime StartedTime
        {
            get;
            private set;
        }

        public ILogFactory LogFactory
        {
            get;
            private set;
        }

        public Encoding TextEncodig
        {
            get;
            private set;
        }

        public AppServerBase()
        {
            
        }

        protected AppServerBase(IReceiveFilterFactory<TRequestInfo> receiveFilterFactory)
        {
            this.ReceiveFilterFactory = receiveFilterFactory;
        }

        internal static CommandFilterAttribute[] GetCommandFilterAttributes(Type type)
        {
            var attrs = type.GetCustomAttributes(true);
            return attrs.OfType<CommandFilterAttribute>().ToArray();
        }

        protected virtual bool SetupCommands(Dictionary<string, ICommand<TAppSession, TRequestInfo>> discoveredCommands)
        {
            foreach (var loader in m_CommandLoaders)
            {
                loader.Error += new EventHandler<ErrorEventArgs>(CommandLoaderOnError);
                loader.Updated += new EventHandler<CommandUpdateEventArgs<ICommand<IAppSession, TRequestInfo>>>(CommandLoaderOnCommandsUpdated);

                if (!loader.Initialoze(RootConfig, this))
                {
                    if (Logger.IsErrorEnabled)
                    {
                        Logger.ErrorFormat("Failed initialize the command loader {0}.", loader.ToString());
                    }
                    return false;
                }

                IEnumerable<ICommand<TAppSession, TRequestInfo>> commands;
                if (!loader.TryLoadCommands(out commands))
                {
                    if (Logger.IsErrorEnabled)
                    {
                        Logger.ErrorFormat("Failed load Commands from the command loader {0}.",loader.ToString());
                    }
                    return false;
                }

                if (commands != null && commands.Any())
                {
                    foreach (var c in commands)
                    {
                        if (discoveredCommands.ContainsKey(c.Name))
                        {
                            if (Logger.IsErrorEnabled)
                            {
                                Logger.Error("Duplicated name command has been found! Command namd : "+ c.Name);
                            }
                            return false;
                        }
                        
                        var castedCommand = c as ICommand<TAppSession, TRequestInfo>;

                        if (castedCommand == null)
                        {
                            if (Logger.IsErrorEnabled)
                            {
                                Logger.Error("Invalid command has been found! Command name : " + c.Name);
                            }
                            return false;
                        }

                        if (Logger.IsDebugEnabled)
                        {
                            Logger.DebugFormat("The Command {0}({1}) has been discovered", castedCommand.Name, castedCommand.ToString());
                        }
                        discoveredCommands.Add(c.Name, castedCommand);
                    }
                }
            }

            return true;
        }
    
        
        void CommandLoaderOnCommandsUpdated(object sender, CommandUpdateEventArgs<ICommand<TAppSession, TRequestInfo>> e)
        {
            var workingDict = m_CommandContainer.Values.ToDictionary(c => c.Command.Name, c => c.Command, StringComparer.OrdinalIgnoreCase);
            var updatedCommands = 0;

            foreach (var c in e.Commands)
            {
                if (c == null)
                {
                    continue;
                }

                if (c.UpdateAction == CommandUpdateAction.Remove)
                {
                    workingDict.Remove(c.Command.Name);
                    if (Logger.IsInfoEnabled)
                    {
                        Logger.InfoFormat("The command '{0}' has been removed from this server!", c.Command.Name);
                    }
                }
                else if (c.UpdateAction == CommandUpdateAction.Add)
                {
                    workingDict.Add(c.Command.Name, c.Command);
                    if (Logger.IsInfoEnabled)
                    {
                        Logger.InfoFormat("The command '{0}' has been added into this server!", c.Command.Name);
                    }
                }
                else
                {
                    workingDict[c.Command.Name] = c.Command;
                    if (Logger.IsInfoEnabled)
                    {
                        Logger.InfoFormat("The command '{0}' nas been updated!", c.Command.Name);
                    }
                }

                updatedCommands++;
            }

            if (updatedCommands > 0)
            {
                OnCommandSetup(workingDict);
            }

        }
        
        void CommandLoaderOnError(object sender, ErrorEventArgs e)
        {
            if (!Logger.IsErrorEnabled)
            {
                return;
            }
            
            Logger.Error(e.Exception);
        }
        
        public bool Setup(IBootstrap bootstrap, IServerConfig config, ProviderFactoryInfo[] factories)
        {
            return true;
        }

        partial void SetDefaultCulture(IRootConfig rootConfig, IServerConfig config);

        private void SetupBasic(IRootConfig rootConfig, IServerConfig config, ISocketServerFactory socketServerFactory)
        {
            if (rootConfig == null)
            {
                throw new ArgumentNullException("rootConfig");
            }

            RootConfig = rootConfig;

            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            if (!string.IsNullOrEmpty(config.Name))
            {
                m_Name = config.Name;
            }
            else
            {
                m_Name = string.Format("{0}-{1}", this.GetType().Name, Math.Abs(this.GetHashCode()));
            }

            Config = config;
            
            SetDefaultCulture(rootConfig, config);

            if (!m_ThreadPoolConfigured)
            {
                if (!ThreadPoolEx.ResetThreadPool(rootConfig.MaxWorkingThreads >=0 ? rootConfig.MaxWorkingThreads : new Nullable<int>(),
                        rootConfig.MaxCompletionPortThreads >= 0? rootConfig.MaxCompletionPortThreads : new Nullable<int>(),
                        rootConfig.MinWorkingThreads >=0? rootConfig.MinWorkingThreads : new Nullable<int>(),
                        rootConfig.MinCompletionPortThreads >= 0 ? rootConfig.MinCompletionPortThreads : new Nullable<int>()))
                {
                    throw new Exception("Failed to configure thread pool!");
                }

                m_ThreadPoolConfigured = true;
            }

            if (socketServerFactory == null)
            {
                var socketServerFactoryType =
                    Type.GetType("SuperSocket.SocketEngine.SocketServerFactory, SuperSocket.SocketEngine", true);

                socketServerFactory = (ISocketServerFactory)Activator.CreateInstance(socketServerFactoryType);
            }

            m_SocketServerFactory = socketServerFactory;

            if (!string.IsNullOrEmpty(config.TextEncoding))
            {
                TextEncodig = Encoding.GetEncoding(config.TextEncoding);
            }
            else
            {
                TextEncodig = new ASCIIEncoding();
            }
        }

        private bool SetupMedium(IReceiveFilterFactory<TRequestInfo> receiveFilterFactory, IEnumerable<IConnectionFilter> connectionFilters, IEnumerable<ICommandLoader<ICommand<TAppSession, TRequestInfo>>> commandLoaders)
        {
            if (receiveFilterFactory != null)
            {
                ReceiveFilterFactory = receiveFilterFactory;
            }

            if (connectionFilters != null && connectionFilters.Any())
            {
                if (m_ConnectionFilters == null)
                {
                    m_ConnectionFilters = new List<IConnectionFilter>();
                }
                
                m_ConnectionFilters.AddRange(connectionFilters);
            }

            if (commandLoaders != null && commandLoaders.Any())
            {
                m_CommandLoaders.AddRange(commandLoaders);
            }

            return SetupCommandLoaders(m_CommandLoaders);
        }

        private bool SetupAdvenced(IServerConfig config)
        {
            if (!SetupSecurity(config))
            {
                return false;
            }

            if (!SetupListeners(config))
            {
                return false;
            }

            m_GlobalCommandFilters = GetCommandFilterAttributes(this.GetType());

            var discoveredCommands = new Dictionary<string, ICommand<TAppSession, TRequestInfo>>(StringComparer.OrdinalIgnoreCase);

            if (!SetupCommands(discoveredCommands))
            {
                return false;
            }

            OnCommandSetup(discoveredCommands);

            return true;
        }

        private void OnCommandSetup(IDictionary<string, ICommand<TAppSession, TRequestInfo>> discoveredCommands)
        {
            var commandContainer = new Dictionary<string, CommandInfo<ICommand<TAppSession, TRequestInfo>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var command in discoveredCommands.Values)
            {
                commandContainer.Add(command.Name, 
                    new CommandInfo<ICommand<TAppSession, TRequestInfo>>(command, m_GlobalCommandFilters));
            }

            Interlocked.Exchange(ref m_CommandContainer, commandContainer);
        }

        internal abstract IReceiveFilterFactory<TRequestInfo> CreateDefaultReceiveFilterFactory();

        private bool SetupFinal()
        {
            if (ReceiveFilterFactory == null)
            {
                ReceiveFilterFactory = CreateDefaultReceiveFilterFactory();

                if (ReceiveFilterFactory == null)
                {
                    if (Logger.IsErrorEnabled)
                    {
                        Logger.Error("receiveFilterFactory is required!");
                    }

                    return false;
                }
            }

            var plainConfig = Config as ServerConfig;

            if (plainConfig == null)
            {
                plainConfig = new ServerConfig(Config);

                if (string.IsNullOrEmpty(plainConfig.Name))
                {
                    plainConfig.Name = Name;
                }

                Config = plainConfig;
            }

            try
            {
                m_ServerStatus = new StatusInfoCollection();
                m_ServerStatus.Name = Name;
                m_ServerStatus.Tag = Name;
                m_ServerStatus[StatusInfoKeys.MaxConnectionNumber] = Config.MaxConnectionNumber;
                m_ServerStatus[StatusInfoKeys.Listeners] = m_Listeners;
            }
            catch (Exception e)
            {
                if (Logger.IsErrorEnabled)
                {
                    Logger.Error("Failed to create ServerSummary instance!", e);
                }
            }

            return SetupSocketServer();
        }

        public bool Setup(int port)
        {
            return Setup("Any", port);
        }

        private void TrySetInitializedState()
        {
            if (Interlocked.CompareExchange(ref m_StateCode, ServerStateConst.Initializing, ServerStateConst.NotInitialized) 
                != ServerStateConst.NotInitialized)
            {
                throw new Exception("The server has been initialized already, you cannot initialize it again!");
            }
        }

#if NET_35

    /// <summary>
        /// Setups with the specified ip and port.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="port">The port.</param>
        /// <param name="providers">The providers.</param>
        /// <returns></returns>
        public bool Setup(string ip, int port, params object[] providers)
        {
            return Setup(new ServerConfig
            {
                Name = string.Format("{0}-{1}", this.GetType().Name, Math.Abs(this.GetHashCode())),
                Ip = ip,
                Port = port
            }, providers);
        }
        /// <summary>
        /// Setups with the specified config, used for programming setup
        /// </summary>
        /// <param name="config">The server config.</param>
        /// <param name="providers">The providers.</param>
        /// <returns></returns>
        public bool Setup(IServerConfig config, params object[] providers)
        {
            return Setup(new RootConfig(), config, providers);
        }

        /// <summary>
        /// Setups with the specified root config, used for programming setup
        /// </summary>
        /// <param name="rootConfig">The root config.</param>
        /// <param name="config">The server config.</param>
        /// <param name="providers">The providers.</param>
        /// <returns></returns>
        public bool Setup(IRootConfig rootConfig, IServerConfig config, params object[] providers)
        {
            TrySetInitializedState();

            SetupBasic(rootConfig, config, GetProviderInstance<ISocketServerFactory>(providers));

            if (!SetupLogFactory(GetProviderInstance<ILogFactory>(providers)))
                return false;

            Logger = CreateLogger(this.Name);

            if (!SetupMedium(GetProviderInstance<IReceiveFilterFactory<TRequestInfo>>(providers), GetProviderInstance<IEnumerable<IConnectionFilter>>(providers), GetProviderInstance<IEnumerable<ICommandLoader<ICommand<TAppSession, TRequestInfo>>>>(providers)))
                return false;

            if (!SetupAdvanced(config))
                return false;

            if (!Setup(rootConfig, config))
                return false;

            if(!SetupFinal())
                return false;

            m_StateCode = ServerStateConst.NotStarted;
            return true;
        }

        private T GetProviderInstance<T>(object[] providers)
        {
            if (providers == null || !providers.Any())
                return default(T);

            var providerType = typeof(T);
            return (T)providers.FirstOrDefault(p => p != null && providerType.IsAssignableFrom(p.GetType()));
        }
        
#else
        public bool Setup(IServerConfig config, ISocketServerFactory socketServerFactory = null, IReceiveFilterFactory<TRequestInfo> receiveFilterFactory = null, ILogFactory logFactory = null, IEnumerable<IConnectionFilter> connectionFilters = null, IEnumerable<ICommandLoader<ICommand<TAppSession, TRequestInfo>>> commandLoaders = null)
        {
            return Setup(new RootConfig(), config, socketServerFactory, receiveFilterFactory, logFactory, connectionFilters, commandLoaders);
        }
        
        public bool Setup(IRootConfig rootConfig, IServerConfig config, ISocketServerFactory socketServerFactory = null, IReceiveFilterFactory<TRequestInfo> receiveFilterFactory = null, ILogFactory logFactory = null, IEnumerable<IConnectionFilter> connectionFilters = null, IEnumerable<ICommandLoader<ICommand<TAppSession, TRequestInfo>>> commandLoaders = null)
        {
            TrySetInitializedState();
            
            SetupBasic(rootConfig, config, socketServerFactory);

            if (!SetupLogFactory(logFactory))
            {
                return false;
            }

            Logger = CreateLogger(this.Name);

            if (!SetupMedium(receiveFilterFactory, connectionFilters, commandLoaders))
            {
                return false;
            }

            if (!SetupAdvenced(config))
            {
                return false;
            }

            if (!Setup(rootConfig, config))
            {
                return false;
            }

            if (!SetupFinal())
            {
                return false;
            }

            m_StateCode = ServerStateConst.NotStarted;
            return true;
        }
        
        public bool Setup(string ip, int port, ISocketServerFactory socketServerFactory = null, IReceiveFilterFactory<TRequestInfo> receiveFilterFactory = null, ILogFactory logFactory = null, IEnumerable<IConnectionFilter> connectionFilters = null, IEnumerable<ICommandLoader<ICommand<TAppSession, TRequestInfo>>> commandLoaders = null)
        {
            return Setup(new ServerConfig
            {
                Ip = ip,
                Port = port
            }, 
                socketServerFactory, 
                receiveFilterFactory, 
                logFactory, 
                connectionFilters, 
                commandLoaders);
        }
#endif

        bool IWorkItem.Setup(IBootstrap bootstrap, IServerConfig config, ProviderFactoryInfo[] factories)
        {
            if (bootstrap == null)
            {
                throw new ArgumentNullException("bootstrap");
            }

            BootStrap = bootstrap;

            if (factories == null)
            {
                throw new ArgumentNullException("factories");
            }
            
            TrySetInitializedState();

            var rootConfig = bootstrap.Config;
        }
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////
        

        public bool Start()
        {
            throw new NotImplementedException();
        }
        

        public void TransferSystemMessage(string messageType, object messageData)
        {
            throw new NotImplementedException();
        }

        public string Name { get; }


        public void ReportPotentialConfigChange(IServerConfig config)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public int SessionCount { get; }


        public IAppSession CreateAppSession(ISocketSession socketSession)
        {
            throw new NotImplementedException();
        }

        public bool RegisterSession(IAppSession session)
        {
            throw new NotImplementedException();
        }

        public IAppSession GetSessionByID(string sessionID)
        {
            throw new NotImplementedException();
        }

        public void ResetSessionSecurity(IAppSession session, SslProtocols security)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TAppSession> GetSessions(Func<TAppSession, bool> critera)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TAppSession> GetAllSessions()
        {
            throw new NotImplementedException();
        }

        public event SessionHandler<TAppSession> NewSessionConnected;
        public event SessionHandler<TAppSession, CloseReason> SessionClosed;
        public event RequestHandler<TAppSession, TRequestInfo> NewRequestReceived;
        public event Func<TAppSession, byte[], int, int, bool> RawDataReceived;
        public void ExecuteCommand(IAppSession session, TRequestInfo requestInfo)
        {
            throw new NotImplementedException();
        }

        public ISocketServer SocketServer { get; }

        public bool Validate(IAppSession session, object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            throw new NotImplementedException();
        }

        public Task<ActiveConnectResult> ActiveConnect(EndPoint targetEndPoint)
        {
            throw new NotImplementedException();
        }

        public Task<ActiveConnectResult> ActiceConnect(EndPoint targetEndPoint, EndPoint localEndPoint)
        {
            throw new NotImplementedException();
        }

        #region IActiveConnector

        Task<ActiveConnectResult> IActiveConnector.ActiceConnect(EndPoint targetEndPoint, EndPoint localEndPoint)
        {
            var activeConnector = m_SocketServer as IActiveConnector;

            if (activeConnector == null)
            {
                throw new Exception("this server cannot support active connect");
            }

            return activeConnector.ActiceConnect(targetEndPoint, localEndPoint);
        }

        Task<ActiveConnectResult> IActiveConnector.ActiveConnect(EndPoint targetEndpoint)
        {
            return ((IActiveConnector)this).ActiceConnect(targetEndpoint, null);
        }

        #endregion IActiveConnector
        
        #region ISystemEndPoint

        void ISystemEndPoint.TransferSystemMessage(string messageType, object messageData)
        {
            OnSystemMessageReceived(messageType, messageData);
        }

        protected virtual void OnSystemMessageReceived(string messageType, object messageData)
        {
            
        }

        #endregion ISystemEndPoint

        #region IStatusInfoSource

        private StatusInfoCollection m_ServerStatus;
        
        StatusInfoAttribute[] IStatusInfoSource.GetServerStatusMetadata()
        {
            return this.GetType().GetStatusInfoMetadata();
        }

        StatusInfoCollection IStatusInfoSource.CollectServerStatus(StatusInfoCollection bootstrapStatus)
        {
            UpdateServerStatus(m_ServerStatus);
            this.AsyncRun(() => OnServerStatusCollected(bootstrapStatus, m_ServerStatus), e => Logger.Error(e));
            return m_ServerStatus;
        }

        protected virtual void UpdateServerStatus(StatusInfoCollection serverStatus)
        {
            DateTime now = DateTime.Now;

            serverStatus[StatusInfoKeys.IsRunning] = m_StateCode == ServerStateConst.Running;
            serverStatus[StatusInfoKeys.TotalConnections] = this.SessionCount;

            var totalHandledRequests0 = serverStatus.GetValue<long>(StatusInfoKeys.TotalHandledRequests, 0);

            var totalHandledRequests = this.TotalHandledRequests;

            serverStatus[StatusInfoKeys.RequestHandlingSpeed] = ((totalHandledRequests - totalHandledRequests0) / now.Subtract(serverStatus.CollectedTime).TotalSeconds);
            serverStatus[StatusInfoKeys.TotalHandledRequests] = totalHandledRequests;

            if (State == ServerState.Running)
            {
                var sendingQueuePool = m_SocketServer.SendingQueuePool;
                serverStatus[StatusInfoKeys.AvailableSendingQueueItems] = sendingQueuePool.AvailableItemCount;
                serverStatus[StatusInfoKeys.TotalSendingQueueItems] = sendingQueuePool.TotalItemCount;
            }
            else
            {
                serverStatus[StatusInfoKeys.AvailableSendingQueueItems] = 0;
                serverStatus[StatusInfoKeys.TotalSendingQueueItems] = 0;
            }

            serverStatus.CollectedTime = now;
        }

        protected virtual void OnServerStatusCollected(StatusInfoCollection bootstrapStatus, StatusInfoCollection serverStatus)
        {
            
        }
        #endregion IStatusInfoSource

        #region IDisposible Members

        public void Dispose()
        {
            if (m_StateCode == ServerStateConst.Running)
            {
                Stop();
            }
        }

        #endregion
    }
}