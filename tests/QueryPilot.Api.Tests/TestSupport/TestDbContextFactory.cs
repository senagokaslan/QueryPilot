using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QueryPilot.Api.Data;

namespace QueryPilot.Api.Tests.TestSupport;

internal static class TestDbContextFactory
{
    public static AppDbContext Create(CountingSaveChangesInterceptor saveChanges)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"querypilot-test-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(saveChanges)
            .Options;

        return new AppDbContext(options);
    }
}

internal sealed class CountingSaveChangesInterceptor : SaveChangesInterceptor
{
    public int CallCount { get; private set; }

    public void Reset() => CallCount = 0;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CallCount++;
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return ValueTask.FromResult(result);
    }
}
