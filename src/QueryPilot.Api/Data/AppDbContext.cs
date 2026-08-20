using Microsoft.EntityFrameworkCore;

namespace QueryPilot.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options);
