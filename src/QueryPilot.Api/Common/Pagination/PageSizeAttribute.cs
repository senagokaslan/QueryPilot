using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace QueryPilot.Api.Common.Pagination;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PageSizeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(
        object? value,
        ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var pageSize = (int)value;
        var options = validationContext
            .GetRequiredService<IOptions<PaginationOptions>>()
            .Value;

        return pageSize >= 1 && pageSize <= options.MaxPageSize
            ? ValidationResult.Success
            : new ValidationResult(
                $"PageSize must be between 1 and {options.MaxPageSize}.",
                [validationContext.MemberName!]);
    }
}
