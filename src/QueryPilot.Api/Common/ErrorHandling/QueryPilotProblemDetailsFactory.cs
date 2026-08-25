using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace QueryPilot.Api.Common.ErrorHandling;

public sealed class QueryPilotProblemDetailsFactory : ProblemDetailsFactory
{
    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        var resolvedStatus = statusCode
            ?? StatusCodes.Status500InternalServerError;
        var problemDetails = new ProblemDetails
        {
            Status = resolvedStatus,
            Title = title ?? ProblemDetailsTypes.GetTitle(resolvedStatus),
            Type = type ?? ProblemDetailsTypes.ForStatusCode(resolvedStatus),
            Detail = detail,
            Instance = instance ?? httpContext.Request.Path
        };
        AddTraceId(problemDetails, httpContext);
        return problemDetails;
    }

    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(modelStateDictionary);

        var resolvedStatus = statusCode ?? StatusCodes.Status400BadRequest;
        var problemDetails = new ValidationProblemDetails(modelStateDictionary)
        {
            Status = resolvedStatus,
            Title = title ?? "Validation failed.",
            Type = type ?? ProblemDetailsTypes.ForStatusCode(resolvedStatus),
            Detail = detail,
            Instance = instance ?? httpContext.Request.Path
        };
        AddTraceId(problemDetails, httpContext);
        return problemDetails;
    }

    private static void AddTraceId(
        ProblemDetails problemDetails,
        HttpContext httpContext)
    {
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
    }
}
