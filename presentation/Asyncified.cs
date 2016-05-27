using System;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using AsyncDolls;
using NUnit.Framework;

namespace RearchitectTowardsAsyncAwait
{
    [TestFixture]
    public class Asyncified
    {
        [Test]
        public async Task Locks_WithSemaphore()
        {
            int sharedRessource = 0;

            var semaphore = new SemaphoreSlim(1);

            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                tasks[i] = ((Func<Task>) (async () =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        sharedRessource++;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }))();
            }

            await Task.WhenAll(tasks);

            sharedRessource.ToString().Output();
        }

        [Test]
        public async Task Locks_WithAsyncLock()
        {
            int sharedRessource = 0;

            var semaphore = new AsyncLock();

            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                tasks[i] = ((Func<Task>) (async () =>
                {
                    using (await semaphore.LockAsync())
                    {
                        sharedRessource++;
                    }
                }))();
            }

            await Task.WhenAll(tasks);

            sharedRessource.ToString().Output();
        }

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

        [Test]
        public async Task AsyncEvent()
        {
            MyAsyncEvent += MyAsyncEventHandler2;
            MyAsyncEvent += MyAsyncEventHandler;

            "Observing one exception".Output();

            try
            {
                await OnMyAsyncEvent();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Caught: {ex.Message} ");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Caught: {ex.Message} ");
            }

            "Observing all exceptions".Output();

            Task allTasks = null;
            try
            {
                allTasks = OnMyAsyncEvent();
                await allTasks;
            }
            catch
            {
                AggregateException allExceptions = allTasks.Exception;
                allExceptions.Handle(e =>
                {
                    Console.WriteLine(e.Message);
                    return true;
                });
            }
        }


        event AsyncEventHandler MyAsyncEvent;

        public delegate Task AsyncEventHandler(object sender, EventArgs e);

        async Task MyAsyncEventHandler(object sender, EventArgs e)
        {
            Console.WriteLine("Inside MyAsyncEventHandler");
            await Task.Yield();
            Console.WriteLine("About to throw inside MyAsyncEventHandler");
            throw new InvalidOperationException();
        }

        async Task MyAsyncEventHandler2(object sender, EventArgs e)
        {
            Console.WriteLine("Inside MyAsyncEventHandler2");
            await Task.Yield();
            Console.WriteLine("About to throw inside MyAsyncEventHandler2");
            throw new ArgumentException();
        }

        protected virtual Task OnMyAsyncEvent()
        {
            var handler = MyAsyncEvent;

            if (handler == null)
            {
                return Task.CompletedTask;
            }

            var invocationList = handler.GetInvocationList();
            var handlerTasks = new Task[invocationList.Length];

            for (int i = 0; i < invocationList.Length; i++)
            {
                handlerTasks[i] = ((AsyncEventHandler)invocationList[i])(this, EventArgs.Empty);
            }

            return Task.WhenAll(handlerTasks);
        }

        [Test]
        public async Task AsyncWaitForEvent()
        {
            var tcs = new TaskCompletionSource<object>();
            MyEvent += (sender, args) =>
            {
                tcs.TrySetResult(null);
            };

            ((Func<Task>) (async () =>
            {
                await Task.Delay(1000);
                "Firing event".Output();
                OnMyEvent();
            }))().Ignore();

            "Await completion".Output();
            await tcs.Task;
            "Done".Output();
        }

        event EventHandler MyEvent = delegate { };

        protected virtual void OnMyEvent()
        {
            MyEvent(this, EventArgs.Empty);
        }

        [Test]
        public async Task ManualResetEventUsage_OneTime()
        {
            var tcs = new TaskCompletionSource<object>();

            var t1 = ((Func<Task>)(async () =>
            {
                "Entering wait".Output();
                await tcs.Task;
                "Continue".Output();
            }))();

            var t2 = ((Func<Task>)(async () =>
            {
                await Task.Delay(2000);
                tcs.TrySetResult(null);
            }))();

            await Task.WhenAll(t1, t2);
        }


        [Test]
        public async Task AsyncManualResetEventUsage()
        {
            var syncEvent = new AsyncManualResetEvent(false);

            var t1 = ((Func<Task>) (async () =>
            {
                "Entering wait".Output();
                await syncEvent.WaitAsync();
                "Continue".Output();
            }))();

            var t2 = ((Func<Task>)(async () =>
            {
                await Task.Delay(2000);
                syncEvent.Set();
            }))();

            await Task.WhenAll(t1, t2);
        }

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

        [Test]
        public async Task SemaphoreSlimUsage()
        {
            var semaphore = new SemaphoreSlim(1);

            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                var i1 = i;

                tasks[i] = ((Func<Task>) (async () =>
                {
                    $"{ i1 } Entering wait".Output();
                    await semaphore.WaitAsync();
                    await Task.Delay(1000);
                    $"{ i1 } Continue".Output();
                    semaphore.Release();
                }))();
            }

            await Task.WhenAll(tasks);
        }

        [Test]
        public async Task AmbientState()
        {
            var classWithAmbientState = new ClassWithAmbientState();

            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                tasks[i] = ((Func<Task>)(async () =>
                {
                    classWithAmbientState.Do();
                    await Task.Delay(200).ConfigureAwait(false);
                    classWithAmbientState.Do();
                }))();
            }

            await Task.WhenAll(tasks);
        }

        class ClassWithAmbientState
        {
            static AsyncLocal<int> ambientState = new AsyncLocal<int>();

            static ClassWithAmbientState()
            {
                ambientState.Value = 1;
            }

            public void Do()
            {
                ambientState.Value++;

                $"Thread: { Thread.CurrentThread.ManagedThreadId }, Value: { ambientState.Value }".Output();
            }
        }

        [Test]
        public async Task AmbientFloatingState()
        {
            var classWithAmbientFloatingState = new ClassWithAmbientFloatingState();

            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                tasks[i] = ((Func<Task>)(async () =>
                {
                    var state = new State();
                    classWithAmbientFloatingState.Do(state);
                    await Task.Delay(200).ConfigureAwait(false);
                    classWithAmbientFloatingState.Do(state);
                }))();
            }

            await Task.WhenAll(tasks);
        }

        class ClassWithAmbientFloatingState
        {
            public void Do(State state)
            {
                state.Value++;

                $"Thread: { Thread.CurrentThread.ManagedThreadId }, Value: { state.Value }".Output();
            }
        }

        class State
        {
            public State()
            {
                Value = 1;
            }

            public int Value { get; set; }
        }

        [Test]
        public async Task AmbientFloatingStateReturned()
        {
            var classWithAmbientFloatingState = new ClassWithAmbientFloatingStateReturned();

            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                tasks[i] = ((Func<Task>)(async () =>
                {
                    int current = 1;
                    current = classWithAmbientFloatingState.Do(current);
                    await Task.Delay(200).ConfigureAwait(false);
                    classWithAmbientFloatingState.Do(current);
                }))();
            }

            await Task.WhenAll(tasks);
        }


        class ClassWithAmbientFloatingStateReturned
        {
            public int Do(int current)
            {
                current++;

                $"Thread: { Thread.CurrentThread.ManagedThreadId }, Value: { current }".Output();

                return current;
            }
        }

        [Test]
        public async Task OutParameterUsage()
        {
            string fileName = await IoBoundMethodWithOutParameter("42");
            fileName.Output();
        }

        static async Task<string> IoBoundMethodWithOutParameter(string content)
        {
            var randomFileName = Path.GetTempFileName();
            using (var writer = new StreamWriter(randomFileName))
            {
                await writer.WriteLineAsync(content);
            }
            return randomFileName;
        }

        [Test]
        public async Task RemotingUsage() // Not doing real remoting but you get the point
        {
            AsyncClient asyncClient = new AsyncClient();
            await asyncClient.Run();
        }

        public class AsyncClient : MarshalByRefObject
        {
            delegate string RemoteAsyncDelegate();

            [OneWay]
            public string OurRemoteAsyncCallBack(IAsyncResult ar)
            {
                RemoteAsyncDelegate del = (RemoteAsyncDelegate)((AsyncResult)ar).AsyncDelegate;
                return del.EndInvoke(ar);
            }

            public async Task Run()
            {
                RemoteService remoteService = new RemoteService();

                RemoteAsyncDelegate remoteCall = remoteService.TimeConsumingRemoteCall;

                var result = await Task.Factory.FromAsync(remoteCall.BeginInvoke, OurRemoteAsyncCallBack, null);
                result.Output();
            }
        }
    }
}