using Microsoft.Extensions.Logging;

namespace WafeControl.Core.Threading;

public static class TaskExtensions
{
    /// <summary>
    /// Runs a task nobody waits for, without losing its failure: an exception is logged instead of going unobserved.
    /// Use instead of <c>_ = SomethingAsync();</c>.
    /// </summary>
    public static void Forget(this Task task, ILogger logger, string operation)
    {
        if (task.IsCompleted)
        {
            Observe(task, logger, operation);
            return;
        }

        _ = task.ContinueWith(t => Observe(t, logger, operation), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static void Observe(Task task, ILogger logger, string operation)
    {
        if (task.IsFaulted)
            logger.LogError(task.Exception!.GetBaseException(), "{Operation} failed", operation);
    }
}
