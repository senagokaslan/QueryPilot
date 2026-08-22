using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QueryPilot.Api.Common.ErrorHandling;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Common.Responses;
using QueryPilot.Api.Configuration;
using QueryPilot.Api.Data;
using QueryPilot.Api.Data.Seed;
using QueryPilot.Api.Features.Categories;
using QueryPilot.Api.Features.Customers;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Products;
using QueryPilot.Api.Features.Returns;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Logging.AddFilter(
    "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware",
    LogLevel.None);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services
    .AddHealthChecks()
    .AddCheck(
        "api",
        () => HealthCheckResult.Healthy("API is running."))
    .AddCheck<PostgreSqlHealthCheck>(
        "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(5));
builder.Services.Configure<AiOptions>(
    builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<DemoSeedOptions>(
    builder.Configuration.GetSection(DemoSeedOptions.SectionName));
builder.Services
    .AddOptions<PaginationOptions>()
    .Bind(builder.Configuration.GetSection(PaginationOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.DefaultPageSize <= options.MaxPageSize,
        "Pagination:DefaultPageSize cannot be greater than Pagination:MaxPageSize.")
    .ValidateOnStart();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed.",
                Type = ProblemDetailsTypes.BadRequest,
                Instance = context.HttpContext.Request.Path
            };
            problemDetails.Extensions["traceId"] =
                context.HttpContext.TraceIdentifier;

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>($"{DemoSeedOptions.SectionName}:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapControllers();

app.Run();
