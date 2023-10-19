using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace TsAudio.Utils.Threading;

public class SingleThreadSynchronizationContext : SynchronizationContext
{
    private readonly Thread thread;
    private readonly BlockingCollection<(SendOrPostCallback Callback, object State)> tasks = new();

    /// <summary>
    /// Number of recursive posts.
    /// </summary>
    private int recursiveCount = 0;

    public SingleThreadSynchronizationContext(string threadName, ThreadPriority priority = ThreadPriority.Normal, bool isBackground = true)
    {
        this.thread = new Thread(this.Execute)
        {
            Name = threadName,
            Priority = priority,
            IsBackground = isBackground
        };
        this.thread.Start();
    }

    /// <summary>
    /// Send continuation into synchronization context.
    /// </summary>
    /// <param name="continuation">Continuation callback delegate.</param>
    /// <param name="state">Continuation argument.</param>
    public override void Send(SendOrPostCallback continuation, object state)
    {
        if(!this.TryExecuteInline(continuation, state))
        {
            using var slim = new ManualResetEventSlim();

            this.tasks.Add((state =>
            {
                slim.Reset();
                continuation(state);
                slim.Set();
            }, state));

            slim.Wait();
        }
    }

    /// <summary>
    /// Post continuation into synchronization context.
    /// </summary>
    /// <param name="continuation">Continuation callback delegate.</param>
    /// <param name="state">Continuation argument.</param>
    public override void Post(SendOrPostCallback continuation, object state)
    {
        if (!this.TryExecuteInline(continuation, state))
        {
            this.tasks.Add((continuation, state));
        }
    }

    public override string ToString()
    {
        return $"{nameof(SingleThreadSynchronizationContext)} {this.thread.Name} Id: {this.thread.ManagedThreadId}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryExecuteInline(SendOrPostCallback continuation, object state)
    {
        //Took here: https://github.com/kekyo/SynchContextSample/blob/master/SynchContextSample/QueueSynchronizationContext.cs#L88
        var currentThreadId = Thread.CurrentThread.ManagedThreadId;

        if(currentThreadId == this.thread.ManagedThreadId)
        {
            // HACK: If current thread is already target thread, invoke continuation directly.
            //   But if continuation has invokeing Post/Send recursive, cause stack overflow.
            //   We can fix this problem by simple solution: Continuation invoke every post into queue,
            //   but performance will be lost.
            //   This counter uses post for scattering (each 50 times).
            if(this.recursiveCount < 50)
            {
                this.recursiveCount++;

                // Invoke continuation on current thread is better performance.
                continuation(state);

                this.recursiveCount--;
                return true;
            }
        }

        return false;
    }

    private void Execute()
    {
        foreach(var task in this.tasks.GetConsumingEnumerable())
        {
            task.Callback(task.State);
        }
    }

}
