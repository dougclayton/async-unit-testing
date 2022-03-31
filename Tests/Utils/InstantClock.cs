using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Utils
{
    /// <summary>
    /// A fake clock that is always running: time advances immediately for each Delay call so it completes instantly.
    /// This is suitable for "whole system" testing, where the exact set of background tasks waiting in Delay is an implementation detail,
    /// so time advances in an automatic and arbitrary way and nothing needs to be released (except Defer, which is still for exceptional cases).
    /// <br/>
    /// This class is best for testing code that waits for time to elapse to simulate the real object/service, but
    /// which are not currently the direct subject of the actual test. In that case, any extra waiting is just wasted time,
    /// so it does not ever actually wait. If an independent object/service that takes time to compute a result
    /// cannot be stopped, then it is a race condition to assume you can catch it in a "busy" state, so Running mode
    /// makes the fake just skip the busy state as quickly as possible. If you want to assert that some code catches
    /// that service in a "busy" state, you must inject either a <see cref="StoppedClock"/> or some other synchronization point.
    /// <br/>
    /// This is suitable for "whole system" testing, where the exact set of background tasks waiting in Delay is an implementation detail,
    /// so time advances in an automatic and arbitrary way and nothing needs to be released (except Defer, which is still for exceptional cases).
    /// </summary>
    public class InstantClock : FakeClockBase, IClock
    {
        // used to perform an actual sleep instead of a yield every once in a while
        private const int YieldsPerSleep = 10;

        private int _yieldCount;

        public InstantClock(DateTime? now = null) : base(now)
        {
        }

        /// <summary>
        /// Returns after a short delay. This is usually Yield, but occasionally Delay to avoid burning CPU in long loops.
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <param name="cancellationToken"></param>
        public async Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken = default)
        {
            // note that the first time here, the return value will be one, so the sleep does not happen
            // until we have invoked this method YieldsPerSleep times.
            var sleep = Interlocked.Increment(ref _yieldCount) % YieldsPerSleep == 0;
            if (sleep)
            {
                // add a real sleep so that an infinite loop cannot saturate the CPU
                await Task.Delay(1, cancellationToken);
            }
            else
            {
                // yield so that the task completion is still done asynchronously, like a real clock
                await Task.Yield();
            }

            SetTimeForwardOnly(CurrentTime + timeSpan);
        }
    }
}
