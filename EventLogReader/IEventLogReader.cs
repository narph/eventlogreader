using Nest;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace EventLogReader
{

    //producer
    public interface IEventLogReader
    {
        EventLog[] GetGroups();
    }

    public class EventLogReader : IEventLogReader
    {
        public EventLog[] GetGroups()
        {
            return EventLog.GetEventLogs()
                .Where(e => e.Log != "System").ToArray();
        }
    }


    //consumer

    public interface IEventLogWriter
    {
        Task<bool> Consume(EventLogEntry[] batch);
    }

    public class EventLogWriter : IEventLogWriter
    {
        public ElasticClient Client { get; }
        public EventLogWriter()
        {
            var connectionSettings = new ConnectionSettings(new Uri("{es uri}"))
                .DefaultIndex("event-log")
                .BasicAuthentication("elastic", "{pass}");
            Client = new ElasticClient(connectionSettings);
        }

        public async Task<bool> Consume(EventLogEntry[] batch)
        {
            var response = await Client.IndexManyAsync(batch);
            return response.IsValid;
        }
    }



    public class EventLogReaderWriter
    {
        private readonly IEventLogReader reader;
        private readonly IEventLogWriter writer;

        public EventLogReaderWriter(IEventLogReader eventLogReader, IEventLogWriter eventLogWriter)
        {
            this.reader = eventLogReader;
            this.writer = eventLogWriter;
        }

        public async Task Execute(int batchSize, int maxInFlightMessages)
        {
            var queue = new BatchBlock<EventLogEntry>(batchSize, new GroupingDataflowBlockOptions() { BoundedCapacity = maxInFlightMessages });
            var remoteEventLogs = reader.GetGroups();

            using (var mainProgressBar = new ProgressBar(remoteEventLogs.Length, "Reading all event logs"))
            {
                var producer = RetrieveEventLogs(queue, remoteEventLogs, mainProgressBar);
                var consumer = SendEventLogs(queue, remoteEventLogs.Sum(e => e.Entries.Count), mainProgressBar);
                await Task.WhenAll(producer, consumer, queue.Completion);
            }
        }

        public async Task SendEventLogs(BatchBlock<EventLogEntry> queue, int totalMessages, ProgressBar progressBar)
        {
            var sw = new Stopwatch();

            using (var pbar = progressBar.Spawn(totalMessages, $"Elastic", new ProgressBarOptions { CollapseWhenFinished = false, ForegroundColor = ConsoleColor.White }))
            {
                while (await queue.OutputAvailableAsync())
                {
                    var batch = await queue.ReceiveAsync();
                    sw.Restart();
                    var success = await writer.Consume(batch);
                    pbar.Message = $"Indexing success: {success} took: {sw.Elapsed}";
                    foreach (var x in Enumerable.Range(0, batch.Count())) pbar.Tick();
                }
                pbar.Message = "Done indexing!";
            }
        }

        private async Task RetrieveEventLogs(BatchBlock<EventLogEntry> queue, EventLog[] eventLogs, ProgressBar progressBar)
        {
            var tasks = new List<Task>();
            foreach (EventLog log in eventLogs)
            {
                tasks.Add(Task.Run(async () => await ReadEventLog(queue, log, progressBar)));
            }
            await Task.WhenAll(tasks);
            queue.Complete();

        }
        private async Task ReadEventLog(BatchBlock<EventLogEntry> queue, EventLog eventLog, ProgressBar progressBar)
        {
            var rand = new Random();
            var colour = rand.Next(1, 15);
            var entries = eventLog.Entries;

            using (var pbar = progressBar.Spawn(entries.Count, $"{eventLog.Log}", new ProgressBarOptions { CollapseWhenFinished = false, ForegroundColor = (ConsoleColor)colour }))
            {
                foreach (EventLogEntry entry in entries)
                {
                    pbar.Tick($"{eventLog.LogDisplayName}:{entry.InstanceId}");
                    await queue.SendAsync(entry);
                }
            }
            progressBar.Tick();
        }


    }

}
