using System;
using System.Configuration;
using SuperSocket.Common;

namespace SuperSocket.SocketBase
{
    interface IConfigValueChangeNotifier
    {
        bool Notify(string newValue);
    }

    class ConfigValueChangeNotifier : IConfigValueChangeNotifier
    {
        Func<string, bool> m_Handler;

        public ConfigValueChangeNotifier(Func<string, bool> handler)
        {
            m_Handler = handler;
        }
        public bool Notify(string newValue)
        {
            return m_Handler(newValue);
        }
    }

    class ConfigValueChangeNotifier<TConfigOption> : IConfigValueChangeNotifier
        where TConfigOption : ConfigurationElement, new()
    {
        Func<TConfigOption, bool> m_Handler;

        public ConfigValueChangeNotifier(Func<TConfigOption, bool> handler)
        {
            m_Handler = handler;
        }
        public bool Notify(string newValue)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                return m_Handler(default(TConfigOption));
            }
            else
            {
                return m_Handler(ConfigurationExtension.DeserializeChildConfig<TConfigOption>(newValue));
            }
        }
    }
    
    
}