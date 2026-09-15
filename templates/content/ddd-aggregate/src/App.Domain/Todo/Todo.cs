using App.Domain.Common;

namespace App.Domain.Todo;

/// <summary>
/// The Todo aggregate. One command record per operation, a <see cref="Can{TCommand}"/> guard that doubles
/// as the HAL link predicate, and events registered rather than published.
/// </summary>
public sealed class Todo : AggregateRoot<Todo, TodoId>
{
    private Todo(TodoId id, string name, string description, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Description = description;
        CreatedAt = createdAt;
        State = TodoState.Draft;
    }

#pragma warning disable CS8618 // Materialisation constructor: EF Core sets the remaining properties after construction.
    private Todo(TodoId id, DateTimeOffset createdAt)
        : base(id)
    {
        CreatedAt = createdAt;
    }
#pragma warning restore CS8618

    public string Name { get; private set; }

    public string Description { get; private set; }

    public TodoState State { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static Todo Create(TodoCommand.Create data, TimeProvider clock)
    {
        var todo = new Todo(TodoId.New(), data.Name, data.Description, clock.GetUtcNow());

        todo.RegisterEvent(new TodoEvent.Created(todo.Id));
        return todo;
    }

    public void Update(TodoCommand.Update data)
    {
        AssertCan<TodoCommand.Update>();

        Name = data.Name;
        Description = data.Description;

        RegisterEvent(new TodoEvent.Updated(Id, Name, Description));
    }

    public void Activate(TodoCommand.Activate data)
    {
        AssertCan<TodoCommand.Activate>();

        State = TodoState.Active;
        RegisterEvent(new TodoEvent.Activated(Id));
    }

    public void Close(TodoCommand.Close data)
    {
        AssertCan<TodoCommand.Close>();

        State = TodoState.Closed;
        RegisterEvent(new TodoEvent.Closed(Id));
    }

    /// <summary>Whether the aggregate's current state permits the given command. Drives HAL link visibility.</summary>
    public bool Can<TCommand>()
        where TCommand : TodoCommand
        => Can(typeof(TCommand));

    public bool Can(Type command) => State switch
    {
        TodoState.Draft => command == typeof(TodoCommand.Update)
                           || command == typeof(TodoCommand.Activate),
        TodoState.Active => command == typeof(TodoCommand.Update)
                            || command == typeof(TodoCommand.Close),
        _ => false,
    };

    private void AssertCan<TCommand>()
        where TCommand : TodoCommand
    {
        if (!Can<TCommand>())
        {
            throw new OperationNotAllowedException(typeof(Todo), typeof(TCommand), State.ToString());
        }
    }
}
