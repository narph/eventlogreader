using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ShellProgressBar;

namespace EventLogReader
{

    //simple example of a multithreaded reader of all windows event logs using raw threads

    class Program
    {
        static void Main1(string[] args)
        {
            var remoteEventLogs = EventLog.GetEventLogs();
            var threads = new List<Thread>();
            using (var mainProgressBar = new ProgressBar(remoteEventLogs.Length, "Reading all event logs"))
            {
                foreach (EventLog log in remoteEventLogs)
                {
                    var thread = new Thread(() => ReadEventLog(log, mainProgressBar));
                    thread.Start();
                    threads.Add(thread);
                }
                threads.ForEach(thread => thread.Join());
            }
            Console.ReadKey();
        }


        static void ReadEventLog(EventLog eventLog, ProgressBar progressBar)
        {
            var rand = new Random();
            var colour = rand.Next(1, 15);

            using (var pbar = progressBar.Spawn(eventLog.Entries.Count, $"{eventLog.Log}", new ProgressBarOptions { CollapseWhenFinished = false, ForegroundColor = (ConsoleColor)colour }))
            {
                foreach (EventLogEntry entry in eventLog.Entries)
                {
                    pbar.Tick($"{eventLog.LogDisplayName}:{entry.InstanceId}");
                }
            }
            progressBar.Tick();
        }

    }
}
