using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QueryPilot.Api.Common.Exceptions;
using QueryPilot.Api.Common.Pagination;
using QueryPilot.Api.Data;
using QueryPilot.Api.Features.Categories.Dtos;

namespace QueryPilot.Api.Features.Categories;

public sealed class CategoryService(
    AppDbContext dbContext,
    IOptions<PaginationOptions> paginationOptions) : ICategoryService
{
    private readonly PaginationOptions _paginationOptions = paginationOptions.Value;

    public async Task<PagedResponse<CategoryResponse>> GetAllAsync(
        CategoryListRequest request,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationRequest(request.Page, request.PageSize)
            .Normalize(_paginationOptions);
        var query = dbContext.Categories.AsNoTracking();

        if (request.IsActive.HasValue)
        {
            query = query.Where(category => category.IsActive == request.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var categories = await query
            .OrderBy(category => category.Id)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .Select(category => new CategoryResponse(
                category.Id,
                category.Name,
                category.IsActive,
                category.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CategoryResponse>(
            categories,
            pagination.Page,
            pagination.PageSize,
            totalCount);
    }

    public async Task<CategoryResponse> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.Id == id)
            .Select(category => new CategoryResponse(
                category.Id,
                category.Name,
                category.IsActive,
                category.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return category
            ?? throw new NotFoundException($"Category with id {id} was not found.");
    }

    public async Task<CategoryResponse> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(request.Name);
        await EnsureNameIsUniqueAsync(name, null, cancellationToken);

        var category = new Category
        {
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Categories.Add(category);
        await SaveChangesAsync(name, cancellationToken);

        return Map(category);
    }

    public async Task<CategoryResponse> UpdateAsync(
        long id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Category with id {id} was not found.");
        var name = NormalizeName(request.Name);

        await EnsureNameIsUniqueAsync(name, id, cancellationToken);

        category.Name = name;
        category.IsActive = request.IsActive;
        await SaveChangesAsync(name, cancellationToken);

        return Map(category);
    }

    public async Task DeactivateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Category with id {id} was not found.");

        if (!category.IsActive)
        {
            return;
        }

        category.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameIsUniqueAsync(
        string name,
        long? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.ToLowerInvariant();
        var nameExists = await dbContext.Categories.AnyAsync(
            category => category.Id != excludedId
                && category.Name.ToLower() == normalizedName,
            cancellationToken);

        if (nameExists)
        {
            throw new ConflictException($"A category named '{name}' already exists.");
        }
    }

    private async Task SaveChangesAsync(
        string categoryName,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ConflictException(
                $"A category named '{categoryName}' already exists.",
                exception);
        }
    }

    private static string NormalizeName(string name)
    {
        var normalizedName = name.Trim();

        if (normalizedName.Length == 0)
        {
            throw new RequestValidationException(
                new Dictionary<string, string[]>
                {
                    [nameof(CreateCategoryRequest.Name)] =
                    ["Category name cannot be empty or whitespace."]
                });
        }

        return normalizedName;
    }

    private static CategoryResponse Map(Category category) => new(
        category.Id,
        category.Name,
        category.IsActive,
        category.CreatedAt);
}
