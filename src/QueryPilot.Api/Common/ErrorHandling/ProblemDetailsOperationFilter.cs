using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace QueryPilot.Api.Common.ErrorHandling;

public sealed class ProblemDetailsOperationFilter : IOperationFilter
{
    private const string ProblemMediaType = "application/problem+json";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!operation.Responses.ContainsKey("500"))
        {
            operation.Responses["500"] = new OpenApiResponse
            {
                Description = "Unexpected server error.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [ProblemMediaType] = new()
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(
                            typeof(ProblemDetails),
                            context.SchemaRepository)
                    }
                }
            };
        }

        foreach (var (statusCode, response) in operation.Responses)
        {
            if (!int.TryParse(statusCode, out var numericStatus)
                || numericStatus < StatusCodes.Status400BadRequest)
            {
                continue;
            }

            var schema = response.Content.Values
                .Select(content => content.Schema)
                .FirstOrDefault(candidate => candidate is not null)
                ?? context.SchemaGenerator.GenerateSchema(
                    typeof(ProblemDetails),
                    context.SchemaRepository);
            response.Content = new Dictionary<string, OpenApiMediaType>
            {
                [ProblemMediaType] = new() { Schema = schema }
            };
        }
    }
}
