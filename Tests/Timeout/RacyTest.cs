using System;
using System.Threading.Tasks;
using Xunit;

namespace Tests.Timeout
{
    public class RacyTest
    {
        [Fact]
        public async Task Get_AutoExpires()
        {
            var runCount = 0;
            var disposed = false;
            
            // what if this setting is buried too deep to override? 
            var delay = TimeSpan.FromMilliseconds(100);
            
            var lazy = new AsyncLazy<string>(
                () => Task.FromResult($"run{++runCount}"),
                _ => { disposed = true; },
                delay);
            
            Assert.Equal("run1", await lazy.Get());
            
            // will this execute fast enough after the Get()???
            Assert.False(disposed);
            
            // is this long enough to wait for the disposer to run???
            await Task.Delay(1000);
            Assert.True(disposed);
        }
    }
}