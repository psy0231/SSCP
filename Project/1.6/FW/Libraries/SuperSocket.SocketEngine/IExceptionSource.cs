using System;
using ErrorEventArgs = SuperSocket.Common.ErrorEventArgs;

namespace SuperSocket.SocketEngine
{
    public interface IExceptionSource
    {
        event EventHandler<ErrorEventArgs> ExceptionThrown;
    }
}