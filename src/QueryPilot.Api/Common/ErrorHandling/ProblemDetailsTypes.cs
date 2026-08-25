namespace QueryPilot.Api.Common.ErrorHandling;

public static class ProblemDetailsTypes
{
    public const string BadRequest =
        "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    public const string NotFound =
        "https://tools.ietf.org/html/rfc9110#section-15.5.5";

    public const string MethodNotAllowed =
        "https://tools.ietf.org/html/rfc9110#section-15.5.6";

    public const string UnsupportedMediaType =
        "https://tools.ietf.org/html/rfc9110#section-15.5.16";

    public const string Conflict =
        "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    public const string InternalServerError =
        "https://tools.ietf.org/html/rfc9110#section-15.6.1";

    public const string ServiceUnavailable =
        "https://tools.ietf.org/html/rfc9110#section-15.6.4";

    public static string ForStatusCode(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => BadRequest,
        StatusCodes.Status404NotFound => NotFound,
        StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
        StatusCodes.Status409Conflict => Conflict,
        StatusCodes.Status500InternalServerError => InternalServerError,
        StatusCodes.Status503ServiceUnavailable => ServiceUnavailable,
        _ => "about:blank"
    };

    public static string GetTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad request.",
        StatusCodes.Status404NotFound => "Resource not found.",
        StatusCodes.Status405MethodNotAllowed => "Method not allowed.",
        StatusCodes.Status415UnsupportedMediaType => "Unsupported media type.",
        StatusCodes.Status409Conflict =>
            "The request conflicts with the current state.",
        StatusCodes.Status500InternalServerError =>
            "An unexpected error occurred.",
        StatusCodes.Status503ServiceUnavailable =>
            "Service temporarily unavailable.",
        _ => "HTTP request failed."
    };
}
