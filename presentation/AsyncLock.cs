using System;
using System.Threading;
using System.Threading.Tasks;

namespace RearchitectTowardsAsyncAwait
{
    /// <summary>
    /// Inspired by http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266988.aspx
    /// </summary>
    class AsyncLock
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);
        private readonly Task<Releaser> cachedReleaser;

        public AsyncLock()
        {
            cachedReleaser = Task.FromResult(new Releaser(this));
        }

        public Task<Releaser> LockAsync()
        {
            return LockAsync(CancellationToken.None);
        }

        public Task<Releaser> LockAsync(CancellationToken cancellationToken)
        {
            var wait = semaphore.WaitAsync(cancellationToken);
            return wait.IsCompleted ?
                cachedReleaser :
                wait.ContinueWith((_, state) => new Releaser((AsyncLock)state),
                    this, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public struct Releaser : IDisposable
        {
            private readonly AsyncLock asyncLock;

            public Releaser(AsyncLock asyncLock)
            {
                this.asyncLock = asyncLock;
            }

            public void Dispose()
            {
                asyncLock?.semaphore.Release();
            }
        }
    }
}