using System;
using System.Threading.Tasks;
using Tests.Utils;

namespace Tests.AsyncThreads
{
    // Constructs an item asynchronously one time only, on demand
    public class AsyncLazy<T> where T : class
    {
        private readonly Func<Task<T>> _creator;
        private readonly AsyncLock _lock = new();
        private T? _item;
        
        public AsyncLazy(Func<Task<T>> creator) => _creator = creator;
        
        public async Task<T> Get()
        {
            using (await _lock.WaitAsync())
            {
                if (_item == null)
                {
                    _item = await _creator();
                }

                return _item;
            }
        }
    }
}