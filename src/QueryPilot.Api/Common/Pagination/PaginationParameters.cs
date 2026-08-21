namespace QueryPilot.Api.Common.Pagination;

public readonly record struct PaginationParameters(int Page, int PageSize)
{
    public int Skip => checked((Page - 1) * PageSize);
}
