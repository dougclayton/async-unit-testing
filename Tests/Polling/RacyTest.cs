using System;
using System.Threading.Tasks;
using Xunit;

namespace Tests.Polling
{
    public class RacyTest
    {
        [Fact]
        public async Task Get_UpdatedInBackground()
        {
            var value = "value1";

            // what if this setting is buried too deep to override? 
            var delay = TimeSpan.FromMilliseconds(100);
            
            var lazy = new AsyncLazy<string>(
                () => Task.FromResult(value),
                delay);
            
            Assert.Equal("value1", await lazy.Get());

            value = "value2";
            // is this long enough to wait for a background update???
            await Task.Delay(1000);

            Assert.Equal("value2", await lazy.Get());
        }
    }
}