namespace QueryPilot.Api.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public const string FrontendPolicyName = "QueryPilotFrontend";

    public string[] AllowedOrigins { get; set; } = [];

    public bool AllowCredentials { get; set; }
}
