using ClickHouse.Client.Utility;
using MongoDB.Driver.Linq;
using MQ.bll.Common;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
namespace MQ.bll
{
    public class ThreadManagerAsync : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cts;
        private readonly CancellationToken _cancellationToken;
        private ConcurrentDictionary<string, Task> _tasks;
        private ServiceMsgSettings _sms;
        private ConcurrentDictionary<string, ReceiveAllMessages> _workers = new ConcurrentDictionary<string, ReceiveAllMessages>();

        public ThreadManagerAsync(ServiceMsgSettings sms, CancellationTokenSource cts)
        {
            int maxDegreeOfParallelism = sms.Workers.Length;
            _sms = sms;
            _cts = cts;
            _cancellationToken = _cts.Token;
            foreach (var bo in sms.Workers)
            {
                if(bo.IsEnabled)
                  if (!_workers.TryAdd(bo.Name, new ReceiveAllMessages(bo, _cancellationToken)))
                    throw new Exception($"Worker name {bo.Name} must be unique.");
            }
            _semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            
            _tasks = new ConcurrentDictionary<string, Task>();
        }

        public ReceiveAllMessages GetWorker(string? key = null)
        {
            if(string.IsNullOrEmpty(key))  
                return _workers.FirstOrDefault().Value;
            return _workers[key];
        }

        public async Task RunAsync(string key)
        {
            await _semaphore.WaitAsync(); // Limit concurrency

            var task = Task.Run(async () =>
            {
                try
                {
                    await _workers[key].ProcessLauncherAsync();
                }
                catch (Exception ex)
                {
                    Log.Error($"Finish worker {key} processing caused an exception:");
                    Log.Error(ex.Message);
                }
                finally
                {
                    _semaphore.Release();
                }
            }, _cancellationToken);

            _tasks[key] = task;
        }
        public Task TaskCompletionSourceWithCancelation(CancellationToken cancellationToken)
        {

            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => tcs.SetResult(true), tcs);
            return tcs.Task;
        }
        public async Task MonitorAndRestart()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                foreach (var key in _workers.Keys)
                {
                    if (!_tasks.ContainsKey(key) || _tasks[key].IsFaulted || _tasks[key].Status == TaskStatus.RanToCompletion)
                    {
                        Log.Information($"Restarting Task {key}...");
                        await RunAsync(key);

                    }
                }

                await Task.Delay(50000, _cancellationToken);
            }
        }
        //public async Task WaitForAllAsync()
        //{
        //    object value = await Task.WhenAll(_tasks.ToArray);
        //}

        public void CancelAll()
        {
            _cts.Cancel();
  
         }

        public void Dispose()
        {
            _semaphore.Dispose();

        }
    }
}
