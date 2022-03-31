using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Utils
{
    /// <summary>
    /// Provides the shared time and Defer handling for the stopped clock and the instant clock.
    /// </summary>
    public abstract class FakeClockBase
    {
        public static readonly DateTime DefaultTime = DateTime.Parse("2020-01-01T12:00:00Z");

        protected DateTime CurrentTime;

        // this class uses simple locking to make it thread-safe. It's not highly contended.
        private readonly object _lock = new();

        // the tasks waiting on Defer
        private readonly Dictionary<string, ClockWaiter> _deferredWaiters = new();

        protected FakeClockBase(DateTime? now = null)
        {
            CurrentTime = now ?? DefaultTime;
        }

        public DateTime UtcNow
        {
            get
            {
                lock (_lock)
                {
                    return CurrentTime;
                }
            }
        }

        public async Task Defer(TimeSpan timeSpan, string? name = null, CancellationToken cancellationToken = default)
        {
            if (name == null)
            {
                name = "test";
                // // wait forever
                // await new TaskCompletionSource().Task;
                // return;
            }

            ClockWaiter? waiter;

            lock (_lock)
            {
                if (!_deferredWaiters.TryGetValue(name, out waiter))
                {
                    waiter = new ClockWaiter();
                    _deferredWaiters[name] = waiter;
                }
            }

            try
            {
                await waiter.Enter(CurrentTime, timeSpan, $"Already waiting in Defer for '{name}'", cancellationToken);
            }
            finally
            {
                lock (_lock)
                {
                    _deferredWaiters.Remove(name, out _);
                }
            }
        }

        /// <summary>
        /// Defers the cancellation of the token until ReleaseDefer() is called for this name.
        /// </summary>
        public void CancelTokenAfter(CancellationTokenSource tokenSource, TimeSpan timeSpan, string? name = null)
        {
            // throw an exception immediately since this function does not return a Task
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            async void Cancel()
            {
                await Defer(timeSpan, name);
                tokenSource.Cancel();
            }

            Cancel();
        }

        public Task WaitForDefer()
        {
            var tasks = new List<Task>();
            lock (_lock)
            {
                foreach (var name in _deferredWaiters.Keys)
                {
                    if (!_deferredWaiters.TryGetValue(name, out var waiter))
                    {
                        // pre-register a waiter for this name
                        waiter = new ClockWaiter();
                        _deferredWaiters[name] = waiter;
                    }

                    tasks.Add(waiter.Wait());
                }
            }

            return Task.WhenAll(tasks);
        }

        public async Task WaitForDeferAndRelease()
        {
            await WaitForDefer();
            ReleaseDefer();
        }

        public void ReleaseDefer()
        {
            lock (_lock)
            {
                foreach (var name in _deferredWaiters.Keys)
                {
                    if (_deferredWaiters.Remove(name, out var waiter))
                    {
                        waiter.ReleaseIfEntered("Defer has not been reached yet");
                        SetTimeForwardOnly(waiter.ReleaseTime);
                    }
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                foreach (var waiter in _deferredWaiters.Values)
                {
                    waiter.Release();
                }

                _deferredWaiters.Clear();
                CurrentTime = DefaultTime;
            }
        }

        protected void SetCurrentTime(DateTime now)
        {
            lock (_lock)
            {
                CurrentTime = now;
            }
        }

        protected void SetTimeForwardOnly(DateTime now)
        {
            lock (_lock)
            {
                if (now > CurrentTime)
                {
                    CurrentTime = now;
                }
            }
        }

        protected class ClockWaiter
        {
            private readonly TaskCompletionSource<TimeSpan> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public DateTime ReleaseTime { get; private set; }

            public async Task Enter(DateTime now, TimeSpan timeSpan, string repeatError, CancellationToken cancellationToken = default)
            {
                if (_entered.Task.IsCompleted)
                {
                    throw new ArgumentException(repeatError);
                }

                ReleaseTime = now + timeSpan;
                _entered.SetResult(timeSpan);

                await _released.Task.WaitBounded();
            }

            public Task<TimeSpan> Wait()
            {
                return _entered.Task;
            }

            public void ReleaseIfEntered(string unreachedError)
            {
                if (!_entered.Task.IsCompleted)
                {
                    throw new ArgumentException(unreachedError);
                }

                _released.SetResult();
            }

            public void Release()
            {
                _released.SetResult();
            }
        }

    }
}
