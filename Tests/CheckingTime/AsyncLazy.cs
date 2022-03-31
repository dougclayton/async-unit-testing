using System;
using System.Threading.Tasks;
using Tests.Utils;

namespace Tests.CheckingTime
{
    // Constructs an item asynchronously but gets a new one if it is too old
    public class AsyncLazy<T> where T : class
    {
        private readonly Func<Task<T>> _creator;
        private readonly AsyncLock _lock = new();
        private readonly TimeSpan _expiration;
        private readonly IClock _clock;
        private T? _item;
        private DateTime _expiresAt;
        
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
                if (_item == null || _expiresAt <= _clock.UtcNow)
                {
                    _item = await _creator();
                    _expiresAt = _clock.UtcNow + _expiration;
                }

                return _item;
            }
        }
    }
}