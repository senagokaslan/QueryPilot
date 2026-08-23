using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QueryPilot.Api.Configuration;
using Xunit;

namespace QueryPilot.Api.Tests.Configuration;

public sealed class CorsOptionsValidatorTests
{
    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("https://frontend.example.com/path")]
    public void Wildcard_or_non_origin_value_is_rejected_even_with_credentials(
        string origin)
    {
        var options = new CorsOptions
        {
            AllowedOrigins = [origin],
            AllowCredentials = true
        };

        Assert.Throws<InvalidOperationException>(() =>
            CorsOptionsValidator.Validate(options, new TestHostEnvironment("Development")));
    }

    [Fact]
    public void Production_rejects_http_origin()
    {
        var options = new CorsOptions
        {
            AllowedOrigins = ["http://frontend.example.com"]
        };

        Assert.Throws<InvalidOperationException>(() =>
            CorsOptionsValidator.Validate(options, new TestHostEnvironment("Production")));
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
