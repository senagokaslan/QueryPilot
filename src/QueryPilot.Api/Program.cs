using QueryPilot.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<AiOptions>(
    builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
