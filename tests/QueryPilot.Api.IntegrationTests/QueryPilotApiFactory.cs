using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QueryPilot.Api.Features.Ai;

namespace QueryPilot.Api.IntegrationTests;

public sealed class QueryPilotApiFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlDatabaseFixture database;

    public QueryPilotApiFactory(PostgreSqlDatabaseFixture database)
    {
        this.database = database;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            database.TestConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiService>();
            services.AddSingleton<IAiService>(new FakeAiService(
                new AiQuestionUnderstanding(
                    AiAnalysisType.Unknown,
                    Period: null,
                    From: null,
                    To: null,
                    ProductName: null,
                    CategoryName: null,
                    Granularity: null,
                    Metric: null,
                    Limit: null),
                new AiResultExplanation("unused")));
        });
    }

    public WebApplicationFactory<Program> WithTestServices(
        Action<IServiceCollection> configureServices) =>
        WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(configureServices));
}
