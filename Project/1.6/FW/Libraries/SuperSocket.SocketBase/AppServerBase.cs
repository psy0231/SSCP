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
    public abstract partial class AppServerBase<TAppSession, TRequestInfo> : IAppServer<TAppSession, TRequestInfo>, IRawDataProcessor<TAppSession>, IRequestHandler<TRequestInfo>, ISocketServerAccessor, IStatusInfoSource, IRemoteCertificateValidator, IActiveConnector, ISystemEndPoint, IDisposable
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

        private List<ICommandLoader<ICommand<TAppSession, TRequestInfo>>> m_CommandLoaders = new List<ICommandLoader<ICommand<TAppSession, TRequestInfo>>>();

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
                loader.Updated += new EventHandler<CommandUpdateEventArgs<ICommand<TAppSession, TRequestInfo>>>(CommandLoaderOnCommandsUpdated);

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

        private bool SetupAdvanced(IServerConfig config)
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

            if (!SetupAdvanced(config))
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
            
            SetupBasic(rootConfig, config, GetSingleProviderInstance<ISocketServerFactory>(factories, ProviderKey.SocketServerFactory));

            if (!SetupLogFactory(GetSingleProviderInstance<ILogFactory>(factories, ProviderKey.LogFactory)))
            {
                return false;
            }

            Logger = CreateLogger(this.Name);

            IEnumerable<IConnectionFilter> connectionFilters = null;

            if (!TryGetProviderInstances(factories, ProviderKey.ConnectionFilter, null, (p, f) =>
                {
                    var ret = p.Initialize(f.Name, this);

                    if (!ret)
                    {
                        Logger.ErrorFormat("Failed to initialize the connection filter: {0}.", f.Name);
                    }

                    return ret;
                }, out connectionFilters))
            {
                return false;
            }

            if (!SetupMedium(
                    GetSingleProviderInstance<IReceiveFilterFactory<TRequestInfo>>(factories, ProviderKey.ReceiveFilterFactory),
                    connectionFilters, 
                    GetProviderInstances<ICommandLoader<ICommand<TAppSession, TRequestInfo>>>(
                        factories, 
                        ProviderKey.CommandLoader, 
                        (t)=> Activator.CreateInstance(t.MakeGenericType(typeof(ICommand<TAppSession,TRequestInfo>))))));
            {
                return false;
            }

            if (!SetupAdvanced(config))
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

        private TProvider GetSingleProviderInstance<TProvider>(ProviderFactoryInfo[] factories, ProviderKey key)
        {
            var factory = factories.FirstOrDefault(p => p.Key.Name == key.Name);

            if (factory == null)
            {
                return default(TProvider);
            }

            return factory.ExportFactory.CreateExport<TProvider>();
        }

        private bool TryGetProviderInstances<TProvider>(ProviderFactoryInfo[] factories, ProviderKey key, Func<Type, object> creator, Func<TProvider, ProviderFactoryInfo, bool> initializer, out IEnumerable<TProvider> providers)
            where TProvider : class
        {
            IEnumerable<ProviderFactoryInfo> selectedFactories = factories.Where(p => p.Key.Name == key.Name);

            if (!selectedFactories.Any())
            {
                providers = null;
                return true;
            }

            providers = new List<TProvider>();

            var list = (List<TProvider>)providers;

            foreach (var f in selectedFactories)
            {
                var provider = creator == null ? f.ExportFactory.CreateExport<TProvider>() : f.ExportFactory.CreateExport<TProvider>(creator);

                if (!initializer(provider, f))
                {
                    return false;
                }
                
                list.Add(provider);
            }

            return true;
        }

        private IEnumerable<TProvider> GetProviderInstances<TProvider>(ProviderFactoryInfo[] factories, ProviderKey key)
            where TProvider : class
        {
            return GetProviderInstances<TProvider>(factories, key, null);
        }

        private IEnumerable<TProvider> GetProviderInstances<TProvider>(ProviderFactoryInfo[] factories, ProviderKey key, Func<Type, object> creator)
            where TProvider : class
        {
            IEnumerable<TProvider> providers;
            TryGetProviderInstances<TProvider>(factories, key, creator, (p, f) => true, out providers);
            return providers;
        }

        private bool SetupLogFactory(ILogFactory logFactory)
        {
            if (logFactory !=null)
            {
                LogFactory = logFactory;
                return true;
            }

            if (LogFactory == null)
            {
                LogFactory = new Log4NetLogFactory();
            }

            return true;
        }

        protected virtual bool SetupCommandLoaders(List<ICommandLoader<ICommand<TAppSession, TRequestInfo>>> commandLoaders)
        {
            commandLoaders.Add(new ReflectCommandLoader<ICommand<TAppSession, TRequestInfo>>());
            return true;
        }

        protected virtual ILog CreateLogger(string loggerName)
        {
            return LogFactory.GetLog(loggerName);
        }

        private bool SetupSecurity(IServerConfig config)
        {
            if (!string.IsNullOrEmpty(config.Security))
            {
                SslProtocols configProtocol;
                if (!config.Security.TryParseEnum<SslProtocols>(true, out configProtocol))
                {
                    if (Logger.IsErrorEnabled)
                    {
                        Logger.ErrorFormat("Failed to parse '{0}' to SslProtocol!", config.Security);
                    }

                    return false;
                }

                BasicSecurity = configProtocol;
            }
            else
            {
                BasicSecurity = SslProtocols.None;
            }

            try
            {
                var certificate = GetCertificate(config.Certificate);

                if (certificate != null)
                {
                    Certificate = certificate;
                }
                else if (BasicSecurity != SslProtocols.None)
                {
                    if (Logger.IsErrorEnabled)
                    {
                        Logger.Error("Certificate is required in this security mode!");
                    }

                    return false;
                }
                
            }
            catch (Exception e)
            {
                if (Logger.IsErrorEnabled)
                {
                    Logger.Error("Failed to initialize certificate!", e);
                }

                return false;
            }

            return true;
        }

        protected virtual X509Certificate GetCertificate(ICertificateConfig certificate)
        {
            if (certificate == null)
            {
                if (BasicSecurity != SslProtocols.None && Logger.IsErrorEnabled)
                {
                    Logger.Error("There is no certificate configured!");
                }

                return null;
            }

            if (string.IsNullOrEmpty(certificate.FilePath) && string.IsNullOrEmpty(certificate.Thumbprint))
            {
                if (BasicSecurity != SslProtocols.None && Logger.IsErrorEnabled)
                {
                    Logger.Error("You should define certificate node and either attribute 'filePath' or 'thumbprint' is required!");
                }

                return null;
            }

            return CertificateManager.Initialize(certificate, GetFilePath);
        }

        bool IRemoteCertificateValidator.Validate(IAppSession session, object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return ValidateClientCertificate((TAppSession)session, sender, certificate, chain, sslPolicyErrors);
        }

        protected virtual bool ValidateClientCertificate(TAppSession session, object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }

        private bool SetupSocketServer()
        {
            try
            {
                m_SocketServer = m_SocketServerFactory.CreateSocketServer<TRequestInfo>(this, m_Listeners, Config);
                return m_SocketServer != null;
            }
            catch (Exception e)
            {
                if (Logger.IsErrorEnabled)
                {
                    Logger.Error(e);
                }

                return false;
            }
        }

        private IPAddress ParseIPAddress(string ip)
        {
            if (string.IsNullOrEmpty(ip) || "Any".Equals(ip, StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.Any;
            }
            else if ("IPv6Any".Equals(ip, StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.IPv6Any;
            }
            else
            {
                return IPAddress.Parse(ip);
            }
        }

        private bool SetupListeners(IServerConfig config)
        {
            var listeners = new List<ListenerInfo>();

            try
            {
                if (config.Port>0)
                {
                    listeners.Add(new ListenerInfo
                    {
                        EndPoint = new IPEndPoint(ParseIPAddress(config.Ip), config.Port),
                        BackLog = config.ListenBacklog,
                        Security = BasicSecurity
                    });
                }
                else
                {
                    if (!string.IsNullOrEmpty(config.Ip))
                    {
                        if (Logger.IsErrorEnabled)
                        {
                            Logger.Error("Port is required in config");
                        }

                        return false;
                    }
                }

                if (config.Listeners != null && config.Listeners.Any())
                {
                    if (listeners.Any())
                    {
                        if (Logger.IsErrorEnabled)
                        {
                            Logger.Error("If you configured Ip and Port in server node, you cannot defined listener in listeners node any more!");
                        }

                        return false;
                    }

                    foreach (var l in config.Listeners)
                    {
                        SslProtocols configProtocol;

                        if (string.IsNullOrEmpty(l.Security))
                        {
                            configProtocol = BasicSecurity;
                        }else if (!l.Security.TryParseEnum<SslProtocols>(true, out configProtocol))
                        {
                            if (Logger.IsErrorEnabled)
                            {
                                Logger.ErrorFormat("Failed to parse '{0}' to SslProtocol!", config.Security);
                            }

                            return false;
                        }

                        if (configProtocol != SslProtocols.None && (Certificate == null))
                        {
                            if (Logger.IsErrorEnabled)
                            {
                                Logger.Error("Threr if no Certificate loaded, but there is a secure listener defined!");
                            }

                            return false;
                        }
                        
                        listeners.Add(new ListenerInfo
                        {
                            EndPoint = new IPEndPoint(ParseIPAddress(l.Ip), l.Port),
                            BackLog = l.Backlog,
                            Security = configProtocol
                        });
                    }
                }

                if (!listeners.Any())
                {
                    if (Logger.IsErrorEnabled)
                    {
                        Logger.Error("No listener defined!");
                    }

                    return false;
                }

                m_Listeners = listeners.ToArray();

                return false;
            }
            catch (Exception e)
            {
                if (Logger.IsErrorEnabled)
                {
                    Logger.Error(e);
                }

                return false;
            }
        }

        public string Name
        {
            get
            {
                return m_Name;
            }
        }

        private ISocketServer m_SocketServer;

        ISocketServer ISocketServerAccessor.SocketServer
        {
            get
            {
                return m_SocketServer;
            }
        }

        public virtual bool Start()
        {
            var origStateCode = Interlocked.CompareExchange(ref m_StateCode, ServerStateConst.Starting, ServerStateConst.NotStarted);

            
            if (origStateCode != ServerStateConst.NotStarted)
            {
                if (origStateCode != ServerStateConst.NotStarted)
                {
                    throw new Exception("You cannot start a server instance which has not been setup yet.");
                }
                
                if (Logger.IsErrorEnabled)
                {
                    Logger.ErrorFormat("This server instance is in the state {0}, you cannot start it now.", (ServerState)origStateCode);
                }
                
                return false;
            }

            if (!m_SocketServer.Start())
            {
                m_StateCode = ServerStateConst.NotStarted;
                return false;
            }
            
            StartedTime = DateTime.Now;
            m_StateCode = ServerStateConst.Running;

            m_ServerStatus[StatusInfoKeys.IsRunning] = true;
            m_ServerStatus[StatusInfoKeys.StartedTime] = StartedTime;

            try
            {
                #pragma warning disable 0612,618
                OnStartup();
                #pragma warning restore 0612,618

                OnStarted();
            }
            catch (Exception e)
            {
                if (Logger.IsErrorEnabled)
                {
                    Logger.Error("One exception wa thrown in the method 'OnStartup()'.",e);
                }
            }
            finally
            {
                if (Logger.IsFatalEnabled)
                {
                    Logger.Info(string.Format("The server instance {0} has been started!",Name));
                }
            }

            return true;
        }

        [Obsolete("Use OnStarted() insted")]
        protected virtual void OnStartup()
        {
            
        }

        protected virtual void OnStarted()
        {
            
        }
        
        protected virtual void OnStopped()
        {

        }
        public virtual void Stop()
        {
            if (Interlocked.CompareExchange(ref m_StateCode, ServerStateConst.Stopping, ServerStateConst.Running)
                != ServerStateConst.Running)
            {
                return;
            }

            m_SocketServer.Stop();

            m_StateCode = ServerStateConst.NotStarted;

            OnStopped();

            m_ServerStatus[StatusInfoKeys.IsRunning] = false;
            m_ServerStatus[StatusInfoKeys.StartedTime] = null;

            if (Logger.IsInfoEnabled)
            {
                Logger.Info(string.Format("The server instance {0} has been stopped!", Name));
            }
        }

        private CommandInfo<ICommand<TAppSession, TRequestInfo>> GetCommandByName(string commandName)
        {
            CommandInfo<ICommand<TAppSession, TRequestInfo>> commandProxy;

            if (m_CommandContainer.TryGetValue(commandName, out commandProxy))
            {
                return commandProxy;
            }
            else
            {
                return null;
            }
        }

        private Func<TAppSession, byte[], int, int, bool> m_RawDataReceivedHandler;
        
        event Func<TAppSession, byte[], int, int, bool> IRawDataProcessor<TAppSession>.RawDataReceived
        {
            add
            {
                m_RawDataReceivedHandler += value;
            }

            remove
            {
                m_RawDataReceivedHandler -= value;
            }
        }

        internal bool OnRawDataReceived(IAppSession session, byte[] buffer, int offset, int length)
        {
            var handler = m_RawDataReceivedHandler;
            if (handler == null)
            {
                return true;
            }

            return handler((TAppSession)session, buffer, offset, length);
        }

        private RequestHandler<TAppSession, TRequestInfo> m_RequestHandler;
        
        public virtual event RequestHandler<TAppSession, TRequestInfo> NewRequestReceived
        {
            add
            {
                m_RequestHandler += value;
            }
            remove
            {
                m_RequestHandler -= value;
            }
        }

        protected virtual void ExecuteCommand(TAppSession session, TRequestInfo requestInfo)
        {
            if (m_RequestHandler == null)
            {
                var commandProxy = GetCommandByName(requestInfo.Key);

                if (commandProxy != null)
                {
                    var command = commandProxy.Command;
                    var commandFilters = commandProxy.Filters;

                    session.CurrentCommand = requestInfo.Key;

                    var cancelled = false;

                    if (commandFilters == null)
                    {
                        command.ExcuteCommand(session, requestInfo);
                    }
                    else
                    {
                        var commandContext = new CommandExecutingContext();
                        commandContext.Initialize(session, requestInfo, command);

                        for (int i = 0; i < commandFilters.Length; i++)
                        {
                            var filter = commandFilters[i];
                            filter.OnCommandExecuting(commandContext);

                            if (commandContext.Cancel)
                            {
                                cancelled = true;
                                if (Logger.IsInfoEnabled)
                                {
                                    Logger.Info(session, string.Format("The excuting of the command {0} was cancelled by the command filter {1}.", command.Name, filter.GetType().ToString()));
                                }

                                break;
                            }
                        }

                        if (!cancelled)
                        {
                            try
                            {
                                command.ExcuteCommand(session, requestInfo);
                            }
                            catch (Exception exc)
                            {
                                commandContext.Exception = exc;
                            }

                            for (int i = 0; i < commandFilters.Length; i++)
                            {
                                var filter = commandFilters[i];
                                filter.OnCommandExecuted(commandContext);
                            }

                            if (commandContext.Exception != null && !commandContext.ExceptionHandled)
                            {
                                try
                                {
                                    session.InternalHandleException(commandContext.Exception);
                                }
                                catch 
                                {
                                    
                                }
                            }
                        }
                    }

                    if (!cancelled)
                    {
                        session.PrevCommand = requestInfo.Key;

                        if (Config.LogCommand && Logger.IsInfoEnabled)
                        {
                            Logger.Info(session, string.Format("Command - {0}", requestInfo.Key));
                        }
                    }
                }
                else
                {
                    session.InternalHandleUnknownRequest(requestInfo);
                }
                
                session.LastActiveTime = DateTime.Now;
            }
            else
            {
                session.CurrentCommand = requestInfo.Key;

                try
                {
                    m_RequestHandler(session, requestInfo);
                }
                catch (Exception e)
                {
                    session.InternalHandleException(e);
                }

                session.PrevCommand = requestInfo.Key;
                session.LastActiveTime = DateTime.Now;

                if (Config.LogCommand && Logger.IsInfoEnabled)
                {
                    Logger.Info(session, string.Format("Command - {0}", requestInfo.Key));
                }
            }

            Interlocked.Increment(ref m_TotalHandledRequests);
        }

        internal void ExecuteCommand(IAppSession session, TRequestInfo requestInfo)
        {
            this.ExecuteCommand((TAppSession)session, requestInfo);
        }

        void IRequestHandler<TRequestInfo>.ExecuteCommand(IAppSession session, TRequestInfo requestInfo)
        {
            this.ExecuteCommand((TAppSession)session, requestInfo);
        }

        public IEnumerable<IConnectionFilter> ConnectionFilters
        {
            get
            {
                return m_ConnectionFilters;
            }
        }

        private bool ExecuteConnectionFilters(IPEndPoint remoteAddress)
        {
            if (m_ConnectionFilters == null)
            {
                return true;
            }

            for (int i = 0; i < m_ConnectionFilters.Count; i++)
            {
                var currentFilter = m_ConnectionFilters[i];
                if (!currentFilter.AllowConnect(remoteAddress))
                {
                    if (Logger.IsInfoEnabled)
                    {
                        Logger.InfoFormat("A connection from {0} has been refused by filter {1}!", remoteAddress, currentFilter.Name);
                    }
                    return false;
                }
            }

            return true;
        }

        IAppSession IAppServer.CreateAppSession(ISocketSession socketSession)
        {
            if (!ExecuteConnectionFilters(socketSession.RemoteEndPoint))
            {
                return NullAppSession;
            }

            var appSession = CreateAppSession(socketSession);

            appSession.Initialize(this, socketSession);

            return appSession;
        }
        
        public virtual TAppSession CreateAppSession(ISocketSession socketSession)
        {
            return new TAppSession();
        }

        bool IAppServer.RegisterSession(IAppSession session)
        {
            var appSession = session as TAppSession;

            if (!RegisterSession(appSession.SessionID, appSession))
            {
                return false;
            }

            appSession.SocketSession.Closed += OnSocketSessionClosed;

            if (Config.LogBasicSessionActivity &&  Logger.IsInfoEnabled)
            {
                Logger.Info(session, "A new session connected!");
            }

            OnNewSessionConnected(appSession);
            return true;
        }

        protected virtual bool RegisterSession(string sessionID, TAppSession appSession)
        {
            return true;
        }

        private SessionHandler<TAppSession> m_NewSessionConnected;
        
        public event SessionHandler<TAppSession> NewSessionConnected
        {
            add
            {
                m_NewSessionConnected += value;
            }
            remove
            {
                m_NewSessionConnected -= value;
            }
        }

        protected virtual void OnNewSessionConnected(TAppSession session)
        {
            var handler = m_NewSessionConnected;
            if (handler == null)
            {
                return;
            }

            handler.BeginInvoke(session, OnNewSessionConnectedCallback, handler);
        }

        private void OnNewSessionConnectedCallback(IAsyncResult result)
        {
            try
            {
                var handler = (SessionHandler<TAppSession>)result.AsyncState;
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public void ResetSessionSecurity(IAppSession session, SslProtocols security)
        {
            m_SocketServer.ResetSessionSecurity(session, security);
        }

        private void OnSocketSessionClosed(ISocketSession session, CloseReason reason)
        {
            if (Logger.IsInfoEnabled && (Config.LogBasicSessionActivity || (reason != CloseReason.ServerClosing && reason != CloseReason.ClientClosing && reason != CloseReason.ServerShutdown && reason != CloseReason.SocketError)))
            {
                Logger.Info(session, string.Format("This session was closed for {0}!", reason));
            }

            var appSession = session.AppSession as TAppSession;
            appSession.Connected = false;
            OnSessionClosed(appSession, reason);
        }

        private SessionHandler<TAppSession, CloseReason> m_SessionClosed;
        
        public event SessionHandler<TAppSession, CloseReason> SessionClosed
        {
            add
            {
                m_SessionClosed += value;
            }
            remove
            {
                m_SessionClosed -= value;
            }
        }

        protected virtual void OnSessionClosed(TAppSession session, CloseReason reason)
        {
            var handler = m_SessionClosed;

            if (handler != null)
            {
                handler.BeginInvoke(session, reason, OnSessionClosedCallback, handler);
            }

            session.OnSessionClosed(reason);
        }

        private void OnSessionClosedCallback(IAsyncResult result)
        {
            try
            {
                var handler = (SessionHandler<TAppSession, CloseReason>)result.AsyncState;
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
        
        public abstract TAppSession GetSessionByID(string sessionID);

        IAppSession IAppServer.GetSessionByID(string sessionID)
        {
            return this.GetSessionByID(sessionID);
        }
        
        public virtual IEnumerable<TAppSession> GetSessions(Func<TAppSession, bool> critera)
        {
            throw new NotSupportedException();
        }
        
        public virtual IEnumerable<TAppSession> GetAllSessions()
        {
            throw new NotSupportedException();
        }

        public abstract int SessionCount
        {
            get;
        }

        public string GetFilePath(string relativeFilePath)
        {
            var filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativeFilePath);

            if (!System.IO.File.Exists(filePath) && RootConfig != null && RootConfig.Isolation != IsolationMode.None)
            {
                var rootDir = System.IO.Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName;
                var rootFilePath = System.IO.Path.Combine(rootDir, relativeFilePath);

                if (System.IO.File.Exists(rootFilePath))
                {
                    return rootFilePath;
                }
            }

            return filePath;
        }
        /////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public void TransferSystemMessage(string messageType, object messageData)
        {
            throw new NotImplementedException();
        }

        public void ReportPotentialConfigChange(IServerConfig config)
        {
            throw new NotImplementedException();
        }

        public event Func<TAppSession, byte[], int, int, bool> RawDataReceived;

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
                serverStatus[StatusInfoKeys.AvailableSendingQueueItems] = sendingQueuePool.AvialableItemsCount;
                serverStatus[StatusInfoKeys.TotalSendingQueueItems] = sendingQueuePool.TotalItemsCount;
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