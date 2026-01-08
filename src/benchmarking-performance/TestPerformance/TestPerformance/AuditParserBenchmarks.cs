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

        [Benchmark]
        public void LogLocalTable()
        {
            Parallel.For(0, 10, i =>
            {
                DBHelper dbHelper = new DBHelper(@$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
                dbHelper.SetLogType(LogType.LocalTable);

                for (int j = 0; j < 100; j++)
                {
                    dbHelper.AddLogMessage();

                }

            });
        }

        [Benchmark]
        public void LogLinkedServerTable()
        {
            Parallel.For(0, 10, i =>
            {
                DBHelper dbHelper = new DBHelper(@$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
                dbHelper.SetLogType(LogType.LinkedServerTable);

                for (int j = 0; j < 100; j++)
                {
                    dbHelper.AddLogMessage();
                }
            });
        }

        [Benchmark(Baseline = true)]
        public void LogRabbitMQPost()
        {
            Parallel.For(0, 10, i =>
            {
                DBHelper dbHelper = new DBHelper(@$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
                dbHelper.SetLogType(LogType.RabbitMQPost);

                for (int j = 0; j < 100; j++)
                {
                    dbHelper.AddLogMessage();
                }
            });
        }


        public void LogRabbitMQPostDebug()
        {
            DBHelper dbHelper = new DBHelper(@$"Server = localhost; Database = CGate; User = CGateUser; Password = MyPassword321; MultipleActiveResultSets = true; TrustServerCertificate = true; Encrypt = False");
            dbHelper.SetLogType(LogType.RabbitMQPost);
            dbHelper.AddLogMessage();
        }
    }
}