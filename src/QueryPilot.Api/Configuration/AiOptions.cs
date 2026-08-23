namespace QueryPilot.Api.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public const int MinimumTimeoutSeconds = 1;

    public const int MaximumTimeoutSeconds = 120;

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public string ApiKey { get; set; } = string.Empty;
}
