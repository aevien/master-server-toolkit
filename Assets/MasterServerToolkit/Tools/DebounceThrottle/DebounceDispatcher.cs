using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.DebounceThrottle
{
    /// <summary>
    /// The Debounce dispatcher delays the invocation of an action until a predetermined interval has elapsed since the last call. This ensures that the action is only invoked once after the calls have stopped for the specified duration.
    /// </summary>
    public class DebounceDispatcher : DebounceDispatcher<bool>
    {
        /// <summary>
        /// Debouncing the execution of asynchronous tasks.
        /// It ensures that a function is invoked only once within a specified interval, even if multiple invocations are requested.
        /// </summary>
        /// <param name="interval">The minimum interval in milliseconds between invocations of the debounced function.</param>
        public DebounceDispatcher(int interval) : base(interval)
        {
        }

        /// <summary>
        /// Method manages the debouncing of the function invocation.
        /// </summary>
        /// <param name="function">The function returns Task to be invoked asynchronously</param>
        /// <param name="cancellationToken">An optional CancellationToken</param>
        /// <returns>Returns Task to be executed with minimal delay</returns>
        public Task DebounceAsync(Func<Task> function, CancellationToken cancellationToken = default)
        {
            return base.DebounceAsync(async () =>
            {
                await function.Invoke();
                return true;
            }, cancellationToken);
        }

        /// <summary>
        /// Method manages the debouncing of the function invocation.
        /// </summary>
        /// <param name="action">The action to be invoked</param>
        /// <param name="cancellationToken">An optional CancellationToken</param>
        public void Debounce(Action action, CancellationToken cancellationToken = default)
        {
            Func<Task<bool>> actionAsync = () => Task.Run(() =>
            {
                action.Invoke();
                return true;
            }, cancellationToken);

            Task _ = DebounceAsync(actionAsync, cancellationToken);
        }
    }
}