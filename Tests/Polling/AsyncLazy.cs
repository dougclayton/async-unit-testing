using System;
using System.Threading.Tasks;
using Tests.Utils;

namespace Tests.Polling
{
    // Constructs an item asynchronously and automatically updates it in the background
    public class AsyncLazy<T> where T : class
    {
        private readonly Func<Task<T>> _creator;
        private readonly AsyncLock _lock = new();
        private readonly TimeSpan _expiration;
        private readonly IClock _clock;
        private T? _item;
        
        public AsyncLazy(Func<Task<T>> creator, TimeSpan expiration, IClock? clock = null)
        {
            _creator = creator;
            _expiration = expiration;
            _clock = clock ?? new Clock();
        }

        public async Task<T> Get()
        {
            using (await _lock.WaitAsync())
            {
                if (_item == null)
                {
                    _item = await _creator();
                    _ = Poll();
                }

                return _item;
            }
        }

        private async Task Poll()
        {
            while (true)
            {
                await _clock.Delay(_expiration);

                var item = await _creator();
                using (await _lock.WaitAsync())
                {
                    _item = item;
                }
            }
        }
    }
}