using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using SuperSocket.SocketBase.Metadata;

namespace SuperSocket.SocketEngine
{
    class DefaultBootstrapProcessWarp : DefaultBootstrapAppDomainWrap
    {
        public DefaultBootstrapProcessWarp(IBootstrap bootstrap, IConfigurationSource config, string startupConfigFile) 
            : base(bootstrap, config, startupConfigFile)
        {
            
        }

        protected override IWorkItem CreateWorkItemInstance(string serviceTypeName, StatusInfoAttribute[] serverStatusMetadata)
        {
            return new ProcessAppServer(serviceTypeName, serverStatusMetadata);
        }
    }
    
    class ProcessBootstrap : AppDomainBootstrap
    {
        public ProcessBootstrap(IConfigurationSource config) 
            : base(config)
        {
            var clientChannel = ChannelServices.RegisteredChannels.FirstOrDefault(c => c is IpcClientChannel);

            if (clientChannel == null)
            {
                // Create the channel.
                clientChannel = new IpcClientChannel();
                // Register the channel.
                ChannelServices.RegisterChannel(clientChannel, false);
            }
        }

        protected override IBootstrap CreateBootstrapWrap(IBootstrap bootstrap, IConfigurationSource config, string startupConfigFile)
        {
            return new DefaultBootstrapProcessWarp(bootstrap, config, startupConfigFile);
        }
    }
}