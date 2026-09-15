using App.Api.Common;
using App.Domain.Common;
using App.Domain.Todo;
using App.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Todo;

/// <summary>
/// Endpoint module for the Todo aggregate. Reads may use the DbContext directly; every state change goes
/// through the aggregate and <see cref="IUnitOfWork"/> (design §6.3). State-changing routes carry a policy,
/// which the startup check enforces and HAL link generation reads.
/// </summary>
internal sealed class TodoEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/todos").WithTags("Todos");

        group.MapGet("/", ListAsync).WithName(TodoRels.Collection);
        group.MapGet("/{id}", GetAsync).WithName(TodoRels.Self);

        group.MapPost("/", CreateAsync)
            .WithName(TodoRels.Create)
            .RequireAuthorization(Policies.User);

        group.MapPut("/{id}", UpdateAsync)
            .WithName(TodoRels.Update)
            .RequireAuthorization(Policies.User);

        group.MapPost("/{id}/activate", ActivateAsync)
            .WithName(TodoRels.Activate)
            .RequireAuthorization(Policies.Admin);

        group.MapPost("/{id}/close", CloseAsync)
            .WithName(TodoRels.Close)
            .RequireAuthorization(Policies.Admin);
    }

    private static async Task<Ok<HalCollection<TodoSummary>>> ListAsync(
        AppDbContext db, HalLinks links, HttpContext http, CancellationToken ct)
    {
        // Read path: straight from the DbContext, no tracking. The aggregate is loaded (not projected) because
        // its Can(...) predicate drives the per-item link set.
        var todos = await db.Set<Domain.Todo.Todo>().AsNoTracking().OrderBy(x => x.CreatedAt).ToListAsync(ct);

        var items = new List<TodoSummary>(todos.Count);
        foreach (var todo in todos)
        {
            items.Add(new TodoSummary(todo.Id, todo.Name, todo.State)
            {
                Links = await LinksFor(todo, links, http),
            });
        }

        return TypedResults.Ok(new HalCollection<TodoSummary>("todos", items, links.Collection(http, TodoRels.Collection)));
    }

    private static async Task<Results<Ok<TodoDetail>, NotFound>> GetAsync(
        TodoId id, AppDbContext db, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var todo = await db.Set<Domain.Todo.Todo>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return todo is null ? TypedResults.NotFound() : TypedResults.Ok(await Detail(todo, links, http));
    }

    private static async Task<Created<TodoDetail>> CreateAsync(
        TodoCommand.Create command,
        ITodos todos,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        HalLinks links,
        LinkGenerator linkGenerator,
        HttpContext http,
        CancellationToken ct)
    {
        var todo = Domain.Todo.Todo.Create(command, clock);
        todos.Add(todo);
        await unitOfWork.CommitAsync(ct);

        var location = linkGenerator.GetPathByName(http, TodoRels.Self, new { id = todo.Id });
        return TypedResults.Created(location, await Detail(todo, links, http));
    }

    private static async Task<Ok<TodoDetail>> UpdateAsync(
        TodoId id,
        TodoCommand.Update command,
        ITodos todos,
        IUnitOfWork unitOfWork,
        HalLinks links,
        HttpContext http,
        CancellationToken ct)
    {
        var todo = await todos.GetRequiredAsync(id, ct);

        todo.Update(command);
        await unitOfWork.CommitAsync(ct);

        return TypedResults.Ok(await Detail(todo, links, http));
    }

    private static async Task<Ok<TodoDetail>> ActivateAsync(
        TodoId id, ITodos todos, IUnitOfWork unitOfWork, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var todo = await todos.GetRequiredAsync(id, ct);

        todo.Activate(new TodoCommand.Activate());
        await unitOfWork.CommitAsync(ct);

        return TypedResults.Ok(await Detail(todo, links, http));
    }

    private static async Task<Ok<TodoDetail>> CloseAsync(
        TodoId id, ITodos todos, IUnitOfWork unitOfWork, HalLinks links, HttpContext http, CancellationToken ct)
    {
        var todo = await todos.GetRequiredAsync(id, ct);

        todo.Close(new TodoCommand.Close());
        await unitOfWork.CommitAsync(ct);

        return TypedResults.Ok(await Detail(todo, links, http));
    }

    private static async Task<TodoDetail> Detail(Domain.Todo.Todo todo, HalLinks links, HttpContext http)
        => new(todo.Id, todo.Name, todo.Description, todo.State, todo.CreatedAt)
        {
            Links = await LinksFor(todo, links, http),
        };

    private static Task<IReadOnlyDictionary<string, HalLink>> LinksFor(Domain.Todo.Todo todo, HalLinks links, HttpContext http)
        => links.ForAsync(http, TodoRels.Self, new { id = todo.Id }, TodoRels.CommandEndpoints, todo.Can);
}
