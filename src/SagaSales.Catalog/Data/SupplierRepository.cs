namespace SagaSales.Catalog.Data;

using SagaSales.BuildingBlocks.Data;
using SagaSales.Shared.DTOs;

/// <summary>Suppliers, through <c>db/catalog/V006__functions_purchasing.sql</c>.</summary>
public interface ISupplierRepository
{
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken);

    Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierDto>> ListAsync(bool includeInactive, string? search, CancellationToken cancellationToken);
}

public sealed class SupplierRepository(IStoredProcedureExecutor executor) : ISupplierRepository
{
    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("name", request.Name.Trim())
            .With("tax_id", Nullify(request.TaxId))
            .With("contact_name", Nullify(request.ContactName))
            .With("email", Nullify(request.Email))
            .With("phone", Nullify(request.Phone))
            .With("notes", Nullify(request.Notes));

        var row = await executor.QueryProcSingleAsync<SupplierRow>(
            "dbo.sp_supplier_create", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<SupplierDto> UpdateAsync(
        Guid id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("supplier_id", id)
            .With("name", request.Name.Trim())
            .With("tax_id", Nullify(request.TaxId))
            .With("contact_name", Nullify(request.ContactName))
            .With("email", Nullify(request.Email))
            .With("phone", Nullify(request.Phone))
            .With("notes", Nullify(request.Notes));

        var row = await executor.QueryProcSingleAsync<SupplierRow>(
            "dbo.sp_supplier_update", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New().With("supplier_id", id);
        return executor.ExecuteProcAsync("dbo.sp_supplier_deactivate", arguments, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierDto>> ListAsync(
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("include_inactive", includeInactive)
            .With("search", Nullify(search));

        var rows = await executor.QueryProcAsync<SupplierRow>(
            "dbo.sp_supplier_list", arguments, cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    private static string? Nullify(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class SupplierRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? TaxId { get; set; }

        public string? ContactName { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public string? Notes { get; set; }

        public short Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public SupplierDto ToDto() => new()
        {
            Id = Id,
            Name = Name,
            TaxId = TaxId,
            ContactName = ContactName,
            Email = Email,
            Phone = Phone,
            Notes = Notes,
            Status = Status,
            CreatedAt = CreatedAt,
        };
    }
}
