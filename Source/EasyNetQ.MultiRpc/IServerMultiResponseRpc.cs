using System;
using System.Threading.Tasks;
using EasyNetQ.Topology;

namespace EasyNetQ.MultiRpc
{
    public interface IServerMultiResponseRpc
    {
        IDisposable Respond(string requestExchangeName,
                            string queueName,
                            string topic,
                            Func<SerializedMessage, IObservable<SerializedMessage>> handleRequest);
    }

    class ServerMultiResponseRpc : IServerMultiResponseRpc
    {
        private readonly IAdvancedBus _advancedBus;
        private readonly IConnectionConfiguration _configuration;

        public ServerMultiResponseRpc(
            IAdvancedBus advancedBus,
            IConnectionConfiguration configuration)
        {
            _advancedBus = advancedBus;
            _configuration = configuration;
        }

        public IDisposable Respond(string requestExchangeName, string queueName, string topic, Func<SerializedMessage, IObservable<SerializedMessage>> handleRequest)
        {
            var expires = (int)TimeSpan.FromSeconds(_configuration.Timeout).TotalMilliseconds;

            var exchange = _advancedBus.ExchangeDeclare(requestExchangeName, ExchangeType.Topic);

            var queue = _advancedBus.QueueDeclare(
                queueName,
                passive: false,
                durable: false,
                exclusive: false,
                autoDelete: true,
                expires: expires);

            _advancedBus.Bind(exchange, queue, topic);

            var responseExchange = Exchange.GetDefault();
            return _advancedBus.Consume(
                queue, 
                (msgBytes, msgProp, messageRecievedInfo) => 
                    Task.Factory.StartNew(() => ExecuteResponder(responseExchange, handleRequest, new SerializedMessage(msgProp, msgBytes))));
        }

        private Task ExecuteResponder(
            IExchange responseExchange, 
            Func<SerializedMessage, 
            IObservable<SerializedMessage>> responder, 
            SerializedMessage requestMessage)
        {
            var tcs = new TaskCompletionSource<object>();

            //will be disposed when onCompleted or onError is called
            var dispose = responder(requestMessage).Subscribe(
                OnNext(responseExchange, requestMessage),            //send reply
                OnError(responseExchange, requestMessage, tcs),      //send error message, tcs.TrySetException(ex)
                OnCompleted(responseExchange, requestMessage, tcs)   //() => send completed message + tcs.TrySetResult(null)
                );

            return tcs.Task;
        }

        private Action<SerializedMessage> OnNext(IExchange responseExchange, SerializedMessage requestMessage)
        {
            return sm => SendReply(responseExchange, requestMessage, sm);
        }

        private Action OnCompleted(IExchange responseExchange, SerializedMessage requestMessage, TaskCompletionSource<object> tcs)
        {
            return () =>
                {
                    SendReply(responseExchange, requestMessage, MultiRpcHelper.CreateCompletedMessage());
                    tcs.TrySetResult(null);
                };
        }

        private Action<Exception> OnError(IExchange responseExchange, SerializedMessage requestMessage, TaskCompletionSource<object> tcs)
        {
            return ex =>
                {
                    SendReply(responseExchange, requestMessage, MultiRpcHelper.CreateErrorMessage(ex));
                    tcs.TrySetException(ex);
                };
        }

        private void SendReply(IExchange responseExchange, SerializedMessage requestMessage, SerializedMessage reply)
        {
            reply.Properties.CorrelationId = requestMessage.Properties.CorrelationId;
            _advancedBus.Publish(responseExchange, requestMessage.Properties.ReplyTo, false, false, reply.Properties, reply.Body);
        }
    }
}
