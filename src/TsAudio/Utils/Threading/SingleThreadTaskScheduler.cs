using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Threading;

public class SingleThreadTaskScheduler : TaskScheduler, IDisposable
{
    private static Lazy<SingleThreadTaskScheduler> @default = new(() => new SingleThreadTaskScheduler(nameof(Default) + nameof(SingleThreadTaskScheduler))); 
    public static new SingleThreadTaskScheduler Default = @default.Value;
    private bool disposed;
    private readonly Thread thread;
    private readonly BlockingCollection<Task> tasks = new();

    public override int MaximumConcurrencyLevel => 1;

    public SingleThreadTaskScheduler(string threadName = nameof(SingleThreadTaskScheduler))
    {
        this.thread = new Thread(this.Execute);
        this.thread.Name = threadName;
        this.thread.Start();
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return this.tasks.ToList();
    }

    protected override void QueueTask(Task task)
    {
        this.tasks.Add(task);

        if(this.thread.ThreadState == System.Threading.ThreadState.Unstarted)
        {
            this.thread.Start();
        }
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return false;
    }

    private void Execute()
    {
        foreach(var task in this.tasks.GetConsumingEnumerable())
        {
            try
            {
                this.TryExecuteTask(task);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }

    public void Dispose()
    {
        if(!this.disposed)
        {
            this.tasks.Dispose();
            this.disposed = true;
        }
    }
}
