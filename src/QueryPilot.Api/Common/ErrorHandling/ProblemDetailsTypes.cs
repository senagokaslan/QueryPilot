namespace QueryPilot.Api.Common.ErrorHandling;

public static class ProblemDetailsTypes
{
    public const string BadRequest =
        "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    public const string NotFound =
        "https://tools.ietf.org/html/rfc9110#section-15.5.5";

    public const string Conflict =
        "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    public const string InternalServerError =
        "https://tools.ietf.org/html/rfc9110#section-15.6.1";

    public const string ServiceUnavailable =
        "https://tools.ietf.org/html/rfc9110#section-15.6.4";
}
