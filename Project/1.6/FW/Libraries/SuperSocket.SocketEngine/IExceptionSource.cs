using System;
using SuperSocket.Common;

namespace SuperSocket.SocketEngine
{
    interface IExceptionSource
    {
        event EventHandler<ErrorEventArgs> ExceptionThrown;
    }
}