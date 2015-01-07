using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using EasyNetQ.Topology;

namespace EasyNetQ.MultiRpc
{
    public interface IServerMultiResponseRpc
    {
        IDisposable Respond(string requestExchangeName,
                            string queueName,
                            string topic,
                            Func<SerializedMessage, IObserver<SerializedMessage>, Task> produceResponds);
    }

    public class ServerMultiResponseRpc : IServerMultiResponseRpc
    {
        private readonly IAdvancedBus _advancedBus;

        public ServerMultiResponseRpc(
            IAdvancedBus advancedBus)
        {
            _advancedBus = advancedBus;
        }

        public IDisposable Respond(string requestExchangeName, string queueName, string topic, Func<SerializedMessage,IObserver<SerializedMessage>, Task> produceResponds)
        {
            var expires = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;

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
                (msgBytes, msgProp, messageRecievedInfo) => ExecuteResponder(responseExchange, produceResponds, new SerializedMessage(msgProp, msgBytes)));
        }

        private Task ExecuteResponder(
            IExchange responseExchange,
            Func<SerializedMessage, IObserver<SerializedMessage>, Task> responder, 
            SerializedMessage requestMessage)
        {
            var tcs = new TaskCompletionSource<object>();
            var subject = new Subject<SerializedMessage>();

            

            //will be disposed when onCompleted or onError is called
            var dispose = Observable.Create<SerializedMessage>(observer => subject.Subscribe(observer))
                .Subscribe(
                OnNext(responseExchange, requestMessage),            //send reply
                OnError(responseExchange, requestMessage, tcs),      //send error message, tcs.TrySetException(ex)
                OnCompleted(responseExchange, requestMessage, tcs)   //() => send completed message + tcs.TrySetResult(null)
                );

            responder(requestMessage, subject)
                .ContinueWith(task =>
                    {
                        if (task.IsFaulted)
                        {
                            subject.OnError(task.Exception != null ? (Exception)task.Exception: new EasyNetQException("UserHandler failed without exception"));
                        }
                    });


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
