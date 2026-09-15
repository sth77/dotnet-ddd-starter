namespace App.Domain.Common;

/// <summary>Thrown by Vogen value objects when <c>From(...)</c> receives an invalid primitive.</summary>
public sealed class ValueObjectValidationException : DomainException
{
    public ValueObjectValidationException(string message)
        : base(message)
    {
    }

    public ValueObjectValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
