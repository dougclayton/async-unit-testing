using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Utils
{
    /// <summary>
    /// A clock that reports a single time until changed.
    ///
    /// This class is intended for ensuring that code waits for actual time to elapse, because every Delay waits
    /// indefinitely to be released. This makes it most useful for directly testing classes that interact
    /// with time. For more indirect uses of time in unit tests, such as  the clock to use in "integration" tests,
    /// <see cref="InstantClock"/> will likely provide a better experience.<br/>
    ///
    /// The call to Delay blocks, and your test has to wait for your code to call Delay to assert what it was called with,
    /// as well as to advance time. To see why, consider how you might write it if Delay was released by advancing time:
    /// <code>
    /// public static async Task RealMethodBeingTested(IClock clock, TaskCompletionSource asyncCallCompleted)
    /// {
    ///     await SomeRemoteService();
    ///     asyncCallCompleted.TrySetResult();
    ///     await clock.Delay(TimeSpan.FromMinutes(1));
    /// }
    ///
    /// [Fact]
    /// public async Task RealMethodBeingTested_ShouldBlock()
    /// {
    ///     var clock = new StoppedClock(T0);
    ///     var tcs = new TaskCompletionSource();
    ///     Task.Run(() => RealMethodBeingTested(clock, tcs));
    ///     await tcs.Task;
    ///     clock.AdvanceTime(TimeSpan.FromMinutes(1));
    ///     // do some tests about the SomeRemoteService call
    ///     ...
    /// }
    /// </code>
    /// This test has a race condition: if Delay() happens before AdvanceTime() happens, then AdvanceTime()
    /// will update time to T0+1m and release Delay() properly; however, if AdvanceTime() happens first, it will advance
    /// time to T0+1m but by the time Delay() runs, it will wait indefinitely for the test to advance another minute beyond T0+1m.
    /// The only way to avoid this is to make Delay() itself be a gate that can be waited on,
    /// so the test releases the gate and advances time atomically. That would look like this:<br/>
    ///
    /// <code>
    /// public static async Task RealMethodBeingTested(IClock clock)
    /// {
    ///     await SomeRemoteService();
    ///     await clock.Delay(TimeSpan.FromMinutes(1));
    /// }
    ///
    /// [Fact]
    /// public async Task RealMethodBeingTested_ShouldBlock()
    /// {
    ///     var clock = new StoppedClock(T0);
    ///     Task.Run(() => RealMethodBeingTested(clock));
    ///     var delay = await clock.WaitForDelay();
    ///     Assert.Equal(TimeSpan.FromMinutes(1), delay);
    ///     // do some tests about the SomeRemoteService call
    ///     ...
    ///     clock.ReleaseDelay();
    /// }
    /// </code>
    /// This code does not have a race condition: the Delay method sees that time is T0 when it enters,
    /// and it waits to be released. The WaitForDelay method waits for Delay to be called.
    /// Then, when Release is called, it advances time by the input to Delay (ie, 1 minute) and Delay() exits.<br/>
    ///
    /// Defer also blocks in this class, but it requires a name.
    /// </summary>
    public class StoppedClock : FakeClockBase, IClock
    {
        private ClockWaiter _delayClockWaiter = new();

        private readonly object _lock = new();

        public StoppedClock(DateTime? now = null) : base(now)
        {
        }

        public void AdvanceTime(TimeSpan timeSpan)
        {
            SetTime(UtcNow + timeSpan);
        }

        public void SetTime(DateTime now)
        {
            SetCurrentTime(now);
        }

        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return _delayClockWaiter.Enter(CurrentTime, timeSpan, "Already waiting in Delay", cancellationToken);
            }
        }

        /// <summary>
        /// Waits for the Delay method to be called and returns the timespan it was called with
        /// </summary>
        public Task<TimeSpan> WaitForDelay()
        {
            lock (_lock)
            {
                return _delayClockWaiter.Wait();
            }
        }

        /// <summary>
        /// Releases the Delay() and advances time to match the Delay. If advance is given, time is advanced by that instead.
        /// </summary>
        public void ReleaseDelay(TimeSpan? advance = null)
        {
            lock (_lock)
            {
                _delayClockWaiter.ReleaseIfEntered("Delay has not been reached yet");
                if (advance != null)
                {
                    SetTimeForwardOnly(CurrentTime + advance.Value);
                }

                var releaseTime = _delayClockWaiter.ReleaseTime;
                _delayClockWaiter = new ClockWaiter();
                SetTimeForwardOnly(releaseTime);
            }
        }

        public async Task<TimeSpan> WaitForDelayAndRelease(TimeSpan? advance = null)
        {
            var timeSpan = await WaitForDelay();
            ReleaseDelay(advance);
            return timeSpan;
        }
    }
}
