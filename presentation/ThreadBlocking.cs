using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AsyncDolls;
using NUnit.Framework;

namespace RearchitectTowardsAsyncAwait
{
    [TestFixture]
    public class ThreadBlocking
    {
        [Test]
        public async Task Locks()
        {
            //var locker = new object();

            //lock (locker)
            //{
            //    await Task.Yield();
            //}
        }

        [Test]
        public async Task Event()
        {
            MyEvent += MyEventHandler;

            try
            {
                OnMyEvent();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Caught: { ex.Message } ");
            }

            await Task.Delay(100);
        }

        event EventHandler MyEvent = delegate { };

        async void MyEventHandler(object sender, EventArgs e)
        {
            Console.WriteLine("Inside MyEventHandler");
            await Task.Yield();
            Console.WriteLine("About to throw inside MyEventHandler");
            throw new InvalidOperationException();
        }

        protected virtual void OnMyEvent()
        {
            MyEvent(this, EventArgs.Empty);
        }

        [Test]
        public async Task ManualResetEventUsage()
        {
            var syncEvent = new ManualResetEvent(false);

            var t1 = Task.Run(() =>
            {
                "Entering wait".Output();
                syncEvent.WaitOne();
                "Continue".Output();
            });

            var t2 = Task.Run(() =>
            {
                Thread.Sleep(2000);
                syncEvent.Set();
            });

            await Task.WhenAll(t1, t2);
        }

        [Test]
        public async Task SemaphoreUsage()
        {
            var semaphore = new Semaphore(1, 1);

            var tasks = new Task[3];
            for (int i = 0; i < 3; i++)
            {
                var i1 = i;
                tasks[i] = Task.Run(() =>
                {
                    $"{ i1 } Entering wait".Output();
                    semaphore.WaitOne();
                    Thread.Sleep(1000);
                    $"{ i1 } Continue".Output();
                    semaphore.Release();
                });
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
                tasks[i] = Task.Run(() =>
                {
                    classWithAmbientState.Do();
                    Thread.Sleep(200);
                    classWithAmbientState.Do();
                });
            }

            await Task.WhenAll(tasks);
        }

        class ClassWithAmbientState
        {
            static ThreadLocal<int> ambientState = new ThreadLocal<int>(() => 1); // newer version of [ThreadStatic]

            public void Do()
            {
                ambientState.Value++;

                $"Thread: { Thread.CurrentThread.ManagedThreadId }, Value: { ambientState.Value }".Output();
            }
        }

        [Test]
        public void OutParameterUsage()
        {
            string fileName;
            IoBoundMethodWithOutParameter("42", out fileName);
            fileName.Output();
        }

        static void IoBoundMethodWithOutParameter(string content, out string parameter)
        {
            var randomFileName = Path.GetTempFileName();
            using (var writer = new StreamWriter(randomFileName))
            {
                writer.Write(content);
            }
            parameter = randomFileName;
        }

        //static async Task IoBoundMethodWithOutParameter(string content, out string parameter)
        //{
        //    var randomFileName = Path.GetTempFileName();
        //    using (var writer = new StreamWriter(randomFileName))
        //    {
        //        await writer.WriteLineAsync(content);
        //    }
        //    parameter = randomFileName;
        //}

        [Test]
        public void RemotingUsage() // Not doing real remoting but you get the point
        {
            SyncClient syncClientApp = new SyncClient();
            syncClientApp.Run();
        }

        public class SyncClient : MarshalByRefObject
        {
            delegate string RemoteSyncDelegate();

            public void Run()
            {
                RemoteService remoteService = new RemoteService();

                RemoteSyncDelegate remoteCall = remoteService.TimeConsumingRemoteCall;

                remoteCall().Output();
            }
        }
    }
}