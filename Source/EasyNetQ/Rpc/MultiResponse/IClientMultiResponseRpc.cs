using System;
using EasyNetQ.Topology;

namespace EasyNetQ.Rpc.MultiResponse
{
    public interface IClientMultiResponseRpc
    {
        IObservable<SerializedMessage> Request(
            IExchange requestExchange,
            string requestRoutingKey,
            bool mandatory,
            bool immediate,
            TimeSpan timeout,
            SerializedMessage request);
    }

    class ClientMultiResponseRpc : IClientMultiResponseRpc
    {
        public IObservable<SerializedMessage> Request(
            IExchange requestExchange, 
            string requestRoutingKey, 
            bool mandatory, 
            bool immediate,
            TimeSpan timeout, 
            SerializedMessage request)
        {
            throw new NotImplementedException();
        }
    }
}