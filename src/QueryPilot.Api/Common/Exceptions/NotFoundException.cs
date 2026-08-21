namespace QueryPilot.Api.Common.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);
