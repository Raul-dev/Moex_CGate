using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace MQ.Share
{
    /*
    public class ThreadManagerAsync : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cts;
        private readonly ConcurrentBag<Task> _tasks;
        private ConcurrentDictionary<string, Task> _tasks = new ConcurrentDictionary<int, Task>();

        public ThreadManagerAsync(int maxDegreeOfParallelism)
        {
            _semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            _cts = new CancellationTokenSource();
            _tasks = new ConcurrentBag<Task>();
        }

        public async Task RunAsync(Func<CancellationToken, Task> action)
        {
            await _semaphore.WaitAsync(); // Limit concurrency

            var task = Task.Run(async () =>
            {
                try
                {
                    await action(_cts.Token);
                }
                finally
                {
                    _semaphore.Release();
                }
            }, _cts.Token);

            _tasks.Add(task);
        }

        public async Task WaitForAllAsync()
        {
            await Task.WhenAll(_tasks.ToArray());
        }

        public void CancelAll()
        {
            _cts.Cancel();
        }

        public void Dispose()
        {
            _semaphore.Dispose();
            _cts.Dispose();
        }
    }
    */
    public class ThreadManager
    {
        private ConcurrentDictionary<int, Task> _tasks = new ConcurrentDictionary<int, Task>();
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public void StartTasks(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int taskId = i;
                _tasks[i] = Task.Factory.StartNew(() => WorkerMethod(taskId, _cts.Token), _cts.Token);
            }
        }

        private void WorkerMethod(int id, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Console.WriteLine($"Task {id} running...");
                Thread.Sleep(1000); // Имитация работы
                if (new Random().Next(5) == 0) throw new Exception($"Task {id} failed!");
            }
        }

        public void MonitorAndRestart()
        {
            foreach (var key in _tasks.Keys)
            {
                if (_tasks[key].IsFaulted)
                {
                    Console.WriteLine($"Restarting Task {key}...");
                    _tasks[key] = Task.Factory.StartNew(() => WorkerMethod(key, _cts.Token), _cts.Token);
                }
            }
        }
    }
}
