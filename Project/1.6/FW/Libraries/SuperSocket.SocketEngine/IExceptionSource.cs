using System;
using System.IO;

namespace SuperSocket.SocketEngine
{
    public interface IExceptionSource
    {
        event EventHandler<ErrorEventArgs> ExceptionThrown;
    }
}