using System;
using System.Threading;
using System.Threading.Tasks;

namespace MQ.Share.Wraper
{


    public class CancelableAsyncWrapper
    {
        public static Task<T> WrapExternalOperationWithCancellation<T>(
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();

            // Register a callback to cancel the TCS when the token is canceled.
            // The registration object itself should be disposed of later.
            var registration = cancellationToken.Register(() =>
            {
                // Use TrySetCanceled to avoid exceptions if the task is already completed
                tcs.TrySetCanceled(cancellationToken);
            });

            // The task returned to the consumer. 
            // We link the disposal of the registration to the task's completion.
            tcs.Task.ContinueWith(_ => registration.Dispose(),
                                  TaskScheduler.Default);

            // In a real scenario, you would start the external operation here 
            // and link its completion (result/exception) to the TCS.
            // For example: 
            // ExternalLibrary.StartOperation(result => tcs.TrySetResult(result), 
            //                                exception => tcs.TrySetException(exception));

            return tcs.Task;
        }
    }
}