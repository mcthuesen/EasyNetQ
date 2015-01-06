namespace EasyNetQ.Consumer
{
    using System.Threading.Tasks;

    public interface IAckContinuationStrategy
    {
        void AckContinuation(ConsumerExecutionContext context, Task task, bool handled, bool acked);
    }

    public class DefaultAckContinuationStrategy : IAckContinuationStrategy 
    {
        public void AckContinuation(ConsumerExecutionContext context, Task task, bool handled, bool acked){}
    }
}