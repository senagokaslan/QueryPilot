using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QueryPilot.Api.Data;

namespace QueryPilot.Api.Tests.TestSupport;

internal sealed class ReturnTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;

    private ReturnTestDatabase(
        SqliteConnection connection,
        AppDbContext context,
        CountingSaveChangesInterceptor saveChanges)
    {
        this.connection = connection;
        Context = context;
        SaveChanges = saveChanges;
    }

    public AppDbContext Context { get; }

    public CountingSaveChangesInterceptor SaveChanges { get; }

    public static async Task<ReturnTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var saveChanges = new CountingSaveChangesInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(saveChanges, new RemoveForUpdateInterceptor())
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        saveChanges.Reset();
        return new ReturnTestDatabase(connection, context, saveChanges);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }

    private sealed class RemoveForUpdateInterceptor : DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            RemoveForUpdate(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            RemoveForUpdate(command);
            return ValueTask.FromResult(result);
        }

        private static void RemoveForUpdate(DbCommand command) =>
            command.CommandText = command.CommandText.Replace(
                " FOR UPDATE",
                string.Empty,
                StringComparison.OrdinalIgnoreCase);
    }
}
