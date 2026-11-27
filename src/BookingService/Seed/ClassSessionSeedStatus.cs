namespace BookingService.Seed;

/// <summary>Whether the demo data is loaded (#255). <see cref="ClassSessionSeedHealthCheck"/> keeps /health unhealthy until it is.</summary>
public sealed class ClassSessionSeedStatus
{
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a value indicating whether the seed has finished.</summary>
    public bool HasFinished => _finished.Task.IsCompleted;

    /// <summary>Gets a task that completes when the seed has finished.</summary>
    public Task Finished => _finished.Task;

    /// <summary>Marks the seed as finished.</summary>
    public void MarkFinished()
    {
        _finished.TrySetResult();
    }
}
