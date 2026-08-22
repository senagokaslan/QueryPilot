using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QueryPilot.Api.Features.Returns.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateReturnRequest
{
    [Range(1, long.MaxValue)]
    public long OrderItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(Return.ReasonMaxLength, MinimumLength = 1)]
    public string Reason { get; init; } = string.Empty;
}
