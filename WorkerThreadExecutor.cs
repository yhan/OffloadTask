namespace LightTrade.Instruments.Threading
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Execute functions in a dedicated foreground thread.
    /// </summary>
    public class WorkerThreadExecutor : IExecuteTasks
    {
        private TimeSpan _disposeTimeout;

        private bool _mustStop;

        private Queue<CompletableFunction> _queue = new Queue<CompletableFunction>();

        private object _syncRoot = new object();

        private Thread _workerThread;

        public WorkerThreadExecutor(TimeSpan disposeTimeout)
        {
            _disposeTimeout = disposeTimeout;

            _workerThread = new Thread(WorkerRoutine)
                                {
                                    IsBackground = true
                                };
            _workerThread.Start();
        }

        /// <summary>
        /// Return a task to get the result of a function that will be executed by the unique worker thread.
        /// </summary>
        /// <param name="func">The async function to be executed.</param>
        /// <param name="timeout">The timeout for this function execution (when CPU is available for it).</param>
        /// <returns>A task containing the result of the specified function.</returns>
        public async Task<object> Execute(Func<Task<object>> func, TimeSpan timeout)
        {
            var completableFunction = new CompletableFunction(func, timeout);

            // Enqueue the func
            lock (_syncRoot)
            {
                _queue.Enqueue(completableFunction);
                if (_queue.Count == 1)
                {
                    Monitor.Pulse(_syncRoot);
                }
            }

            return await (Task<object>)completableFunction.Task;
        }

        /// <summary>
        /// Return a task to get the result of a function that will be executed by the unique worker thread.
        /// </summary>
        /// <param name="func">The function to be executed.</param>
        /// <param name="timeout">The timeout for this function execution (when CPU is available for it).</param>
        /// <returns>A task containing the result of the specified function.</returns>
        public Task<object> Execute(Func<object> func, TimeSpan timeout)
        {
            var completableFunction = new CompletableFunction(func, timeout);

            // Enqueue the func
            lock (_syncRoot)
            {
                _queue.Enqueue(completableFunction);
                if (_queue.Count == 1)
                {
                    Monitor.Pulse(_syncRoot);
                }
            }

            return (Task<object>)completableFunction.Task;
        }

        public void Dispose()
        {
            _mustStop = true;
            GC.SuppressFinalize(this);

            var joined = _workerThread.Join(_disposeTimeout);
            if (!joined)
            {
                // _workerThread.Abort();
            }
        }

        private async void WorkerRoutine()
        {
            while (!_mustStop)
            {
                CompletableFunction completableFunction;

                lock (_syncRoot)
                {
                    if (_queue.Count == 0)
                    {
                        Monitor.Wait(_syncRoot);
                    }

                    completableFunction = _queue.Dequeue();
                }

                try
                {
                    completableFunction.StartCancellationTimeoutCountdown();
                    var result = await completableFunction.ExecuteFunction();
                    completableFunction.SetResult(result);
                }
                catch (Exception e)
                {
                    completableFunction.SetException(e);
                }
                finally
                {
                    completableFunction.Dispose();
                }
            }
        }

        /// <summary>
        /// Function which can be started and completed/faulted.
        /// </summary>
        private class CompletableFunction : IDisposable
        {
            private readonly TimeSpan _timeout;

            private CancellationTokenRegistration _cancellationTokenRegistration;

            private CancellationTokenSource _ct;

            private TaskCompletionSource<object> _taskCompletionSource;

            public CompletableFunction(Func<object> function, TimeSpan timeout)
            {
                // object can be simple object OR Task<object>
                _timeout = timeout;
                Function = function;

                _taskCompletionSource = new TaskCompletionSource<object>();
            }

            public Func<object> Function { get; }

            public Task Task => _taskCompletionSource.Task;

            public void Dispose()
            {
                _cancellationTokenRegistration.Dispose();
                _ct?.Dispose();
            }

            public void StartCancellationTimeoutCountdown()
            {
                if (!Debugger.IsAttached)
                {
                    _ct = new CancellationTokenSource(_timeout);
                    _cancellationTokenRegistration = _ct.Token.Register(() => _taskCompletionSource.TrySetCanceled(), useSynchronizationContext: false);
                }
            }

            public void SetResult(object result)
            {
                _taskCompletionSource.TrySetResult(result);
            }

            public void SetException(Exception exception)
            {
                _taskCompletionSource.TrySetException(exception);
            }

            public async Task<object> ExecuteFunction()
            {
                if (Function.GetType().GenericTypeArguments[0] == typeof(Task<object>))
                {
                    var task = (Task<object>)Function();
                    return await task;
                }

                return Function();
            }
        }
    }
}