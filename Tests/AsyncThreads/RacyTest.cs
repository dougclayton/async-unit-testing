using System.Threading.Tasks;
using Xunit;

namespace Tests.AsyncThreads
{
    public class RacyTest
    {
        [Fact]
        public async Task MutualExclusion_Ensured()
        {
            var runCount = 0;
            
            var lazy = new AsyncLazy<string>(async () =>
            {
                runCount++;
                // how long to wait to make sure the test runs before we exit???
                await Task.Delay(1000);
                return $"run{runCount}";
            });
            
            var firstGetTask = lazy.Get();
            // how long to wait to make sure the real class gets inside our lambda???
            await Task.Delay(100);
            Assert.Equal(1, runCount);

            var secondGetTask = lazy.Get();
            // how long to wait to make sure the second Get does not run???
            await Task.Delay(100);

            Assert.Equal("run1", await firstGetTask);
            Assert.Equal("run1", await secondGetTask);
            
            Assert.Equal(1, runCount);
        }
    }
}