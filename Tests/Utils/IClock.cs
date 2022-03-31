using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Utils
{
    /// <summary>
    /// A simple interface to allow unit testing code that involves the passage of time. It is expected there will be
    /// three main modes of this interface: a real clock, that just uses system time (<see cref="Clock"/>),
    /// a clock for which Delay does not wait at all, and a clock for which Delay blocks until manually released.
    /// </summary>
    /// <remarks>Note: implementations of this class must be thread-safe.</remarks>
    public interface IClock
    {
        /// <summary>
        /// A replacement for DateTime.UtcNow
        /// </summary>
        public DateTime UtcNow { get; }

        /// <summary>
        /// In real code, this sleeps like Task.Delay(timeSpan), but in unit tests, this sleeps like Task.Yield()/Task.Delay(0),
        /// or blocks until explicitly released.<br/>
        ///
        /// This is intended for when code doesn't really want to wait, but is either emulating a process that takes
        /// finite time or just does not want to busy-wait and consume too much CPU. This method should be used for
        /// emulating a long-running process, as the delay in a polling loop, or in code that is explicitly based on time.
        /// </summary>
        /// <remarks>If timeSpan is negative, it is treated like zero: no delay. This is different than Task.Delay!
        /// It is easy to forget to clamp a computed timespan to 0, and that makes Task.Delay block indefinitely.</remarks>
        public Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken = default);

        /// <summary>
        /// In real code, this sleeps like Task.Delay(timeSpan), but in unit tests, this blocks until explicitly released.<br/>
        ///
        /// This is intended for when code wants to run some kind of cancel/cleanup/abort/timeout behavior after "too much" time
        /// has passed. When testing, this always blocks. We cannot implement this with Task.Yield(),
        /// because cleanup would happen immediately. We also cannot implement this with some real Task.Delay(),
        /// because some tests will need to trigger it explicitly, and do not want to wait for the actual time given to elapse.
        /// Besides, using system time is a race condition anyhow. Therefore, this method takes an optional name that can be
        /// used to "release" a task blocked on a certain Defer, as needed.
        /// </summary>
        /// <seealso cref="CancelTokenAfter"/>
        public Task Defer(TimeSpan timeSpan, string? name = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Like <see cref="Defer"/>, but defers the token to be canceled after the given time.
        /// </summary>
        public void CancelTokenAfter(CancellationTokenSource tokenSource, TimeSpan timeSpan, string? name = null);
    }
}
