namespace QueryPilot.Api.Configuration;

public static class DemoSeedOptionsValidator
{
    public static void Validate(DemoSeedOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction() && options.Enabled)
        {
            throw new InvalidOperationException(
                "DemoSeed:Enabled must be false in Production.");
        }
    }
}
