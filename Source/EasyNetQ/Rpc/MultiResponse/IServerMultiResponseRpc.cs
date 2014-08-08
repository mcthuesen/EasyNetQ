using System;
using System.Threading.Tasks;
using EasyNetQ.Topology;

namespace EasyNetQ.Rpc.MultiResponse
{
    public interface IServerMultiResponseRpc
    {
        IDisposable Respond(IExchange requestExchange,
                            string queueName,
                            string topic,
                            Func<SerializedMessage, IObserver<SerializedMessage>> handleRequest);
    }

    class ServerMultiResponseRpc : IServerMultiResponseRpc
    {
        private readonly IAdvancedBus _advancedBus;
        private readonly IConnectionConfiguration _configuration;
        private readonly IRpcHeaderKeys _rpcHeaderKeys;

        public ServerMultiResponseRpc(
            IAdvancedBus advancedBus,
            IConnectionConfiguration configuration,
            IRpcHeaderKeys rpcHeaderKeys)
        {
            Preconditions.CheckNotNull(advancedBus, "advancedBus");
            Preconditions.CheckNotNull(configuration, "configuration");

            _advancedBus = advancedBus;
            _configuration = configuration;
            _rpcHeaderKeys = rpcHeaderKeys;
        }

        public IDisposable Respond(IExchange requestExchange, string queueName, string topic, Func<SerializedMessage, IObserver<SerializedMessage>> handleRequest)
        {
            Preconditions.CheckNotNull(requestExchange, "requestExchange");
            Preconditions.CheckNotNull(queueName, "queueName");
            Preconditions.CheckNotNull(topic, "topic");
            Preconditions.CheckNotNull(handleRequest, "handleRequest");

            var expires = (int)TimeSpan.FromSeconds(_configuration.Timeout).TotalMilliseconds;

            //_advancedBus.ExchangeDeclare(requestExchange.Name, )

            var queue = _advancedBus.QueueDeclare(
                queueName,
                passive: false,
                durable: false,
                exclusive: false,
                autoDelete: true,
                expires: expires);

            _advancedBus.Bind(requestExchange, queue, topic);

            var responseExchange = Exchange.GetDefault();
            return _advancedBus.Consume(queue, (msgBytes, msgProp, messageRecievedInfo) => ExecuteResponder(responseExchange, handleRequest, new SerializedMessage(msgProp, msgBytes)));
        }

        private Task ExecuteResponder(IExchange responseExchange, Func<SerializedMessage, Task<SerializedMessage>> responder, SerializedMessage requestMessage)
        {
            return responder(requestMessage)
                .ContinueWith(RpcHelpers.MaybeAddExceptionToHeaders(_rpcHeaderKeys, requestMessage))
                .Then(uhInfo =>
                {
                    var sm = uhInfo.Response;
                    sm.Properties.CorrelationId = requestMessage.Properties.CorrelationId;
                    _advancedBus.Publish(responseExchange, requestMessage.Properties.ReplyTo, false, false, sm.Properties, sm.Body);
                    return TaskHelpers.FromResult(uhInfo);
                })
                .Then(uhInfo =>
                {
                    if (uhInfo.IsFailed())
                    {
                        throw new EasyNetQResponderException("MessageHandler Failed", uhInfo.Exception);
                    }
                    return TaskHelpers.FromResult(0);
                });
        }
    }
}
