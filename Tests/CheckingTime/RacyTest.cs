using System;
using System.Threading.Tasks;
using Xunit;

namespace Tests.CheckingTime
{
    public class RacyTest
    {
        [Fact]
        public async Task Get_Expired()
        {
            var runCount = 0;
            
            var lazy = new AsyncLazy<string>(
                () => Task.FromResult($"run{++runCount}"),
                TimeSpan.FromMilliseconds(500));
            
            Assert.Equal("run1", await lazy.Get());
            
            // will this second Get execute fast enough after the first???
            Assert.Equal("run1", await lazy.Get());

            // is this long enough to wait for the expiration???
            await Task.Delay(1000);
            Assert.Equal("run2", await lazy.Get());
        }
    }
}
