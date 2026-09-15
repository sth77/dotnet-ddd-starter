using App.Api.Common;
using App.Domain.Todo;

namespace App.Api.Todo;

// Explicit response records replace Spring Data projections (design §6.3). Two shapes, two endpoints — no
// `?projection=` parameter.

public sealed record TodoSummary(
    TodoId Id,
    string Name,
    TodoState State) : HalResource;

public sealed record TodoDetail(
    TodoId Id,
    string Name,
    string Description,
    TodoState State,
    DateTimeOffset CreatedAt) : HalResource;

/// <summary>Endpoint names (stable keys for <c>LinkGenerator</c>) and the command → endpoint → rel binding.</summary>
public static class TodoRels
{
    public const string Collection = "Todos";
    public const string Self = "Todo";
    public const string Create = "CreateTodo";
    public const string Update = "UpdateTodo";
    public const string Activate = "ActivateTodo";
    public const string Close = "CloseTodo";

    /// <summary>Commands that have an endpoint; a command absent from this list never becomes a HAL link.</summary>
    public static readonly IReadOnlyList<CommandEndpoint> CommandEndpoints =
    [
        new(typeof(TodoCommand.Update), "update", Update),
        new(typeof(TodoCommand.Activate), "activate", Activate),
        new(typeof(TodoCommand.Close), "close", Close),
    ];
}
