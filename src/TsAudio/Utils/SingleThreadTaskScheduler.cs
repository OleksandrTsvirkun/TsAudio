using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils;

public class SingleThreadTaskScheduler : TaskScheduler
{
    public static readonly SingleThreadTaskScheduler Instance = new();

    private readonly BlockingCollection<Task> tasks;
    private readonly Thread thread;

    public SingleThreadTaskScheduler()
    {
        this.tasks = new BlockingCollection<Task>();
        this.thread = new Thread(Execute)
        {
            Priority = ThreadPriority.Highest,
        };
        this.thread.Start();
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return this.tasks.ToList();
    }

    protected override void QueueTask(Task task)
    {
        this.tasks.Add(task);
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return false;
    }

    private async void Execute()
    {
        while(true)
        {
            var task = tasks.Take();
            base.TryExecuteTask(task);
            
            try
            {
                await task;
            }
            catch(Exception ex)
            {

            }

        }
    }
}
