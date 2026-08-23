namespace QueryPilot.Api.Configuration;

public static class CorsOptionsValidator
{
    public static void Validate(CorsOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction() && options.AllowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Production requires at least one exact HTTPS Cors:AllowedOrigins entry.");
        }

        foreach (var origin in options.AllowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(origin)
                || origin.Contains('*')
                || !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || string.IsNullOrEmpty(uri.Host)
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new InvalidOperationException(
                    $"Cors origin '{origin}' must be an exact scheme/host/port origin without wildcard, path, query, fragment, or credentials.");
            }

            if (environment.IsProduction()
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Production Cors origin '{origin}' must use HTTPS.");
            }
        }

        if (options.AllowedOrigins.Distinct(StringComparer.Ordinal).Count()
            != options.AllowedOrigins.Length)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins cannot contain duplicate entries.");
        }
    }
}
