using System;
using EasyNetQ.Topology;

namespace EasyNetQ.MultiRpc
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
        private readonly IAdvancedBus _advancedBus;
        private readonly TimeSpan _timeout;

        public ClientMultiResponseRpc(IAdvancedBus advancedBus, TimeSpan timeout)
        {
            _advancedBus = advancedBus;
            _timeout = timeout;
        }

        public IObservable<SerializedMessage> Request(
            IExchange requestExchange, 
            string requestRoutingKey, 
            bool mandatory, 
            bool immediate,
            TimeSpan timeout, 
            SerializedMessage request)
        {
            var correlationId = Guid.NewGuid();
            var responseQueueName = "rpc:" + correlationId;

            var queue = _advancedBus.QueueDeclare(
                responseQueueName,
                passive: false,
                durable: false,
                expires: (int)timeout.TotalMilliseconds,
                exclusive: true,
                autoDelete: true);

            //the response is published to the default exchange with the queue name as routingkey. So no need to bind to exchange
            var continuation = _advancedBus.Consume(queue, timeout);

            PublishRequest(requestExchange, requestRoutingKey, timeout, request, responseQueueName, correlationId);



            return continuation
                .Then(mcc => TaskHelpers.FromResult(new SerializedMessage(mcc.Properties, mcc.Message)))
                .Then(sm => RpcHelpers.ExtractExceptionFromHeadersAndPropagateToTask(_rpcHeaderKeys, sm));
        }

        private static void PublishRequest(IExchange requestExchange, string requestRoutingKey, TimeSpan timeout,
                                           SerializedMessage request, string responseQueueName, Guid correlationId)
        {
            request.Properties.ReplyTo = responseQueueName;
            request.Properties.CorrelationId = correlationId.ToString();
            request.Properties.Expiration = timeout.TotalMilliseconds.ToString();

            advancedBus.Publish(requestExchange, requestRoutingKey, false, false, request.Properties, request.Body);
        }
    }
}