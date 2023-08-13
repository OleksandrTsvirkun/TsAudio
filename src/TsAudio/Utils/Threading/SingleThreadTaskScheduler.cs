using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Threading;

public class SingleThreadTaskScheduler : TaskScheduler
{
    public static new SingleThreadTaskScheduler Default = new();

    private readonly Thread thread;
    private readonly BlockingCollection<Task> tasks = new();

    public override int MaximumConcurrencyLevel => 1;

    public SingleThreadTaskScheduler()
    {
        this.thread = new Thread(this.Execute);
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
}
