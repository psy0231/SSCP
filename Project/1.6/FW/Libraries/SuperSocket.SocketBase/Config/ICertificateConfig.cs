using System.Security.Cryptography.X509Certificates;

namespace SuperSocket.SocketBase.Config
{
    public interface ICertificateConfig
    {
        string FilePath
        {
            get;
        }

        string Password
        {
            get;
        }

        string StroreName
        {
            get;
        }

        string Thumbprint
        {
            get;
        }

        StoreLocation StoreLocation
        {
            get;
        }

        bool ClientSertificateRequired
        {
            get;
        }

        X509KeyStorageFlags KeyStorageFlags
        {
            get;
        }
    }
}