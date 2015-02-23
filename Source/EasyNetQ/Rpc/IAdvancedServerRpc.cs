using System;
using System.Threading.Tasks;

namespace EasyNetQ.Rpc
{
    using EasyNetQ.Topology;

    public interface IAdvancedServerRpc
    {
        IDisposable Respond(string requestExchange, string queueName, string topic, Func<SerializedMessage, MessageReceivedInfo, Task<SerializedMessage>> handleRequest);
        IDisposable Respond(IQueue queue, Func<SerializedMessage, MessageReceivedInfo, Task<SerializedMessage>> handleRequest);
    }
}