namespace SagaSales.Catalog.Data;

using Dapper;
using SagaSales.BuildingBlocks.Data;
using SagaSales.Catalog.Domain;

/// <summary>
/// Products and their stock, through the procedures in <c>db/catalog/V002</c> and <c>V004</c>.
/// No ORM: this repository is the only way in.
/// </summary>
public interface IProductRepository
{
    Task<Product> CreateAsync(
        string name,
        string? description,
        decimal price,
        string currency,
        int stockQuantity,
        Guid? categoryId,
        Guid createdBy,
        CancellationToken cancellationToken);

    Task<Product> UpdateAsync(
        Guid id,
        string name,
        string? description,
        decimal price,
        string currency,
        int stockQuantity,
        Guid? categoryId,
        ProductStatus status,
        CancellationToken cancellationToken);

    Task RetireAsync(Guid id, CancellationToken cancellationToken);

    Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductListResult> ListAsync(
        int pageNumber,
        int pageSize,
        Guid? categoryId,
        string? search,
        string? sortBy,
        bool descending,
        CancellationToken cancellationToken);

    /// <summary>Reserves stock atomically; throws <c>INSUFFICIENT_STOCK</c> when short.</summary>
    Task<StockReservation> ReserveStockAsync(Guid id, int quantity, CancellationToken cancellationToken);

    Task ReleaseStockAsync(Guid id, int quantity, CancellationToken cancellationToken);
}

public sealed record ProductListResult(IReadOnlyList<Product> Items, int TotalCount);

public sealed record StockReservation(Guid Id, string Name, decimal Price, string Currency, int StockQuantity);

public sealed class ProductRepository(
    IStoredProcedureExecutor executor,
    ILogger<ProductRepository> logger) : IProductRepository
{
    public async Task<Product> CreateAsync(
        string name,
        string? description,
        decimal price,
        string currency,
        int stockQuantity,
        Guid? categoryId,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("name", name)
            .With("description", description)
            .With("base_price", price)
            .With("currency", currency)
            .With("stock_quantity", stockQuantity)
            .With("category_id", categoryId)
            .With("created_by", createdBy);

        var row = await executor.QueryProcSingleAsync<ProductRow>(
            "dbo.sp_product_create", arguments, cancellationToken: cancellationToken);

        logger.LogInformation("Product {Name} created", name);
        return row!.ToProduct();
    }

    public async Task<Product> UpdateAsync(
        Guid id,
        string name,
        string? description,
        decimal price,
        string currency,
        int stockQuantity,
        Guid? categoryId,
        ProductStatus status,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("product_id", id)
            .With("name", name)
            .With("description", description)
            .With("base_price", price)
            .With("currency", currency)
            .With("stock_quantity", stockQuantity)
            .With("category_id", categoryId)
            .With("status", (short)status);

        var row = await executor.QueryProcSingleAsync<ProductRow>(
            "dbo.sp_product_update", arguments, cancellationToken: cancellationToken);

        return row!.ToProduct();
    }

    public Task RetireAsync(Guid id, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("product_id", id);
        return executor.ExecuteProcAsync("dbo.sp_product_retire", arguments, cancellationToken: cancellationToken);
    }

    public async Task<Product?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("product_id", id);

        var row = await executor.QueryProcSingleAsync<ProductRow>(
            "dbo.sp_product_get", arguments, cancellationToken: cancellationToken);

        return row?.ToProduct();
    }

    public async Task<ProductListResult> ListAsync(
        int pageNumber,
        int pageSize,
        Guid? categoryId,
        string? search,
        string? sortBy,
        bool descending,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("page_number", pageNumber)
            .With("page_size", pageSize)
            .With("category_id", categoryId)
            .With("search", search)
            .With("sort_by", sortBy ?? "name")
            .With("descending", descending);

        var rows = await executor.QueryProcAsync<ProductRow>(
            "dbo.sp_product_list", arguments, cancellationToken: cancellationToken);

        var items = rows.Select(r => r.ToProduct()).ToList();
        var total = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new ProductListResult(items, total);
    }

    public async Task<StockReservation> ReserveStockAsync(Guid id, int quantity, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("product_id", id)
            .With("quantity", quantity);

        var reservation = await executor.QueryProcSingleAsync<StockReservation>(
            "dbo.sp_stock_reserve", arguments, cancellationToken: cancellationToken);

        if (reservation is null)
        {
            throw new Shared.Exceptions.NotFoundException("Product", id);
        }

        return reservation;
    }

    public Task ReleaseStockAsync(Guid id, int quantity, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("product_id", id)
            .With("quantity", quantity);

        return executor.ExecuteProcAsync("dbo.sp_stock_release", arguments, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Row shape returned by the procedures. Dapper maps by column name, so the aliases in
    /// the SQL match these properties.
    /// </summary>
    private sealed class ProductRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public string Currency { get; set; } = "USD";

        public int StockQuantity { get; set; }

        public Guid? CategoryId { get; set; }

        public short Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public int TotalCount { get; set; }

        public Product ToProduct() => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Price = Price,
            Currency = Currency,
            StockQuantity = StockQuantity,
            CategoryId = CategoryId,
            Status = (ProductStatus)Status,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
    }
}
