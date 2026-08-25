using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using QueryPilot.Api.Common.ErrorHandling;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Common.Responses;
using QueryPilot.Api.Configuration;
using QueryPilot.Api.Data;
using QueryPilot.Api.Data.Seed;
using QueryPilot.Api.Features.Analytics;
using QueryPilot.Api.Features.Ai;
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
var corsOptions = builder.Configuration
    .GetSection(CorsOptions.SectionName)
    .Get<CorsOptions>()
    ?? new CorsOptions();
CorsOptionsValidator.Validate(corsOptions, builder.Environment);
builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsOptions.FrontendPolicyName, policy =>
    {
        if (corsOptions.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();

            if (corsOptions.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        }
    });
});
var demoSeedOptions = builder.Configuration
    .GetSection(DemoSeedOptions.SectionName)
    .Get<DemoSeedOptions>()
    ?? new DemoSeedOptions();
DemoSeedOptionsValidator.Validate(demoSeedOptions, builder.Environment);
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
builder.Services.AddHttpClient<GeminiService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddScoped<IAiService>(serviceProvider =>
    serviceProvider.GetRequiredService<GeminiService>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAiIntentValidator, AiIntentValidator>();
builder.Services.AddScoped<IAiAnalyticsCoordinator, AiAnalyticsCoordinator>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<ProblemDetailsFactory, QueryPilotProblemDetailsFactory>();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy =
            JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    })
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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QueryPilot API",
        Version = "v1",
        Description = """
            QueryPilot is a PostgreSQL-backed business intelligence API for categories,
            products, customers, orders, returns, deterministic analytics, and grounded AI
            explanations. Swagger examples describe the API contract only; they are never
            treated as production data, analytics results, or database seed instructions.
            """
    });
    options.TagActionsBy(apiDescription =>
    {
        var controller = apiDescription.ActionDescriptor.RouteValues["controller"];
        return
        [
            controller switch
            {
                "Ai" => "AI",
                "Analytics" => "Analytics",
                "Categories" => "Categories",
                "Customers" => "Customers",
                "Orders" => "Orders",
                "Products" => "Products",
                "Returns" => "Returns",
                _ => controller ?? "Other"
            }
        ];
    });
    options.OperationFilter<ProblemDetailsOperationFilter>();
    var xmlDocumentationPath = Path.Combine(
        AppContext.BaseDirectory,
        $"{typeof(Program).Assembly.GetName().Name}.xml");
    options.IncludeXmlComments(xmlDocumentationPath);
});

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
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var statusCode = httpContext.Response.StatusCode;
    var problemDetails = new ProblemDetails
    {
        Status = statusCode,
        Title = ProblemDetailsTypes.GetTitle(statusCode),
        Type = ProblemDetailsTypes.ForStatusCode(statusCode),
        Instance = httpContext.Request.Path
    };
    problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

    httpContext.Response.ContentType = "application/problem+json";
    await httpContext.Response.WriteAsJsonAsync(
        problemDetails,
        problemDetails.GetType(),
        options: null,
        contentType: "application/problem+json",
        cancellationToken: httpContext.RequestAborted);
});

var swaggerEnabled = builder.Configuration.GetValue<bool>(
    $"{ApiDocumentationOptions.SectionName}:Enabled");
if (swaggerEnabled && !app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(CorsOptions.FrontendPolicyName);

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

public partial class Program;
