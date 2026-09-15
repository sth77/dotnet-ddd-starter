using App.Api.Common;
using App.Domain.Common;
using App.Domain.Person;
using App.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Person;

public sealed record PersonDetail(PersonId Id, string Name, EmailAddress Email) : HalResource;

public static class PersonRels
{
    public const string Collection = "People";
    public const string Self = "Person";
    public const string Create = "CreatePerson";
    public const string Update = "UpdatePerson";

    public static readonly IReadOnlyList<CommandEndpoint> CommandEndpoints =
    [
        new(typeof(PersonCommand.Update), "update", Update),
    ];
}

internal sealed class PersonEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/people").WithTags("People");

        group.MapGet("/", ListAsync).WithName(PersonRels.Collection);
        group.MapGet("/{id}", GetAsync).WithName(PersonRels.Self);

        group.MapPost("/", CreateAsync)
            .WithName(PersonRels.Create)
            .RequireAuthorization(Policies.User);

        group.MapPut("/{id}", UpdateAsync)
            .WithName(PersonRels.Update)
            .RequireAuthorization(Policies.User);
    }

    private static async Task<Ok<HalCollection<PersonDetail>>> ListAsync(AppDbContext db, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var people = await db.People.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

        var items = new List<PersonDetail>(people.Count);
        foreach (var person in people)
        {
            items.Add(await Detail(person, links, http));
        }

        return TypedResults.Ok(new HalCollection<PersonDetail>("people", items, links.Collection(http, PersonRels.Collection)));
    }

    private static async Task<Results<Ok<PersonDetail>, NotFound>> GetAsync(PersonId id, AppDbContext db, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var person = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return person is null ? TypedResults.NotFound() : TypedResults.Ok(await Detail(person, links, http));
    }

    private static async Task<Created<PersonDetail>> CreateAsync(
        PersonCommand.Create command, IPeople people, IUnitOfWork unitOfWork, HalLinks links, LinkGenerator linkGenerator, HttpContext http, CancellationToken ct)
    {
        var person = Domain.Person.Person.Create(command);
        people.Add(person);
        await unitOfWork.CommitAsync(ct);

        var location = linkGenerator.GetPathByName(http, PersonRels.Self, new { id = person.Id });
        return TypedResults.Created(location, await Detail(person, links, http));
    }

    private static async Task<Ok<PersonDetail>> UpdateAsync(
        PersonId id, PersonCommand.Update command, IPeople people, IUnitOfWork unitOfWork, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var person = await people.GetRequiredAsync(id, ct);

        person.Update(command);
        await unitOfWork.CommitAsync(ct);

        return TypedResults.Ok(await Detail(person, links, http));
    }

    private static async Task<PersonDetail> Detail(Domain.Person.Person person, HalLinks links, HttpContext http)
        => new(person.Id, person.Name, person.Email)
        {
            Links = await links.ForAsync(http, PersonRels.Self, new { id = person.Id }, PersonRels.CommandEndpoints, person.Can),
        };
}
