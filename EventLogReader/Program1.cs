using System;
using System.Threading.Tasks;

namespace EventLogReader
{
    // a throttled  producer/consumer pattern implementation by reading the windows event logs and sending them to elasticsearch

    class Program1
    {
        static async Task Main(string[] args)
        {
            var reader = new EventLogReader();
            var writer = new EventLogWriter();
            var readerWriter = new EventLogReaderWriter(reader, writer);
            await readerWriter.Execute(batchSize: 1000, maxInFlightMessages: 5000);
            Console.ReadKey();
        }
    }
}

