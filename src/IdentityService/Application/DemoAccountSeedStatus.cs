namespace IdentityService.Application;

/// <summary>Whether the demo accounts are seeded. <c>/health</c> reads it, so docker compose waits for the accounts.</summary>
public sealed class DemoAccountSeedStatus
{
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Whether every demo account exists.</summary>
    public bool HasFinished => _finished.Task.IsCompleted;

    /// <summary>Completes when every demo account exists.</summary>
    public Task Finished => _finished.Task;

    /// <summary>Records that every demo account exists.</summary>
    public void MarkFinished()
    {
        _finished.TrySetResult();
    }
}
