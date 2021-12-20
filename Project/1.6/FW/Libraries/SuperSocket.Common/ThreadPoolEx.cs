﻿using System.Threading;

namespace SuperSocket.Common
{
    /// <summary>
    /// Thread pool extension class
    /// </summary>
    public static class ThreadPoolEx
    {
        /// <summary>
        /// Resets the thread pool.
        /// </summary>
        /// <param name="maxWorkingThreads">The max working threads.</param>
        /// <param name="maxCompletionPortThreads">The max completion port threads.</param>
        /// <param name="minWorkingThreads">The min working threads.</param>
        /// <param name="minCompletionPortThreads">The min completion port threads.</param>
        /// <returns></returns>
        public static bool ResetThreadPool(int? maxWorkingThreads, int? maxCompletionPortThreads,
            int? minWorkingThreads, int? minCompletionPortThreads)
        {
            if (maxWorkingThreads.HasValue || maxCompletionPortThreads.HasValue)
            {
                int oldMaxWorkingThreads, oldMaxCompletionPortThreads;

                ThreadPool.GetMaxThreads(out oldMaxWorkingThreads, out oldMaxCompletionPortThreads);

                if (!maxWorkingThreads.HasValue)
                {
                    maxWorkingThreads = oldMaxWorkingThreads;
                }

                if (!maxCompletionPortThreads.HasValue)
                {
                    maxCompletionPortThreads = oldMaxCompletionPortThreads;
                }

                if (maxWorkingThreads.Value != oldMaxWorkingThreads || maxCompletionPortThreads.Value != oldMaxCompletionPortThreads)
                {
                    if (!ThreadPool.SetMaxThreads(maxWorkingThreads.Value, maxCompletionPortThreads.Value))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}