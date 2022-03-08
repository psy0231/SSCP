using System.Configuration;
using SuperSocket.Common;
using SuperSocket.SocketBase.Config;

namespace SuperSocket.SocketEngine.Configuration
{
    public class CommandAssembly : ConfigurationElement, ICommandAssemblyConfig
    {
        [ConfigurationProperty("assembly", IsRequired = false)]
        public string Assembly 
        {
            get
            {
                return this["assembly"] as string;
            }
        }
    }

    [ConfigurationCollection(typeof(CommandAssembly))]
    public class CommandAssemblyCollection : GenericConfigurationElementCollectionBase<CommandAssembly, ICommandAssemblyConfig>
    {
        
    }
}