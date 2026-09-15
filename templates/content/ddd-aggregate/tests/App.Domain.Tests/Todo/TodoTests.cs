using App.Domain.Common;
using App.Domain.Todo;
using Microsoft.Extensions.Time.Testing;

namespace App.Domain.Tests;

public sealed class TodoTests
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero));

    private static Domain.Todo.Todo Draft()
        => Domain.Todo.Todo.Create(new TodoCommand.Create("Name", "Description"), Clock);

    [Fact]
    public void Create_starts_in_draft_stamps_the_clock_and_registers_the_created_event()
    {
        var todo = Draft();

        Assert.Equal(TodoState.Draft, todo.State);
        Assert.Equal("Name", todo.Name);
        Assert.Equal("Description", todo.Description);
        Assert.Equal(Clock.GetUtcNow(), todo.CreatedAt);
        var created = Assert.IsType<TodoEvent.Created>(Assert.Single(todo.PendingEvents));
        Assert.Equal(todo.Id, created.TodoId);
    }

    [Fact]
    public void Activate_moves_draft_to_active_and_registers_the_event()
    {
        var todo = Draft();

        todo.Activate(new TodoCommand.Activate());

        Assert.Equal(TodoState.Active, todo.State);
        Assert.IsType<TodoEvent.Activated>(todo.PendingEvents[^1]);
    }

    [Fact]
    public void Close_requires_an_active_entry()
    {
        var todo = Draft();

        Assert.Throws<OperationNotAllowedException>(() => todo.Close(new TodoCommand.Close()));

        todo.Activate(new TodoCommand.Activate());
        todo.Close(new TodoCommand.Close());

        Assert.Equal(TodoState.Closed, todo.State);
    }

    [Fact]
    public void Update_is_not_allowed_once_closed()
    {
        var todo = Draft();
        todo.Activate(new TodoCommand.Activate());
        todo.Update(new TodoCommand.Update("New", "Changed"));

        Assert.Equal("New", todo.Name);
        Assert.Equal("New", Assert.IsType<TodoEvent.Updated>(todo.PendingEvents[^1]).Name);

        todo.Close(new TodoCommand.Close());

        var ex = Assert.Throws<OperationNotAllowedException>(() => todo.Update(new TodoCommand.Update("Later", "Too late")));

        Assert.Equal(typeof(TodoCommand.Update), ex.CommandType);
        Assert.Equal("Closed", ex.State);
    }

    [Theory]
    [InlineData(TodoState.Draft, typeof(TodoCommand.Update), true)]
    [InlineData(TodoState.Draft, typeof(TodoCommand.Activate), true)]
    [InlineData(TodoState.Draft, typeof(TodoCommand.Close), false)]
    [InlineData(TodoState.Active, typeof(TodoCommand.Activate), false)]
    [InlineData(TodoState.Active, typeof(TodoCommand.Close), true)]
    [InlineData(TodoState.Closed, typeof(TodoCommand.Update), false)]
    public void Can_reflects_the_state_machine(TodoState state, Type command, bool expected)
    {
        var todo = Draft();
        if (state is TodoState.Active or TodoState.Closed)
        {
            todo.Activate(new TodoCommand.Activate());
        }

        if (state is TodoState.Closed)
        {
            todo.Close(new TodoCommand.Close());
        }

        Assert.Equal(expected, todo.Can(command));
    }

    [Fact]
    public void Aggregates_are_equal_by_identity()
    {
        var a = Draft();
        var b = Draft();

        Assert.NotEqual(a, b);
        Assert.Equal(a, a);
    }
}
