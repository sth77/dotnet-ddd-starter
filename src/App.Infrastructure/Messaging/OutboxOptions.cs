using System.ComponentModel.DataAnnotations;

namespace App.Infrastructure.Messaging;

/// <summary>Bound from <c>Outbox</c>; validated on start.</summary>
public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    /// <summary>How long the dispatcher sleeps when there is nothing to do and nobody signalled a commit.</summary>
    [Range(typeof(TimeSpan), "00:00:00.050", "00:05:00")]
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    [Range(1, 1000)]
    public int BatchSize { get; init; } = 32;

    /// <summary>A claimed message becomes reclaimable after this; must exceed the slowest handler.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan LockDuration { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>After this many failed attempts the message is dead-lettered (<c>failed_at</c> set) and logged as an error.</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; init; } = 8;

    /// <summary>Retry with cooldown: delay before attempt n+1 after n failures (the last entry repeats).</summary>
    public IReadOnlyList<TimeSpan> Backoff { get; init; } =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
    ];

    public TimeSpan BackoffFor(int attempts) => Backoff[Math.Clamp(attempts, 1, Backoff.Count) - 1];
}

/// <summary>
/// Lets a committing unit of work wake the dispatcher immediately instead of waiting for the next poll, so
/// in-process delivery latency is milliseconds while the poll interval can stay coarse.
/// </summary>
public sealed class OutboxSignal : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Notify()
    {
        if (_signal.CurrentCount == 0)
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // a notification is already pending
            }
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);

    public void Dispose() => _signal.Dispose();
}
