using App.Application.Common;
using App.Domain.Common;
using App.Domain.Person;
using App.Domain.Sample;
using Microsoft.Extensions.Logging;

namespace App.Application.Sample;

/// <summary>
/// Keeps the denormalised <see cref="Domain.Sample.Sample.OwnerName"/> in sync with the Person module (design §7).
/// Idempotent by construction: setting the same name twice is a no-op.
/// <para>
/// It runs unattended, after a transaction it did not open, and it is the only place where the outcome of that
/// decision exists — which is why the application ring may inject <see cref="ILogger{TCategoryName}"/> (ADR-019).
/// </para>
/// </summary>
public sealed partial class SampleOwnerNameSynchronizer(
    ISamples samples,
    IUnitOfWork unitOfWork,
    ILogger<SampleOwnerNameSynchronizer> logger) : IEventHandler<PersonEvent.Updated>
{
    public async Task HandleAsync(PersonEvent.Updated domainEvent, CancellationToken ct)
    {
        var owner = new Association<Person, PersonId>(domainEvent.PersonId);
        var affected = await samples.FindByOwnerAsync(owner, ct);

        foreach (var sample in affected)
        {
            sample.UpdateOwnerName(new SampleCommand.UpdateOwnerName(domainEvent.Name));
        }

        if (affected.Count > 0)
        {
            LogSynchronised(affected.Count, domainEvent.PersonId.Value);
        }

        await unitOfWork.CommitAsync(ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Refreshed the owner name on {Count} sample(s) of person {PersonId}")]
    partial void LogSynchronised(int count, Guid personId);
}
