using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SuperSocket.SocketBase
{
    public static class Async
    {
        public static Task AsyncRun(this ILoggerProvider logProvider, Action task)
        {
            return AsyncRun(logProvider, task, TaskCreationOptions.None);
        }

        public static Task AsyncRun(this ILoggerProvider logProvider, Action task, TaskCreationOptions taslOption)
        {
            return AsyncRun(logProvider, task, taslOption, null);
        }
        
        public static Task AsyncRun(this ILoggerProvider logProvider, Action task, Action<Exception> exceptionHandler)
        {
            return AsyncRun(logProvider, task, TaskCreationOptions.None, exceptionHandler);
        }

        public static Task AsyncRun(this ILoggerProvider logProvider, Action task, TaskCreationOptions taskOption, Action<Exception> exceptionHandler)
        {
            return Task.Factory.StartNew(task, taskOption).ContinueWith(t =>
            {
                if (exceptionHandler != null)
                {
                    exceptionHandler(t.Exception);
                }
                else
                {
                    if (logProvider.Logger.IsErrorEnabled)
                    {
                        for (int i = 0; i < t.Exception.InnerExceptions.Count; i++)
                        {
                            logProvider.Logger.Error(t.Exception.InnerExceptions[i]);
                        }
                    }
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        
        public static Task AsyncRun(this ILoggerProvider logProvider, Action<object> task, object state)
        {
            return AsyncRun(logProvider, task, state, TaskCreationOptions.None);
        }

        public static Task AsyncRun(this ILoggerProvider logProvider, Action<object> task, object state, TaskCreationOptions taskOption)
        {
            return AsyncRun(logProvider, task, state, taskOption, null);
        }

        public static Task AsyncRun(this ILoggerProvider logProvider, Action<object> task, object state, Action<Exception> exceptionHandler)
        {
            return AsyncRun(logProvider, task, state, TaskCreationOptions.None, exceptionHandler);
        }

        public static Task AsyncRun(this ILoggerProvider logProvider, Action<object> task, object state, TaskCreationOptions taskOption, Action<Exception> exceptionHandler)
        {
            return Task.Factory.StartNew(task, state, taskOption).ContinueWith(t =>
            {
                if (exceptionHandler != null)
                {
                    exceptionHandler(t.Exception);
                }
                else
                {
                    if (logProvider.Logger.IsErrorEnabled)
                    {
                        for (int i = 0; i < t.Exception.InnerExceptions.Count; i++)
                        {
                            logProvider.Logger.Error(t.Exception.InnerExceptions[i]);
                        }
                    }
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}