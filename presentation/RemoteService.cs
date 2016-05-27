using System;
using System.Threading;

namespace RearchitectTowardsAsyncAwait
{
    public class RemoteService : MarshalByRefObject
    {
        public string TimeConsumingRemoteCall()
        {
            Thread.Sleep(1000);
            return "Hello from remote.";
        }
    }
}