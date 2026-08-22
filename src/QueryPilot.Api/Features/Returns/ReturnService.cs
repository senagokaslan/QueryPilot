using Microsoft.EntityFrameworkCore;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Orders;
using QueryPilot.Api.Features.Returns.Dtos;

namespace QueryPilot.Api.Features.Returns;

public sealed class ReturnService(AppDbContext dbContext) : IReturnService
{
    public async Task<ReturnResponse> CreateAsync(
        CreateReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var orderItem = await dbContext.OrderItems
                .FromSqlInterpolated(
                    $"""SELECT * FROM "OrderItems" WHERE "Id" = {request.OrderItemId} FOR UPDATE""")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(
                    $"Order item with id {request.OrderItemId} was not found.");

            var orderStatus = await dbContext.Orders
                .Where(order => order.Id == orderItem.OrderId)
                .Select(order => order.Status)
                .SingleAsync(cancellationToken);

            if (orderStatus != OrderStatus.Completed)
            {
                throw new ConflictException(
                    $"Returns are allowed only for completed orders. Current status: {orderStatus}.");
            }

            var previouslyReturnedQuantity = await dbContext.Returns
                .Where(returnRecord => returnRecord.OrderItemId == orderItem.Id)
                .SumAsync(
                    returnRecord => (int?)returnRecord.Quantity,
                    cancellationToken)
                ?? 0;
            var remainingReturnableQuantity =
                orderItem.Quantity - previouslyReturnedQuantity;

            if (request.Quantity > remainingReturnableQuantity)
            {
                throw new RequestValidationException(
                    new Dictionary<string, string[]>
                    {
                        [nameof(request.Quantity)] =
                        [
                            $"The return quantity cannot exceed the remaining returnable quantity of {remainingReturnableQuantity}."
                        ]
                    });
            }

            var returnDate = DateTime.UtcNow;
            var returnRecord = new Return
            {
                OrderItemId = orderItem.Id,
                Quantity = request.Quantity,
                Reason = request.Reason.Trim(),
                ReturnDate = returnDate,
                Amount = orderItem.UnitPrice * request.Quantity
            };

            dbContext.Returns.Add(returnRecord);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ReturnResponse(
                returnRecord.Id,
                returnRecord.OrderItemId,
                returnRecord.Quantity,
                returnRecord.Reason,
                returnRecord.ReturnDate,
                returnRecord.Amount,
                remainingReturnableQuantity - returnRecord.Quantity);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void ValidateRequest(CreateReturnRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.OrderItemId <= 0)
        {
            errors[nameof(request.OrderItemId)] =
                ["The value must be greater than zero."];
        }

        if (request.Quantity <= 0)
        {
            errors[nameof(request.Quantity)] =
                ["The value must be greater than zero."];
        }

        var reason = request.Reason?.Trim();

        if (string.IsNullOrEmpty(reason))
        {
            errors[nameof(request.Reason)] =
                ["The value cannot be empty or whitespace."];
        }
        else if (reason.Length > Return.ReasonMaxLength)
        {
            errors[nameof(request.Reason)] =
                [$"The value cannot exceed {Return.ReasonMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }
}
