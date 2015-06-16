namespace EasyNetQ.Rpc
{
    public class RpcHeaderKeys : IRpcHeaderKeys
    {
        public string IsFaultedKey { get { return "Rpc.IsFaulted"; } }
        public string ExceptionKey { get { return "Rpc.ExceptionMessage"; } }
    }
}