using Xunit;

namespace QueryPilot.Api.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection
    : ICollectionFixture<PostgreSqlDatabaseFixture>
{
    public const string Name = "Disposable PostgreSQL database";
}
