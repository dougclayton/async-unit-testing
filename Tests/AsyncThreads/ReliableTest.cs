using System.Threading.Tasks;
using Tests.Utils;
using Xunit;

namespace Tests.AsyncThreads
{
    public class ReliableTest
    {
        private class Gate
        {
            public TaskCompletionSource Entered { get; } = new TaskCompletionSource();
            public TaskCompletionSource Exit { get; } = new TaskCompletionSource();
        }
            
        [Fact]
        public async Task MutualExclusion_Ensured()
        {
            var runCount = 0;
            Gate gate1 = new Gate(), gate2 = new Gate();

            var lazy = new AsyncLazy<string>(async () =>
            {
                var gate = (++runCount == 1) ? gate1 : gate2; 
                // wait as long as needed for our test
                gate.Entered.TrySetResult();
                await gate.Exit.Task.WaitBounded();
                
                return $"run{runCount}";
            });
            
            var firstGetTask = lazy.Get();
            // wait as long as needed for our lambda to be called
            await gate1.Entered.Task;
            Assert.Equal(1, runCount);

            var secondGetTask = lazy.Get();
            await Task.Delay(100);
            Assert.False(gate2.Entered.Task.IsCompleted);

            gate1.Exit.TrySetResult();
            Assert.Equal("run1", await firstGetTask);
            gate2.Exit.TrySetResult();
            Assert.Equal("run1", await secondGetTask);
            
            Assert.Equal(1, runCount);
        }
    }
}