using QueryPilot.Api.Features.Returns.Dtos;

namespace QueryPilot.Api.Features.Returns;

public interface IReturnService
{
    Task<ReturnResponse> CreateAsync(
        CreateReturnRequest request,
        CancellationToken cancellationToken = default);
}
