using Xunit;

namespace QueryPilot.Api.IntegrationTests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                PostgreSqlDatabaseFixture.ResolveConfiguredConnectionString()))
        {
            Skip = $"Set {PostgreSqlDatabaseFixture.ConnectionUserSecret} as a user-secret "
                + $"or {PostgreSqlDatabaseFixture.ConnectionEnvironmentVariable}.";
        }
    }
}
