namespace QueryPilot.Api.Common.Pagination;

public sealed record PaginationRequest(int Page = 1, int? PageSize = null)
{
    public PaginationParameters Normalize(PaginationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalizedPage = Math.Max(Page, 1);
        var requestedPageSize = PageSize ?? options.DefaultPageSize;
        var normalizedPageSize = Math.Clamp(
            requestedPageSize,
            1,
            options.MaxPageSize);

        return new PaginationParameters(normalizedPage, normalizedPageSize);
    }
}
