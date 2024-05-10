using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.DebounceThrottle
{
    /// <summary>
    /// The Debounce dispatcher delays the invocation of an action until a predetermined interval has elapsed since the last call. This ensures that the action is only invoked once after the calls have stopped for the specified duration.
    /// </summary>
    /// <typeparam name="T">Type of the debouncing Task</typeparam>
    public class DebounceDispatcher<T>
    {
        private Task<T> _waitingTask;
        private Func<Task<T>> _functToInvoke;
        private object _locker = new object();
        private DateTime _lastInvokeTime;
        private readonly int _interval;

        /// <summary>
        /// Debouncing the execution of asynchronous tasks.
        /// It ensures that a function is invoked only once within a specified interval, even if multiple invocations are requested.
        /// </summary>
        /// <param name="interval">The minimum interval in milliseconds between invocations of the debounced function.</param>
        public DebounceDispatcher(int interval)
        {
            _interval = interval;
        }

        /// <summary>
        /// DebounceAsync method manages the debouncing of the function invocation.
        /// </summary>
        /// <param name="function">The function returns Task to be invoked asynchronously</param>
        /// <param name="cancellationToken">An optional CancellationToken</param>
        /// <returns>Returns Task to be executed with minimal delay</returns>
        public Task<T> DebounceAsync(Func<Task<T>> function, CancellationToken cancellationToken = default)
        {
            lock (_locker)
            {
                _functToInvoke = function;
                _lastInvokeTime = DateTime.UtcNow;

                if (_waitingTask != null)
                {
                    return _waitingTask;
                }

                _waitingTask = Task.Run(async () =>
                {
                    do
                    {
                        double delay = _interval - (DateTime.UtcNow - _lastInvokeTime).TotalMilliseconds;
                        await Task.Delay((int)(delay < 0 ? 0 : delay), cancellationToken);
                    }
                    while (DelayContidion());

                    T res;
                    try
                    {
                        res = await _functToInvoke.Invoke();
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    finally
                    {
                        lock (_locker)
                        {
                            _waitingTask = null;
                        }
                    }
                    return res;

                }, cancellationToken);

                return _waitingTask;
            }
        }

        private bool DelayContidion()
        {
            return (DateTime.UtcNow - _lastInvokeTime).TotalMilliseconds < _interval;
        }
    }
}