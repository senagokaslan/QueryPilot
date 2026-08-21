using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QueryPilot.Api.Common.Exceptions;

namespace QueryPilot.Api.Common.ErrorHandling;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var problemDetails = CreateProblemDetails(httpContext, exception, traceId);

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            // Do not log the exception message, request body, query string, headers,
            // credentials, e-mail addresses, or other request content.
            logger.LogError(
                "Unhandled {ExceptionType} while processing {Method} {Path}. TraceId: {TraceId}",
                exception.GetType().FullName,
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                traceId);
        }

        httpContext.Response.StatusCode = problemDetails.Status
            ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        Exception exception,
        string traceId)
    {
        ProblemDetails problemDetails = exception switch
        {
            RequestValidationException validationException =>
                new HttpValidationProblemDetails(
                    validationException.Errors.ToDictionary(
                        error => error.Key,
                        error => error.Value))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed.",
                    Detail = validationException.Message,
                    Type = ProblemDetailsTypes.BadRequest
                },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = exception.Message,
                Type = ProblemDetailsTypes.NotFound
            },
            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The request conflicts with the current state.",
                Detail = exception.Message,
                Type = ProblemDetailsTypes.Conflict
            },
            AiServiceUnavailableException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "AI service is temporarily unavailable.",
                Detail = exception.Message,
                Type = ProblemDetailsTypes.ServiceUnavailable
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "The server could not complete the request.",
                Type = ProblemDetailsTypes.InternalServerError
            }
        };

        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }
}
