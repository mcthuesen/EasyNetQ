using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyNetQ.Rpc.FreshQueue;
using EasyNetQ.Topology;

namespace EasyNetQ.Rpc
{
    using Newtonsoft.Json;

    static class RpcHelpers
    {
        private static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };

        public static void PublishRequest(IAdvancedBus advancedBus, IExchange requestExchange, SerializedMessage request, string requestRoutingKey, string responseQueueName, Guid correlationId, TimeSpan timeout)
        {
            request.Properties.ReplyTo = responseQueueName;
            request.Properties.CorrelationId = correlationId.ToString();
            request.Properties.Expiration = timeout.TotalMilliseconds.ToString();
            request.Properties.DeliveryMode = 1;

            //TODO write a specific RPC publisher that handles BasicReturn. Then we can set immediate+mandatory to true and react accordingly (now it will time out)
            advancedBus.Publish(requestExchange, requestRoutingKey, false, false, request.Properties, request.Body);
        }

        public static void ExtractExceptionFromHeadersAndPropagateToTaskCompletionSource(IRpcHeaderKeys rpcHeaderKeys, SerializedMessage sm, TaskCompletionSource<SerializedMessage> tcs)
        {
            var isFaulted = false;
            Exception extractedException = null;
            if (sm.Properties.HeadersPresent)
            {
                if (sm.Properties.Headers.ContainsKey(rpcHeaderKeys.IsFaultedKey))
                {
                    isFaulted = Convert.ToBoolean(sm.Properties.Headers[rpcHeaderKeys.IsFaultedKey]);
                }
                if (sm.Properties.Headers.ContainsKey(rpcHeaderKeys.ExceptionKey))
                {
                    var serialiazed = Encoding.UTF8.GetString((byte[])sm.Properties.Headers[rpcHeaderKeys.ExceptionKey]);
                    extractedException = JsonConvert.DeserializeObject<Exception>(serialiazed, _jsonSerializerSettings);
                }
            }
            if (isFaulted)
            {
                extractedException = extractedException ?? new EasyNetQResponderException("The exception message has not been specified.");
                tcs.TrySetException(extractedException);
            }
            else
            {
                tcs.TrySetResult(sm);
            }
        }

        public static Task<SerializedMessage> ExtractExceptionFromHeadersAndPropagateToTask(IRpcHeaderKeys rpcHeaderKeys, SerializedMessage sm)
        {
            var tcs = new TaskCompletionSource<SerializedMessage>();
            ExtractExceptionFromHeadersAndPropagateToTaskCompletionSource(rpcHeaderKeys, sm, tcs);
            return tcs.Task;
        }

        public static Func<Task<SerializedMessage>, UserHandlerInfo> MaybeAddExceptionToHeaders(IRpcHeaderKeys rpcHeaderKeys, SerializedMessage requestMessage)
        {
            return task =>
            {
                if (task.IsFaulted)
                {
                    if (task.Exception != null)
                    {
                        var serializedExc = JsonConvert.SerializeObject(task.Exception.Flatten(), _jsonSerializerSettings);

                        var sm = new SerializedMessage(new MessageProperties(), new byte[] { });
                        sm.Properties.Headers.Add(rpcHeaderKeys.IsFaultedKey, true);
                        sm.Properties.Headers.Add(rpcHeaderKeys.ExceptionKey, serializedExc);
                        sm.Properties.CorrelationId = requestMessage.Properties.CorrelationId;

                        return new UserHandlerInfo(sm,task.Exception);
                    }
                }
                return new UserHandlerInfo(task.Result);
            };
        }
    }
}