namespace App.Domain.Common;

/// <summary>
/// Base for exceptions that express a domain rule outcome the caller can act on. Mapped to RFC 9457
/// ProblemDetails by the API ring (409 for state conflicts, 404 for missing aggregates, 422 for rule violations).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The aggregate's current state does not permit the requested operation.</summary>
public sealed class OperationNotAllowedException : DomainException
{
    public OperationNotAllowedException(Type aggregateType, Type commandType, string state)
        : base($"{aggregateType.Name} does not allow {commandType.Name} in state {state}.")
    {
        AggregateType = aggregateType;
        CommandType = commandType;
        State = state;
    }

    public Type AggregateType { get; }

    public Type CommandType { get; }

    public string State { get; }
}

/// <summary>The referenced aggregate or reference-data entry does not exist.</summary>
public sealed class AggregateNotFoundException : DomainException
{
    public AggregateNotFoundException(Type aggregateType, object id)
        : base($"{aggregateType.Name} {id} was not found.")
    {
        AggregateType = aggregateType;
        Id = id;
    }

    public Type AggregateType { get; }

    public object Id { get; }
}

/// <summary>An invariant would be violated; the request is well-formed but semantically wrong.</summary>
public sealed class DomainRuleViolationException : DomainException
{
    public DomainRuleViolationException(string rule, string message)
        : base(message)
    {
        Rule = rule;
    }

    public string Rule { get; }
}
