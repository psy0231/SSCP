using System;
using System.Collections.Generic;

namespace SuperSocket.SocketBase.Command
{
    public class CommandUpdateEventArgs<T> : EventArgs
    {
        public IEnumerable<CommandUpdateInfo<T>> Commands
        {
            get;
            private set;
        }

        public CommandUpdateEventArgs(IEnumerable<CommandUpdateInfo<T>> commands)
        {
            Commands = commands;
        }

    }
}