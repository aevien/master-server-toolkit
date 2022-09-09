using MasterServerToolkit.Networking;
using System;
using System.Threading;

namespace MasterServerToolkit.MasterServer
{
    public class MstConcurrency
    {
        /// <summary>
        /// Runs method in main thread
        /// </summary>
        /// <param name="action"></param>
        public void RunInMainThread(Action action)
        {
            MstTimer.RunInMainThread(action);
        }

        /// <summary>
        /// Sets the method that is to be executed on the separate thread
        /// </summary>
        /// <param name="action">The method that is to be called on the newly created thread</param>
        private void RunInThreadPool(WaitCallback action)
        {
            ThreadPool.QueueUserWorkItem(action);
        }

        /// <summary>
        /// Used to run a method / expression on a separate thread
        /// </summary>
        /// <param name="action">The method to be run on the separate thread</param>
        /// <param name="delayOrSleep">The amount of time to wait before running the expression on the newly created thread</param>
        /// <returns></returns>
        public void RunInThreadPool(Action action, int delayOrSleep = 0)
        {
            // Wrap the expression in a method so that we can apply the delayOrSleep before and remove the task after it finishes
            WaitCallback callback = (state) =>
            {
                // Apply the specified delay
                if (delayOrSleep > 0)
                    Thread.Sleep(delayOrSleep);

                // Call the requested method
                action.Invoke();
            };

            // Set the method to be called on the separate thread to be the inline method we have just created
            RunInThreadPool(callback);
        }
    }
}