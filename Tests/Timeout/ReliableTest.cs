using System;
using System.Threading.Tasks;
using Tests.Utils;
using Xunit;

namespace Tests.Timeout
{
    public class ReliableTest
    {
        [Fact]
        public async Task Get_AutoExpires()
        {
            var runCount = 0;
            var gate = new AsyncGate();
            var clock = new InstantClock();
            
            // this test doesn't care what the actual delay is!
            var delay = TimeSpan.FromHours(1);

            var lazy = new AsyncLazy<string>(
                () => Task.FromResult($"run{++runCount}"),
                _ => { gate.ReachAndWait(); },
                delay, clock);
            
            Assert.Equal("run1", await lazy.Get());
            
            // nothing is disposed until we say so
            await gate.EnsureGateNotReached();

            clock.ReleaseDefer();
            
            // this doesn't wait for real time
            await gate.WaitToBeReached();

            Assert.Equal("run2", await lazy.Get());
        }
    }
}