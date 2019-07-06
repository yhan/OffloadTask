using System;
using System.Threading.Tasks;

namespace LightTrade.Instruments.Threading
{
    /// <summary>
    /// Executes functions enforcing a threading model (e.g: WorkerThread).
    /// </summary>
    public interface IExecuteTasks : IDisposable
    {
        /// <summary>
        /// Return a task to get the result of a function that will be handled by the executor.
        /// </summary>
        /// <param name="func">The async function to be executed.</param>
        /// <param name="timeout">The timeout for this function execution.</param>
        /// <returns>A task containing the result of the specified function.</returns>
        Task<object> Execute(Func<Task<object>> func, TimeSpan timeout);

        /// <summary>
        /// Return a task to get the result of a function that will be handled by the executor.
        /// </summary>
        /// <param name="func">The function to be executed.</param>
        /// <param name="timeout">The timeout for this function execution.</param>
        /// <returns>A task containing the result of the specified function.</returns>
        Task<object> Execute(Func<object> func, TimeSpan timeout);
    }
}