using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.DebounceThrottle
{
    /// <summary>
    /// The Throttle dispatcher, on the other hand, limits the invocation of an action to a specific time interval. This means that the action will only be executed once within the given time frame, regardless of how many times it is called.
    /// </summary>
    /// <typeparam name="T">Return Type of the executed tasks</typeparam>
    public class ThrottleDispatcher<T>
    {
        private readonly int _interval;
        private readonly bool _delayAfterExecution;
        private readonly bool _resetIntervalOnException;
        private readonly object _locker = new object();
        private Task<T> _lastTask;
        private DateTime? _invokeTime;
        private bool _busy;

        /// <summary>
        /// ThrottleDispatcher is a utility class for throttling the execution of asynchronous tasks.
        /// It limits the rate at which a function can be invoked based on a specified interval.
        /// </summary>
        /// <param name="interval">The minimum interval in milliseconds between invocations of the throttled function.</param>
        /// <param name="delayAfterExecution">If true, the interval is calculated from the end of the previous task execution, otherwise from the start.</param>
        /// <param name="resetIntervalOnException">If true, the interval is reset when an exception occurs during the execution of the throttled function.</param>
        public ThrottleDispatcher(
            int interval,
            bool delayAfterExecution = false,
            bool resetIntervalOnException = false)
        {
            _interval = interval;
            _delayAfterExecution = delayAfterExecution;
            _resetIntervalOnException = resetIntervalOnException;
        }

        private bool ShouldWait()
        {
            return _invokeTime.HasValue &&
                (DateTime.UtcNow - _invokeTime.Value).TotalMilliseconds < _interval;
        }

        /// <summary>
        /// Throttling of the function invocation
        /// </summary>
        /// <param name="function">The function returns Task to be invoked asynchronously.</param>
        /// <param name="cancellationToken">An optional CancellationToken</param>
        /// <returns>Returns a last executed Task</returns>
        public Task<T> ThrottleAsync(Func<Task<T>> function, CancellationToken cancellationToken = default)
        {
            lock (_locker)
            {
                DateTime now = DateTime.UtcNow;

                if (_lastTask != null && (_busy || ShouldWait()))
                {
                    return _lastTask;
                }

                _busy = true;
                _invokeTime = DateTime.UtcNow;

                _lastTask = function.Invoke();

                _lastTask.ContinueWith(task =>
                {
                    if (_delayAfterExecution)
                    {
                        _invokeTime = DateTime.UtcNow;
                    }
                    _busy = false;
                }, cancellationToken);

                if (_resetIntervalOnException)
                {
                    _lastTask.ContinueWith((task, obj) =>
                    {
                        _lastTask = null;
                        _invokeTime = null;
                    }, cancellationToken, TaskContinuationOptions.OnlyOnFaulted);
                }

                return _lastTask;
            }
        }
    }
}
