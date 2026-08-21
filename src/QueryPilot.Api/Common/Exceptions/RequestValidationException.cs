namespace QueryPilot.Api.Common.Exceptions;

public sealed class RequestValidationException : Exception
{
    public RequestValidationException(
        IReadOnlyDictionary<string, string[]> errors,
        string message = "One or more validation errors occurred.")
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
