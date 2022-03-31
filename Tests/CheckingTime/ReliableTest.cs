using System;
using System.Threading.Tasks;
using Tests.Utils;
using Xunit;

namespace Tests.CheckingTime
{
    public class ReliableTest
    {
        [Fact]
        public async Task Get_Expired()
        {
            var runCount = 0;

            var clock = new StoppedClock();
            var lazy = new AsyncLazy<string>(
                () => Task.FromResult($"run{++runCount}"),
                TimeSpan.FromMilliseconds(500), clock);
            
            Assert.Equal("run1", await lazy.Get());
            
            // no fake time passes between the two Get calls
            Assert.Equal("run1", await lazy.Get());

            // not even 499 ms is enough...
            clock.AdvanceTime(TimeSpan.FromMilliseconds(499));
            Assert.Equal("run1", await lazy.Get());
            // ...have to advance to 500 ms
            clock.AdvanceTime(TimeSpan.FromMilliseconds(1));
            Assert.Equal("run2", await lazy.Get());
        }
    }
}