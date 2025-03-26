using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.DebounceThrottle
{
    /// <summary>
    /// The Debounce dispatcher delays the invocation of an action until a predetermined interval has elapsed since the last call.
    /// This ensures that the action is only invoked once after the calls have stopped for the specified duration.
    /// </summary>
    /// <typeparam name="T">Type of the debouncing Task</typeparam>
    public class DebounceDispatcher<T>
    {
        private Task<T> _waitingTask;
        private Func<CancellationToken, Task<T>> _functToInvoke;
        private readonly object _locker = new object();
        private DateTime _lastInvokeTime;
        private readonly int _interval;
        private CancellationTokenSource _linkedCts;

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
        /// DebounceAsync method manages the debouncing of the function invocation with safe cancellation support.
        /// </summary>
        /// <param name="function">The function that returns Task to be invoked asynchronously</param>
        /// <param name="cancellationToken">A CancellationToken to observe while waiting for the task to complete</param>
        /// <returns>Returns Task to be executed with minimal delay</returns>
        public Task<T> DebounceAsync(Func<CancellationToken, Task<T>> function, CancellationToken cancellationToken = default)
        {
            lock (_locker)
            {
                // Dispose previous linked token source if exists
                _linkedCts?.Dispose();

                // Create a new linked token source that will be cancelled if either the provided token
                // or our internal token is cancelled
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                _functToInvoke = function;
                _lastInvokeTime = DateTime.UtcNow;

                if (_waitingTask != null)
                {
                    return _waitingTask;
                }

                _waitingTask = Task.Run(async () =>
                {
                    try
                    {
                        do
                        {
                            // First check if cancellation was requested
                            _linkedCts.Token.ThrowIfCancellationRequested();

                            double delay = _interval - (DateTime.UtcNow - _lastInvokeTime).TotalMilliseconds;
                            int delayMs = (int)(delay < 0 ? 0 : delay);

                            // Use try-catch inside the loop to properly handle cancellation during delay
                            try
                            {
                                await Task.Delay(delayMs, _linkedCts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                // Propagate cancellation up
                                _linkedCts.Token.ThrowIfCancellationRequested();
                            }
                        }
                        while (DelayCondition());

                        // Check for cancellation before invoking the function
                        _linkedCts.Token.ThrowIfCancellationRequested();

                        // Pass the cancellation token to the invoked function
                        return await _functToInvoke.Invoke(_linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Properly propagate cancellation
                        throw;
                    }
                    catch (Exception)
                    {
                        // Re-throw other exceptions
                        throw;
                    }
                    finally
                    {
                        lock (_locker)
                        {
                            _waitingTask = null;

                            // Dispose linked CTS in finally block to ensure cleanup
                            if (_linkedCts != null)
                            {
                                _linkedCts.Dispose();
                                _linkedCts = null;
                            }
                        }
                    }
                }, _linkedCts.Token);

                return _waitingTask;
            }
        }

        /// <summary>
        /// An overload that accepts a function without cancellation token parameter
        /// </summary>
        public Task<T> DebounceAsync(Func<Task<T>> function, CancellationToken cancellationToken = default)
        {
            // Wrap the function to accept a cancellation token but not use it
            return DebounceAsync(ct => function(), cancellationToken);
        }

        private bool DelayCondition()
        {
            return (DateTime.UtcNow - _lastInvokeTime).TotalMilliseconds < _interval;
        }

        /// <summary>
        /// Cancels any pending debounced operation
        /// </summary>
        public void Cancel()
        {
            lock (_locker)
            {
                _linkedCts?.Cancel();
            }
        }

        /// <summary>
        /// Disposes resources used by the dispatcher
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