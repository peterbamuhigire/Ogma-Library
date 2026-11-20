namespace OgmaLibrary.Infrastructure.AI.Onnx;

/// <summary>
/// Runs background inference on a dedicated below-normal-priority thread so passage
/// embedding yields the CPU to the interface and the reader (Sept-23 Phase 14, slice 4).
/// ONNX Runtime's own intra-op pool threads keep their default priority (defect K142).
/// </summary>
internal static class BelowNormalPriorityRunner
{
    /// <summary>Runs <paramref name="work"/> on a new below-normal-priority background thread.</summary>
    public static Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(work());
            }
            catch (OperationCanceledException exception)
            {
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                completion.TrySetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Ogma semantic indexing",
            Priority = ThreadPriority.BelowNormal,
        };
        thread.Start();
        return completion.Task;
    }
}
