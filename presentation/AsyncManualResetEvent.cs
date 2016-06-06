using System.Threading;
using System.Threading.Tasks;

namespace RearchitectTowardsAsyncAwait
{
    /// <summary>
    /// Inspired by http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx
    /// </summary>
    class AsyncManualResetEvent
    {
        private volatile TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncManualResetEvent(bool set)
        {
            if (set)
            {
                tcs.SetResult(true);
            }
        }

        public bool IsSet => tcs.Task.IsCompleted;

        public Task WaitAsync()
        {
            return WaitAsync(CancellationToken.None);
        }

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }

        public void Set()
        {
            tcs.TrySetResult(true);
        }

        public void Reset()
        {
            var sw = new SpinWait();

            do
            {
                var tcs = this.tcs;
                if (!tcs.Task.IsCompleted ||
#pragma warning disable 420
                    Interlocked.CompareExchange(ref this.tcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), tcs) == tcs)
#pragma warning restore 420
                    return;

                sw.SpinOnce();
            } while (true);
        }
    }
}