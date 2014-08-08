using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using EasyNetQ.Topology;

namespace EasyNetQ.MultiRpc
{
    public interface IClientMultiResponseRpc
    {
        IDisposable Request(
            IExchange requestExchange,
            string requestRoutingKey,
            bool mandatory,
            bool immediate,
            TimeSpan timeout,
            SerializedMessage request,
            Action<IObservable<SerializedMessage>> subscribe);
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

        public IDisposable Request(
            IExchange requestExchange, 
            string requestRoutingKey, 
            bool mandatory, 
            bool immediate,
            TimeSpan timeout, 
            SerializedMessage request,
            Action<IObservable<SerializedMessage>> subscribe)
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

            var subject = new Subject<SerializedMessage>();
            var observable = Observable.Create<SerializedMessage>(observer => subject.Subscribe(observer));
            subscribe(observable);

            //the response is published to the default exchange with the queue name as routingkey. So no need to bind to exchange
            var consuming = _advancedBus.Consume(queue, (bytes, properties, arg3) => HandleMessage(subject)(new SerializedMessage(properties, bytes)));
            //TODO set timeout to call subject + dispose consumer

            PublishRequest(requestExchange, requestRoutingKey, timeout, request, responseQueueName, correlationId);
            return consuming;
        }

        private Func<SerializedMessage,Task> HandleMessage(Subject<SerializedMessage> subject)
        {
            return sm =>
                {
                    try
                    {
                        string exceptionMessage;
                        if (MultiRpcHelper.IsErrorMessage(sm, out exceptionMessage))
                        {
                            subject.OnError(new EasyNetQException(exceptionMessage));
                        }
                        else if (MultiRpcHelper.IsCompletedMessage(sm))
                        {
                            subject.OnCompleted();
                        }
                        else
                        {
                            subject.OnNext(sm);
                        }
                    }
                    catch (Exception ex)
                    {
                        subject.OnError(ex);
                    }
                    return TaskHelpers.Completed;
                };
        }

        private void PublishRequest(
            IExchange requestExchange, 
            string requestRoutingKey, 
            TimeSpan timeout,
            SerializedMessage request, 
            string responseQueueName, 
            Guid correlationId)
        {
            request.Properties.ReplyTo = responseQueueName;
            request.Properties.CorrelationId = correlationId.ToString();
            request.Properties.Expiration = timeout.TotalMilliseconds.ToString();

            _advancedBus.Publish(requestExchange, requestRoutingKey, false, false, request.Properties, request.Body);
        }
    }
}