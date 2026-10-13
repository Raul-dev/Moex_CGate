using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TestPerformance
{
    [MemoryDiagnoser]
    public class AuditParserBenchmarks
    {

        public async Task Add1000LogMessage(LogType logType)
        {
            int TargetThreadCount = 20;
            Task[] tasks = new Task[TargetThreadCount];

            for (int i = 0; i < TargetThreadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    string connection = @$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False";
                    DBHelper dbset = new DBHelper(connection);
                    dbset.SetLogType(logType);

                    for (int j = 0; j < 10; j++)
                    {
                        dbset.AddLogMessage();
                    }
                });
            }

            // Ждем, пока все потоки завершат работу. 
            // Этот суммарный "залп" и будет измерен как 1 Invocation.
            await Task.WhenAll(tasks);
        }

        [Benchmark]
        public async Task LogLocalTable()
        {
            await Add1000LogMessage(LogType.LocalTable);
        }

        [Benchmark]
        public async Task LogLinkedServerTable()
        {
            await Add1000LogMessage(LogType.LinkedServerTable);
        }

        [Benchmark(Baseline = true)]
        public async Task LogRabbitMQPost()
        {
            await Add1000LogMessage(LogType.RabbitMQPost);
        }


        [Benchmark]
        public async Task LogInMemoryTableTable()
        {
            await Add1000LogMessage(LogType.InMemoryTable);
        }

        [Benchmark]
        public async Task LogSequentialDiskTable()
        {
            await Add1000LogMessage(LogType.SequentialDiskTable);
        }

        [Benchmark]
        public async Task LogServiceBrokerTable()
        {
            await Add1000LogMessage(LogType.ServiceBroker);
        }

        public void LogRabbitMQPostDebug()
        {
            DBHelper dbHelper = new DBHelper(@$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
            dbHelper.SetLogType(LogType.RabbitMQPost);
            dbHelper.AddLogMessage();
        }
    }
}