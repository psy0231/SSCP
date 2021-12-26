using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using SuperSocket.SocketBase.Config;

namespace SuperSocket.SocketBase.Security
{
    static class CertificateManager
    {
        internal static X509Certificate Initialize(ICertificateConfig cerConfig, Func<string, string> relativePatheHandler)
        {
            if (!string.IsNullOrEmpty(cerConfig.FilePath))
            {
                string filePath;

                if (Path.IsPathRooted(cerConfig.FilePath))
                {
                    filePath = cerConfig.FilePath;
                }
                else
                {
                    filePath = relativePatheHandler(cerConfig.FilePath);
                }

                return new X509Certificate2(filePath, cerConfig.Password, cerConfig.KeyStorageFlags);
                
            }
            else
            {
                var storeName = cerConfig.StroeName;

                if (string.IsNullOrEmpty(storeName))
                {
                    storeName = "ROot";
                }

                var store = new X509Store(storeName, cerConfig.StoreLocation);
                
                store.Open(OpenFlags.ReadOnly);

                var cert = store.Certificates.OfType<X509Certificate2>().Where(c =>
                    c.Thumbprint.Equals(cerConfig.Thumbprint, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                
                store.Close();

                return cert;
            }
        }
        
    }
}