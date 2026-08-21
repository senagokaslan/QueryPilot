using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Customers.Dtos;

namespace QueryPilot.Api.Features.Customers;

public sealed class CustomerService(
    AppDbContext dbContext,
    IOptions<PaginationOptions> paginationOptions) : ICustomerService
{
    private readonly PaginationOptions _paginationOptions = paginationOptions.Value;

    public async Task<PagedResponse<CustomerResponse>> GetAllAsync(
        CustomerListRequest request,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationRequest(request.Page, request.PageSize)
            .Normalize(_paginationOptions);
        var query = dbContext.Customers.AsNoTracking();

        if (request.IsActive.HasValue)
        {
            query = query.Where(customer => customer.IsActive == request.IsActive.Value);
        }

        var city = request.City?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(city))
        {
            query = query.Where(customer => customer.City.ToLower() == city);
        }

        var search = request.Search?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(customer =>
                customer.Name.ToLower().Contains(search)
                || customer.NormalizedEmail.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var customers = await Project(query
                .OrderBy(customer => customer.Id)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CustomerResponse>(
            customers,
            pagination.Page,
            pagination.PageSize,
            totalCount);
    }

    public async Task<CustomerResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var customer = await Project(
                dbContext.Customers.AsNoTracking()
                    .Where(customer => customer.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return customer
            ?? throw new NotFoundException($"Customer with id {id} was not found.");
    }

    public async Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = NormalizeRequiredValue(request.Name, nameof(request.Name));
        var email = request.Email.Trim().ToLowerInvariant();
        var city = NormalizeRequiredValue(request.City, nameof(request.City));

        await EnsureEmailIsUniqueAsync(email, null, cancellationToken);

        var customer = new Customer
        {
            Name = name,
            Email = email,
            NormalizedEmail = email,
            City = city,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Customers.Add(customer);
        await SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Customer with id {id} was not found.");
        var name = NormalizeRequiredValue(request.Name, nameof(request.Name));
        var email = request.Email.Trim().ToLowerInvariant();
        var city = NormalizeRequiredValue(request.City, nameof(request.City));

        await EnsureEmailIsUniqueAsync(email, id, cancellationToken);

        customer.Name = name;
        customer.Email = email;
        customer.NormalizedEmail = email;
        customer.City = city;
        customer.IsActive = request.IsActive!.Value;

        await SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    public async Task DeactivateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Customer with id {id} was not found.");

        if (!customer.IsActive)
        {
            return;
        }

        customer.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureEmailIsUniqueAsync(
        string normalizedEmail,
        long? excludedId,
        CancellationToken cancellationToken)
    {
        var emailExists = await dbContext.Customers.AnyAsync(
            customer => customer.Id != excludedId
                && customer.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (emailExists)
        {
            throw new ConflictException("A customer with this email already exists.");
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ConflictException(
                "A customer with this email already exists.",
                exception);
        }
    }

    private static string NormalizeRequiredValue(string value, string fieldName)
    {
        var normalizedValue = value.Trim();

        if (normalizedValue.Length == 0)
        {
            throw new RequestValidationException(
                new Dictionary<string, string[]>
                {
                    [fieldName] = ["The value cannot be empty or whitespace."]
                });
        }

        return normalizedValue;
    }

    private static IQueryable<CustomerResponse> Project(IQueryable<Customer> query) =>
        query.Select(customer => new CustomerResponse(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.City,
            customer.IsActive,
            customer.CreatedAt));

    private static CustomerResponse Map(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Email,
        customer.City,
        customer.IsActive,
        customer.CreatedAt);
}
