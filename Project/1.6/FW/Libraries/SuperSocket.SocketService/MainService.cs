using System.ServiceProcess;
using SuperSocket.SocketBase;
using SuperSocket.SocketEngine;

namespace SuperSocket.SocketService
{
    partial class MainService : ServiceBase
    {
        private IBootstrap m_Bootstrap;

        public MainService()
        {
            InitializeComponent();
            m_Bootstrap = BootstrapFactory.CreateBootstrap();
        }

        protected override void OnStart(string[] args)
        {
            if (!m_Bootstrap.Initialize())
            {
                return;
            }

            m_Bootstrap.Start();
        }

        protected override void OnStop()
        {
            m_Bootstrap.Stop();
            base.OnStop();
        }

        protected override void OnShutdown()
        {
            m_Bootstrap.Stop();
            base.OnShutdown();
        }
    }
}