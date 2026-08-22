using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Orders.Dtos;

namespace QueryPilot.Api.Features.Orders;

public sealed class OrderService(
    AppDbContext dbContext,
    ILogger<OrderService> logger,
    IOptions<PaginationOptions> paginationOptions) : IOrderService
{
    private readonly PaginationOptions _paginationOptions = paginationOptions.Value;

    public async Task<PagedResponse<OrderListResponse>> GetAllAsync(
        OrderListRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateListRequest(request);

        var pagination = new PaginationRequest(request.Page, request.PageSize)
            .Normalize(_paginationOptions);
        var query = dbContext.Orders.AsNoTracking();

        if (request.From.HasValue)
        {
            var fromUtc = request.From.Value.UtcDateTime;
            query = query.Where(order => order.OrderDate >= fromUtc);
        }

        if (request.To.HasValue)
        {
            var toUtc = request.To.Value.UtcDateTime;
            query = query.Where(order => order.OrderDate <= toUtc);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(order => order.Status == request.Status.Value);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(order => order.CustomerId == request.CustomerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.Id)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(order => new OrderListResponse(
                order.Id,
                order.CustomerId,
                order.OrderDate,
                order.Status,
                order.TotalAmount,
                order.Items.Count))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OrderListResponse>(
            orders,
            pagination.Page,
            pagination.PageSize,
            totalCount);
    }

    public async Task<OrderDetailResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Id == id)
            .Select(order => new OrderDetailResponse(
                order.Id,
                new OrderCustomerSummaryResponse(
                    order.Customer.Id,
                    order.Customer.Name,
                    order.Customer.Email,
                    order.Customer.City),
                order.OrderDate,
                order.Status,
                order.TotalAmount,
                order.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new OrderItemResponse(
                        item.Id,
                        new OrderProductSummaryResponse(
                            item.Product.Id,
                            item.Product.Name,
                            item.Product.SKU),
                        item.Quantity,
                        item.UnitPrice,
                        item.LineTotal,
                        item.Returns.Any()
                            ? new OrderItemReturnSummaryResponse(
                                item.Returns.Count,
                                item.Returns.Sum(returnRecord => returnRecord.Quantity),
                                item.Returns.Sum(returnRecord => returnRecord.Amount))
                            : null))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        return order
            ?? throw new NotFoundException($"Order with id {id} was not found.");
    }

    public async Task<OrderDetailResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var order = await CreateOrderAsync(request, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Order {OrderId} created with {ItemCount} items.",
                order.Id,
                order.Items.Count);

            return order;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<OrderDetailResponse> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {

        var customer = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == request.CustomerId)
            .Select(customer => new
            {
                customer.Id,
                customer.Name,
                customer.Email,
                customer.City,
                customer.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(
                $"Customer with id {request.CustomerId} was not found.");

        if (!customer.IsActive)
        {
            throw new ConflictException(
                $"Customer with id {request.CustomerId} is inactive and cannot place new orders.");
        }

        var productIds = request.Items
            .Select(item => item.ProductId)
            .Distinct()
            .ToArray();
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.SKU,
                product.UnitPrice,
                product.IsActive
            })
            .ToListAsync(cancellationToken);
        var productsById = products.ToDictionary(product => product.Id);

        var missingProductIds = productIds
            .Where(productId => !productsById.ContainsKey(productId))
            .Order()
            .ToArray();

        if (missingProductIds.Length > 0)
        {
            throw new NotFoundException(
                $"Products with ids [{string.Join(", ", missingProductIds)}] were not found.");
        }

        var inactiveProductIds = products
            .Where(product => !product.IsActive)
            .Select(product => product.Id)
            .Order()
            .ToArray();

        if (inactiveProductIds.Length > 0)
        {
            throw new ConflictException(
                $"Products with ids [{string.Join(", ", inactiveProductIds)}] are inactive and cannot be ordered.");
        }

        var nowUtc = DateTime.UtcNow;
        var order = new Order
        {
            CustomerId = customer.Id,
            OrderDate = nowUtc,
            Status = OrderStatus.Pending,
            CreatedAt = nowUtc
        };

        foreach (var requestedItem in request.Items)
        {
            var product = productsById[requestedItem.ProductId];
            var lineTotal = product.UnitPrice * requestedItem.Quantity;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = requestedItem.Quantity,
                UnitPrice = product.UnitPrice,
                LineTotal = lineTotal
            });
        }

        order.TotalAmount = order.Items.Sum(item => item.LineTotal);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        var persistedTotals = await dbContext.Orders
            .AsNoTracking()
            .Where(savedOrder => savedOrder.Id == order.Id)
            .Select(savedOrder => new
            {
                savedOrder.TotalAmount,
                ItemsTotal = savedOrder.Items.Sum(item => item.LineTotal)
            })
            .SingleAsync(cancellationToken);

        if (persistedTotals.TotalAmount != persistedTotals.ItemsTotal)
        {
            throw new InvalidOperationException(
                "The persisted order total does not match its item totals.");
        }

        return new OrderDetailResponse(
            order.Id,
            new OrderCustomerSummaryResponse(
                customer.Id,
                customer.Name,
                customer.Email,
                customer.City),
            order.OrderDate,
            order.Status,
            order.TotalAmount,
            order.Items.Select(item =>
            {
                var product = productsById[item.ProductId];

                return new OrderItemResponse(
                    item.Id,
                    new OrderProductSummaryResponse(
                        product.Id,
                        product.Name,
                        product.SKU),
                    item.Quantity,
                    item.UnitPrice,
                    item.LineTotal,
                    null);
            }).ToArray());
    }

    private static void ValidateListRequest(OrderListRequest request)
    {
        if (request.From.HasValue
            && request.To.HasValue
            && request.From.Value > request.To.Value)
        {
            throw new RequestValidationException(
                new Dictionary<string, string[]>
                {
                    [nameof(request.From)] =
                    ["From must be earlier than or equal to To."],
                    [nameof(request.To)] =
                    ["To must be later than or equal to From."]
                });
        }
    }

    private static void ValidateRequest(CreateOrderRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.CustomerId <= 0)
        {
            errors[nameof(request.CustomerId)] = ["The value must be greater than zero."];
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            errors[nameof(request.Items)] = ["At least one order item is required."];
        }
        else
        {
            for (var index = 0; index < request.Items.Count; index++)
            {
                var item = request.Items[index];

                if (item is null)
                {
                    errors[$"{nameof(request.Items)}[{index}]"] = ["The item is required."];
                    continue;
                }

                if (item.ProductId <= 0)
                {
                    errors[$"{nameof(request.Items)}[{index}].{nameof(item.ProductId)}"] =
                        ["The value must be greater than zero."];
                }

                if (item.Quantity <= 0)
                {
                    errors[$"{nameof(request.Items)}[{index}].{nameof(item.Quantity)}"] =
                        ["The value must be greater than zero."];
                }
            }

            var duplicateProductIds = request.Items
                .Where(item => item is not null && item.ProductId > 0)
                .GroupBy(item => item.ProductId)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .Order()
                .ToArray();

            if (duplicateProductIds.Length > 0)
            {
                errors[nameof(request.Items)] =
                [
                    $"Each product can appear only once. Duplicate product IDs: {string.Join(", ", duplicateProductIds)}."
                ];
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }
}
