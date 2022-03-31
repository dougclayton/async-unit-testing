using System;
using System.Threading.Tasks;
using Tests.Utils;
using Xunit;

namespace Tests.Polling
{
    public class ReliableTest
    {
        [Fact]
        public async Task Get_UpdatedInBackground()
        {
            var value = "value1";
            var runCount = 0;
            var gate = new AsyncGate();

            // this test doesn't care what the actual delay is!
            var delay = TimeSpan.FromHours(1);
            
            var lazy = new AsyncLazy<string>(
                async () =>
                {
                    if (++runCount >= 2) await gate.ReachAndWait();
                    return value;
                },
                delay, new InstantClock());

            Assert.Equal("value1", await lazy.Get());

            // wait for the update loop to hit our code so we know it is seeing our new value 
            await gate.WaitToBeReached();
            value = "value2";
            gate.OpenAndShut();
            // race condition warning! just because the gate is open, does not mean AsyncLazy
            // has gotten the value from the callback, nor even that the code has gone through the gate yet.
            // so we wait for the loop to run again.
            await gate.WaitToBeReached();
            
            Assert.Equal("value2", await lazy.Get());
        }
    }
}
