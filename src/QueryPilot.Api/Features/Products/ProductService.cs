using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Products.Dtos;

namespace QueryPilot.Api.Features.Products;

public sealed class ProductService(
    AppDbContext dbContext,
    IOptions<PaginationOptions> paginationOptions) : IProductService
{
    private readonly PaginationOptions _paginationOptions = paginationOptions.Value;

    public async Task<PagedResponse<ProductResponse>> GetAllAsync(
        ProductListRequest request,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationRequest(request.Page, request.PageSize)
            .Normalize(_paginationOptions);
        var query = dbContext.Products.AsNoTracking();

        if (request.IsActive.HasValue)
        {
            query = query.Where(product => product.IsActive == request.IsActive.Value);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(product => product.CategoryId == request.CategoryId.Value);
        }

        var search = request.Search?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(product =>
                product.Name.ToLower().Contains(search)
                || product.SKU.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var products = await Project(query
                .OrderBy(product => product.Id)
                .Skip(pagination.Skip)
                .Take(pagination.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ProductResponse>(
            products,
            pagination.Page,
            pagination.PageSize,
            totalCount);
    }

    public async Task<ProductResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var product = await Project(
                dbContext.Products.AsNoTracking().Where(product => product.Id == id))
            .SingleOrDefaultAsync(cancellationToken);

        return product
            ?? throw new NotFoundException($"Product with id {id} was not found.");
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = NormalizeRequiredValue(request.Name, nameof(request.Name));
        var sku = NormalizeSku(request.SKU);

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureSkuIsUniqueAsync(sku, null, cancellationToken);

        var product = new Product
        {
            Name = name,
            SKU = sku,
            CategoryId = request.CategoryId,
            UnitPrice = request.UnitPrice,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Products.Add(product);
        await SaveChangesAsync(sku, cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<ProductResponse> UpdateAsync(
        long id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Product with id {id} was not found.");
        var name = NormalizeRequiredValue(request.Name, nameof(request.Name));
        var sku = NormalizeSku(request.SKU);

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureSkuIsUniqueAsync(sku, id, cancellationToken);

        product.Name = name;
        product.SKU = sku;
        product.CategoryId = request.CategoryId;
        product.UnitPrice = request.UnitPrice;
        product.IsActive = request.IsActive!.Value;

        await SaveChangesAsync(sku, cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task DeactivateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Product with id {id} was not found.");

        if (!product.IsActive)
        {
            return;
        }

        product.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCategoryExistsAsync(
        long categoryId,
        CancellationToken cancellationToken)
    {
        var categoryExists = await dbContext.Categories
            .AnyAsync(category => category.Id == categoryId, cancellationToken);

        if (!categoryExists)
        {
            throw new NotFoundException(
                $"Category with id {categoryId} was not found.");
        }
    }

    private async Task EnsureSkuIsUniqueAsync(
        string sku,
        long? excludedId,
        CancellationToken cancellationToken)
    {
        var skuExists = await dbContext.Products.AnyAsync(
            product => product.Id != excludedId && product.SKU == sku,
            cancellationToken);

        if (skuExists)
        {
            throw new ConflictException($"A product with SKU '{sku}' already exists.");
        }
    }

    private async Task SaveChangesAsync(
        string sku,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ConflictException(
                $"A product with SKU '{sku}' already exists.",
                exception);
        }
    }

    private static string NormalizeSku(string sku) =>
        NormalizeRequiredValue(sku, nameof(CreateProductRequest.SKU)).ToUpperInvariant();

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

    private static IQueryable<ProductResponse> Project(IQueryable<Product> query) =>
        query.Select(product => new ProductResponse(
            product.Id,
            product.Name,
            product.SKU,
            new ProductCategorySummaryResponse(
                product.Category.Id,
                product.Category.Name,
                product.Category.IsActive),
            product.UnitPrice,
            product.IsActive,
            product.CreatedAt));
}
