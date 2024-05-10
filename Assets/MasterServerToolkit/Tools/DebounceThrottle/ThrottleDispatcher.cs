using System;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.DebounceThrottle
{
    /// <summary>
    /// The Throttle dispatcher, on the other hand, limits the invocation of an action to a specific time interval. This means that the action will only be executed once within the given time frame, regardless of how many times it is called.
    /// </summary>
    public class ThrottleDispatcher : ThrottleDispatcher<bool>
    {
        public ThrottleDispatcher(
            int interval,
            bool delayAfterExecution = false,
            bool resetIntervalOnException = false)
            : base(interval, delayAfterExecution, resetIntervalOnException)
        {
        }

        /// <summary>
        /// ThrottleAsync method manages the throttling of the action invocation.
        /// </summary>
        /// <param name="function">The function returns the Task to be invoked asynchronously.</param>
        /// <param name="cancellationToken">An optional CancellationToken.</param>
        /// <returns></returns>
        public Task ThrottleAsync(Func<Task> function, CancellationToken cancellationToken = default)
        {
            return base.ThrottleAsync(async () =>
            {
                await function.Invoke();
                return true;
            }, cancellationToken);
        }

        /// <summary>
        /// Throttle method manages the throttling of the action invocation in a synchronous manner.
        /// </summary>
        /// <param name="action">The action to be invoked.</param>
        /// <param name="cancellationToken">An optional CancellationToken.</param>
        public void Throttle(Action action, CancellationToken cancellationToken = default)
        {
            Func<Task<bool>> actionAsync = () => Task.Run(() =>
            {
                action.Invoke();
                return true;
            }, cancellationToken);

            Task _ = ThrottleAsync(actionAsync, cancellationToken);
        }
    }
}