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
    private readonly string environment;
    private readonly IReadOnlyDictionary<string, string?> settings;

    public QueryPilotApiFactory(
        PostgreSqlDatabaseFixture database,
        string environment = "Testing",
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        this.database = database;
        this.environment = environment;
        this.settings = settings ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            database.TestConnectionString);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
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
