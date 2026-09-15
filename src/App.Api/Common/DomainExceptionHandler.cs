using App.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Common;

/// <summary>
/// Maps domain outcomes to RFC 9457 ProblemDetails (design §6.5): 404 for a missing aggregate, 409 for a state
/// or concurrency conflict, 422 for a rule violation. Anything else falls through to the default handler (500).
/// </summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, type, title) = exception switch
        {
            AggregateNotFoundException => (StatusCodes.Status404NotFound, "not-found", "Not found"),
            OperationNotAllowedException => (StatusCodes.Status409Conflict, "operation-not-allowed", "Operation not allowed in the current state"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrency-conflict", "The resource was modified concurrently"),
            DomainRuleViolationException => (StatusCodes.Status422UnprocessableEntity, "rule-violation", "Domain rule violated"),
            ValueObjectValidationException => (StatusCodes.Status422UnprocessableEntity, "invalid-value", "Invalid value"),
            BadHttpRequestException bad when bad.InnerException is ValueObjectValidationException
                => (StatusCodes.Status422UnprocessableEntity, "invalid-value", "Invalid value"),
            _ => (0, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;

        var detail = exception is BadHttpRequestException { InnerException: { } inner } ? inner.Message : exception.Message;
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Type = $"urn:problem-type:{type}",
            Title = title,
            Detail = detail,
        };

        if (exception is OperationNotAllowedException notAllowed)
        {
            problem.Extensions["aggregate"] = notAllowed.AggregateType.Name;
            problem.Extensions["command"] = notAllowed.CommandType.Name;
            problem.Extensions["state"] = notAllowed.State;
        }

        if (exception is DomainRuleViolationException violation)
        {
            problem.Extensions["rule"] = violation.Rule;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
