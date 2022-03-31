using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Utils
{
    /// <summary>
    /// The default IClock implementation that uses the system clock.
    /// </summary>
    public class Clock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public async Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken = default)
        {
            if (timeSpan <= TimeSpan.Zero)
            {
                await Task.Yield();
            }
            else
            {
                await Task.Delay(timeSpan, cancellationToken);
            }
        }

        public Task Defer(TimeSpan timeSpan, string? name = null, CancellationToken cancellationToken = default)
        {
            return Delay(timeSpan, cancellationToken);
        }

        public void CancelTokenAfter(CancellationTokenSource tokenSource, TimeSpan timeSpan, string? name = null)
        {
            if (timeSpan < TimeSpan.Zero)
            {
                tokenSource.Cancel();
            }
            else
            {
                tokenSource.CancelAfter(timeSpan);
            }
        }
    }
}