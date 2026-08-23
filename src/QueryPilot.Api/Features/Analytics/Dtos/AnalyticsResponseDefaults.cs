namespace QueryPilot.Api.Features.Analytics.Dtos;

/// <summary>
/// Defines the shared no-data policy for analytics responses. Aggregate money,
/// quantity and percentage values are numeric zero; collections are empty.
/// </summary>
public static class AnalyticsResponseDefaults
{
    public const decimal Money = 0m;

    public const int Quantity = 0;

    public const decimal Percentage = 0m;

    public static IReadOnlyList<T> EmptyItems<T>() => [];
}
