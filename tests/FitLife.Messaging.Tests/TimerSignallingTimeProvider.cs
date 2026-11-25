using Microsoft.Extensions.Time.Testing;

namespace FitLife.Messaging.Tests;

/// <summary>A <see cref="FakeTimeProvider"/> that signals when code under test starts a timer, such as a <c>Task.Delay</c> on this clock (#356).</summary>
internal sealed class TimerSignallingTimeProvider : FakeTimeProvider
{
    private readonly TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets a task that completes once the first timer is created.</summary>
    public Task TimerCreated => _timerCreated.Task;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = base.CreateTimer(callback, state, dueTime, period);
        _timerCreated.TrySetResult();
        return timer;
    }
}
