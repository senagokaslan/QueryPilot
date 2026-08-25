using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QueryPilot.Api.Configuration;
using Xunit;

namespace QueryPilot.Api.Tests.Configuration;

public sealed class DemoSeedOptionsValidatorTests
{
    [Fact]
    public void Production_rejects_enabled_demo_seed()
    {
        var options = new DemoSeedOptions { Enabled = true };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DemoSeedOptionsValidator.Validate(
                options,
                new TestHostEnvironment("Production")));

        Assert.Equal(
            "DemoSeed:Enabled must be false in Production.",
            exception.Message);
    }

    [Fact]
    public void Production_accepts_disabled_demo_seed()
    {
        var options = new DemoSeedOptions { Enabled = false };

        DemoSeedOptionsValidator.Validate(
            options,
            new TestHostEnvironment("Production"));
    }

    [Fact]
    public void Development_allows_enabled_demo_seed()
    {
        var options = new DemoSeedOptions { Enabled = true };

        DemoSeedOptionsValidator.Validate(
            options,
            new TestHostEnvironment("Development"));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "QueryPilot.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
