using System;
using System.Threading.Tasks;
using Tests.Utils;

namespace Tests.Timeout
{
    // Constructs an item asynchronously and automatically disposes it after an expiration period
    public class AsyncLazy<T> where T : class
    {
        private readonly Func<Task<T>> _creator;
        private readonly Action<T> _disposer;
        private readonly AsyncLock _lock = new();
        private readonly TimeSpan _expiration;
        private readonly IClock _clock;
        private T? _item;
        
        public AsyncLazy(Func<Task<T>> creator, Action<T> disposer, TimeSpan expiration, IClock? clock = null)
        {
            _creator = creator;
            _expiration = expiration;
            _disposer = disposer;
            _clock = clock ?? new Clock();
        }

        public async Task<T> Get()
        {
            using (await _lock.WaitAsync())
            {
                if (_item == null)
                {
                    _item = await _creator();
                    _ = Expire();
                }

                return _item;
            }
        }

        private async Task Expire()
        {
            await _clock.Defer(_expiration);
            
            using (await _lock.WaitAsync())
            {
                if (_item != null)
                {
                    _disposer(_item);
                    _item = null;
                }
            }
        }
    }
}