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
        [Params(1, 20)]
        public int ThreadCount { get; set; }
        [Params(1, 2)]
        public int IsADO { get; set; }

        public async Task Add1000LogMessage(LogType logType)
        {
            
            int TargetRecordCount = Program.TotalCount / ThreadCount;
            Task[] tasks = new Task[ThreadCount];

            for (int i = 0; i < ThreadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    string connectionStr = @"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321!; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False";

                    // Используем using: конструктор сам откроет SqlConnection и применит SetLogType!
                    using (DBHelper dbset = new DBHelper(connectionStr, logType))
                    {
                        for (int j = 0; j < TargetRecordCount; j++)
                        {
                            if(IsADO ==1 )
                              dbset.AddLogMessageDirect();    // НАПРЯМУЮ (ADO.NET) — для максимальной скорости
                            else 
                               dbset.AddLogMessage();       // Через Entity Framework
                        }
                    } // Тут соединение закроется автоматически
                });
            }

            // Ждем, пока все потоки завершат работу. 
            // Этот суммарный "залп" и будет измерен как 1 Invocation.
            await Task.WhenAll(tasks);
        }

        [Benchmark(Baseline = true)]
        public async Task LogLocalTable()
        {
            await Add1000LogMessage(LogType.LocalTable);
        }

        [Benchmark]
        public async Task LogLinkedServerTable()
        {
            await Add1000LogMessage(LogType.LinkedServerTable);
        }

        [Benchmark]
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
            DBHelper dbHelper = new DBHelper(@$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321!; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
            dbHelper.SetLogType(LogType.RabbitMQPost);
            dbHelper.AddLogMessage();
        }
    }
}