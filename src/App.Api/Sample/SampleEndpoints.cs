using App.Api.Common;
using App.Domain.Common;
using App.Domain.Person;
using App.Domain.ReferenceData;
using App.Domain.Sample;
using App.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Sample;

/// <summary>
/// Endpoint module for the Sample aggregate. Reads may use the DbContext directly; every state change goes
/// through the aggregate and <see cref="IUnitOfWork"/> (design §6.3). State-changing routes carry a policy,
/// which the startup check enforces and HAL link generation reads.
/// </summary>
internal sealed class SampleEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/samples").WithTags("Samples");

        group.MapGet("/", ListAsync).WithName(SampleRels.Collection);
        group.MapGet("/{id}", GetAsync).WithName(SampleRels.Self);

        group.MapPost("/", CreateAsync)
            .WithName(SampleRels.Create)
            .RequireAuthorization(Policies.User);

        group.MapPut("/{id}", UpdateAsync)
            .WithName(SampleRels.Update)
            .RequireAuthorization(Policies.User);

        group.MapPost("/{id}/publish", PublishAsync)
            .WithName(SampleRels.Publish)
            .RequireAuthorization(Policies.Admin);

        group.MapPost("/{id}/archive", ArchiveAsync)
            .WithName(SampleRels.Archive)
            .RequireAuthorization(Policies.Admin);
    }

    private static async Task<Ok<HalCollection<SampleSummary>>> ListAsync(
        AppDbContext db, HalLinks links, HttpContext http, CancellationToken ct)
    {
        // Read path: straight from the DbContext, no tracking. The aggregate is loaded (not projected) because
        // its Can(...) predicate drives the per-item link set.
        var samples = await db.Samples.AsNoTracking().OrderBy(s => s.CreatedAt).ToListAsync(ct);

        var items = new List<SampleSummary>(samples.Count);
        foreach (var sample in samples)
        {
            items.Add(new SampleSummary(sample.Id, sample.Name, sample.State, sample.OwnerName)
            {
                Links = await LinksFor(sample, links, http),
            });
        }

        return TypedResults.Ok(new HalCollection<SampleSummary>("samples", items, links.Collection(http, SampleRels.Collection)));
    }

    private static async Task<Results<Ok<SampleDetail>, NotFound>> GetAsync(
        SampleId id, AppDbContext db, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var sample = await db.Samples.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return sample is null ? TypedResults.NotFound() : TypedResults.Ok(await Detail(sample, links, http));
    }

    private static async Task<Created<SampleDetail>> CreateAsync(
        SampleCommand.Create command,
        ISamples samples,
        IPeople people,
        ICities cities,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        HalLinks links,
        LinkGenerator linkGenerator,
        HttpContext http,
        CancellationToken ct)
    {
        var owner = await people.GetRequiredAsync(command.Owner, ct);
        var city = command.City is { } cityId ? await cities.GetRequiredAsync(cityId, ct) : null;

        var sample = Domain.Sample.Sample.Create(command, city, owner, clock);
        samples.Add(sample);
        await unitOfWork.CommitAsync(ct);

        var location = linkGenerator.GetPathByName(http, SampleRels.Self, new { id = sample.Id });
        return TypedResults.Created(location, await Detail(sample, links, http));
    }

    private static async Task<Ok<SampleDetail>> UpdateAsync(
        SampleId id,
        SampleCommand.Update command,
        ISamples samples,
        ICities cities,
        IUnitOfWork unitOfWork,
        HalLinks links,
        HttpContext http,
        CancellationToken ct)
    {
        var sample = await samples.GetRequiredAsync(id, ct);
        var city = command.City is { } cityId ? await cities.GetRequiredAsync(cityId, ct) : null;

        sample.Update(command, city);
        await unitOfWork.CommitAsync(ct);

        return TypedResults.Ok(await Detail(sample, links, http));
    }

    private static async Task<Ok<SampleDetail>> PublishAsync(
        SampleId id, ISamples samples, IUnitOfWork unitOfWork, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var sample = await samples.GetRequiredAsync(id, ct);

        sample.Publish(new SampleCommand.Publish());
        await unitOfWork.CommitAsync(ct);

        return TypedResults.Ok(await Detail(sample, links, http));
    }

    private static async Task<Ok<SampleDetail>> ArchiveAsync(
        SampleId id, ISamples samples, IUnitOfWork unitOfWork, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var sample = await samples.GetRequiredAsync(id, ct);

        sample.Archive(new SampleCommand.Archive());
        await unitOfWork.CommitAsync(ct);

        return TypedResults.Ok(await Detail(sample, links, http));
    }

    private static async Task<SampleDetail> Detail(Domain.Sample.Sample sample, HalLinks links, HttpContext http)
        => new(sample.Id, sample.Name, sample.Description, sample.City, sample.State, sample.Owner.Id, sample.OwnerName, sample.CreatedAt)
        {
            Links = await LinksFor(sample, links, http),
        };

    private static Task<IReadOnlyDictionary<string, HalLink>> LinksFor(Domain.Sample.Sample sample, HalLinks links, HttpContext http)
        => links.ForAsync(http, SampleRels.Self, new { id = sample.Id }, SampleRels.CommandEndpoints, sample.Can);
}
