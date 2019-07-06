namespace LightTrade.Instruments.Threading
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class ObservableExecutor : IExecuteTasks
    {
        private readonly IExecuteTasks _uniqueExecutor;

        private readonly Counter _executionCounter = new Counter();
        
        public ObservableExecutor(IExecuteTasks uniqueExecutor)
        {
            _uniqueExecutor = uniqueExecutor;
        }

        public long ExecutionsCount => _executionCounter.Value;

        public void Dispose()
        {
            _uniqueExecutor.Dispose();
        }

        public Task<object> Execute(Func<Task<object>> func, TimeSpan timeout)
        {
            var result = _uniqueExecutor.Execute(func, timeout);
            _executionCounter.Increment();

            return result;
        }

        public Task<object> Execute(Func<object> func, TimeSpan timeout)
        {
            var result = _uniqueExecutor.Execute(func, timeout);
            _executionCounter.Increment();

            return result;
        }
    }

    public class Counter
    {
        private long _value;

        public long Value => _value;

        public void Increment()
        {
            Interlocked.Increment(ref _value);
        }
    }
}