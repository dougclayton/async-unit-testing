using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Utils
{
    public class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore = new(1);

        public async Task<IDisposable> WaitAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new ReleaseLockDisposable(_semaphore);
        }

        private class ReleaseLockDisposable : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private int _isDisposed;

            public ReleaseLockDisposable(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _isDisposed, 0, 1) == 0)
                {
                    _semaphore.Release();
                }
            }
        }
    }
}