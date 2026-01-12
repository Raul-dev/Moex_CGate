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

        public void Add1000LogMessage(LogType logType)
        {
            Parallel.For(0, 10, i =>
            {
                DBHelper dbHelper = new DBHelper(@$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
                dbHelper.SetLogType(logType);

                for (int j = 0; j < 100; j++)
                {
                    dbHelper.AddLogMessage();
                }

            });
        }

        [Benchmark]
        public void LogLocalTable()
        {
            Add1000LogMessage(LogType.LocalTable)
        }

        [Benchmark]
        public void LogLinkedServerTable()
        {
            Add1000LogMessage(LogType.LinkedServerTable)
        }

        [Benchmark(Baseline = true)]
        public void LogRabbitMQPost()
        {
            Add1000LogMessage(LogType.RabbitMQPost)
        }


        public void LogRabbitMQPostDebug()
        {
            DBHelper dbHelper = new DBHelper(@$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
            dbHelper.SetLogType(LogType.RabbitMQPost);
            dbHelper.AddLogMessage();
        }
    }
}