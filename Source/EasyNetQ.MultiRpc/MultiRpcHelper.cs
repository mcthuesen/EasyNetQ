using System;
using System.Text;

namespace EasyNetQ.MultiRpc
{
    static class MultiRpcHelper
    {
        public static SerializedMessage CreateErrorMessage(Exception ex)
        {
            var sm = new SerializedMessage(new MessageProperties(), new byte[] { });
            sm.Properties.Headers.Add(MultiRpcHeaders.ExceptionMessage, ex.StackTrace);
            return sm;
        }

        public static SerializedMessage CreateCompletedMessage()
        {
            var sm = new SerializedMessage(new MessageProperties(), new byte[] { });
            sm.Properties.Headers.Add(MultiRpcHeaders.IsCompletedMessage, true);
            return sm;
        }

        public static bool IsErrorMessage(SerializedMessage sm, out string exceptionMessage)
        {
            if (sm.Properties.HeadersPresent)
            {
                if (sm.Properties.Headers.ContainsKey(MultiRpcHeaders.ExceptionMessage))
                {
                    exceptionMessage = Encoding.UTF8.GetString((byte[])sm.Properties.Headers[MultiRpcHeaders.ExceptionMessage]);
                    return true;
                }
            }
            exceptionMessage = null;
            return false;
        }

        public static bool IsCompletedMessage(SerializedMessage sm)
        {
            if (sm.Properties.HeadersPresent)
            {
                if (sm.Properties.Headers.ContainsKey(MultiRpcHeaders.IsCompletedMessage))
                {
                    return Convert.ToBoolean(sm.Properties.Headers[MultiRpcHeaders.IsCompletedMessage]);
                }
            }
            return false;
        }
    }
}