using FluentValidation;
using JetReportDesigner.Storage.Repositories;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Infrastructure;

/// <summary>
/// Maps known exceptions to RFC 7807 responses. Anything unrecognised is left for
/// the framework's default handler (500 with no detail leak).
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, errors) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status422UnprocessableEntity,
                "The report definition is invalid.",
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            ReportConcurrencyException => (
                StatusCodes.Status409Conflict,
                "The report was modified by another writer.",
                (Dictionary<string, string[]>?)null),
            NotSupportedException => (
                StatusCodes.Status501NotImplemented,
                "That report feature is not implemented yet.",
                (Dictionary<string, string[]>?)null),
            _ => (0, string.Empty, null),
        };

        if (status == 0)
        {
            return false;
        }

        if (status == StatusCodes.Status409Conflict)
        {
            logger.LogWarning(exception, "Concurrency conflict handled.");
        }

        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.io/{status}",
        };
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
