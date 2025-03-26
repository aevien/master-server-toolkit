using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.DebounceThrottle
{
    /// <summary>
    /// The Throttle dispatcher limits the invocation of an action to a specific time interval. 
    /// This means that the action will only be executed once within the given time frame, 
    /// regardless of how many times it is called.
    /// </summary>
    /// <typeparam name="T">Return Type of the executed tasks</typeparam>
    public class ThrottleDispatcher<T> : IDisposable
    {
        private readonly int _interval;
        private readonly bool _delayAfterExecution;
        private readonly bool _resetIntervalOnException;
        private readonly object _locker = new object();
        private Task<T> _lastTask;
        private DateTime? _invokeTime;
        private bool _busy;
        private CancellationTokenSource _linkedCts;

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
        /// Throttling of the function invocation with safe cancellation support
        /// </summary>
        /// <param name="function">The function that accepts a cancellation token and returns a Task to be invoked asynchronously.</param>
        /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete</param>
        /// <returns>Returns the last executed Task</returns>
        public Task<T> ThrottleAsync(Func<CancellationToken, Task<T>> function, CancellationToken cancellationToken = default)
        {
            lock (_locker)
            {
                // Dispose previous linked token source if exists
                _linkedCts?.Dispose();

                // Create a new linked token source that will be cancelled if either the provided token
                // or our internal token is cancelled
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                DateTime now = DateTime.UtcNow;

                if (_lastTask != null && (_busy || ShouldWait()))
                {
                    return _lastTask;
                }

                _busy = true;
                _invokeTime = DateTime.UtcNow;

                try
                {
                    // Check for cancellation before invoking the function
                    _linkedCts.Token.ThrowIfCancellationRequested();

                    // Pass the cancellation token to the function
                    _lastTask = function.Invoke(_linkedCts.Token);

                    // Set up continuation to update state after task completes
                    _lastTask.ContinueWith(task =>
                    {
                        lock (_locker)
                        {
                            if (_delayAfterExecution)
                            {
                                _invokeTime = DateTime.UtcNow;
                            }
                            _busy = false;
                        }
                    }, _linkedCts.Token, TaskContinuationOptions.NotOnCanceled, TaskScheduler.Default);

                    // Handle task faulting if requested
                    if (_resetIntervalOnException)
                    {
                        _lastTask.ContinueWith(task =>
                        {
                            lock (_locker)
                            {
                                _lastTask = null;
                                _invokeTime = null;
                            }
                        }, _linkedCts.Token, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                    }

                    // Handle task cancellation
                    _lastTask.ContinueWith(task =>
                    {
                        lock (_locker)
                        {
                            if (_resetIntervalOnException)
                            {
                                _lastTask = null;
                                _invokeTime = null;
                            }
                        }
                    }, _linkedCts.Token, TaskContinuationOptions.OnlyOnCanceled, TaskScheduler.Default);

                    return _lastTask;
                }
                catch (OperationCanceledException)
                {
                    // Reset state on cancellation
                    _busy = false;

                    // Propagate the cancellation
                    throw;
                }
                catch (Exception)
                {
                    // Reset state on other exceptions
                    _busy = false;

                    // Re-throw other exceptions
                    throw;
                }
            }
        }

        /// <summary>
        /// An overload that accepts a function without cancellation token parameter for backward compatibility
        /// </summary>
        public Task<T> ThrottleAsync(Func<Task<T>> function, CancellationToken cancellationToken = default)
        {
            // Wrap the function to accept a cancellation token but not use it
            return ThrottleAsync(ct => function(), cancellationToken);
        }

        /// <summary>
        /// Cancels any pending throttled operation
        /// </summary>
        public void Cancel()
        {
            lock (_locker)
            {
                _linkedCts?.Cancel();
            }
        }

        /// <summary>
        /// Releases all resources used by the dispatcher
        /// </summary>
        public void Dispose()
        {
            lock (_locker)
            {
                _linkedCts?.Dispose();
                _linkedCts = null;
            }
        }
    }
}