using System;
using System.Collections.Concurrent;
using System.Threading;

namespace TsAudio.Drivers.WinMM
{
    public class PlaybackThread
    {
        private BlockingCollection<Action> tasks;
        private Thread thread;

        public PlaybackThread()
        {
            this.tasks = new BlockingCollection<Action>();
            this.thread = new Thread(DoWork);
            this.thread.Start();
        }

        public void Execute(Action action)
        {
            this.tasks.Add(action);
        }

        private void DoWork()
        {
            while(thread.IsAlive)
            {
                var task = this.tasks.Take();
                try
                {
                    task();
                }
                catch(Exception ex)
                {

                }
            }
        }

    }
}
