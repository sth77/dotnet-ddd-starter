using App.Application.Common;
using App.Domain.Common;
using App.Domain.Person;
using App.Domain.Sample;

namespace App.Application.Sample;

/// <summary>
/// Keeps the denormalised <see cref="Domain.Sample.Sample.OwnerName"/> in sync with the Person module (design §7).
/// Idempotent by construction: setting the same name twice is a no-op.
/// </summary>
public sealed class SampleOwnerNameSynchronizer(ISamples samples, IUnitOfWork unitOfWork) : IEventHandler<PersonEvent.Updated>
{
    public async Task HandleAsync(PersonEvent.Updated domainEvent, CancellationToken ct)
    {
        var owner = new Association<Person, PersonId>(domainEvent.PersonId);

        foreach (var sample in await samples.FindByOwnerAsync(owner, ct))
        {
            sample.UpdateOwnerName(new SampleCommand.UpdateOwnerName(domainEvent.Name));
        }

        await unitOfWork.CommitAsync(ct);
    }
}
