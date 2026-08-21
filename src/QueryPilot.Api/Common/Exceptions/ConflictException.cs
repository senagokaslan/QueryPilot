namespace QueryPilot.Api.Common.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
