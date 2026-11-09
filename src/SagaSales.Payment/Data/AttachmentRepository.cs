namespace SagaSales.Payment.Data;

using SagaSales.BuildingBlocks.Data;

/// <summary>Comprobantes de pago (spec 007), vía <c>db/payment/V007</c>.</summary>
public interface IAttachmentRepository
{
    Task<PaymentAttachmentDto> AddAsync(
        Guid id,
        Guid paymentId,
        string fileName,
        string contentType,
        int sizeBytes,
        string storageKey,
        Guid uploadedBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentAttachmentDto>> ListAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<(PaymentAttachmentDto Attachment, string StorageKey)?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<OrderProofStatusDto> GetProofStatusAsync(Guid orderId, CancellationToken cancellationToken);
}

public sealed class AttachmentRepository(IStoredProcedureExecutor executor) : IAttachmentRepository
{
    public async Task<PaymentAttachmentDto> AddAsync(
        Guid id,
        Guid paymentId,
        string fileName,
        string contentType,
        int sizeBytes,
        string storageKey,
        Guid uploadedBy,
        CancellationToken cancellationToken)
    {
        var arguments = SqlArgs.New()
            .With("id", id)
            .With("payment_id", paymentId)
            .With("file_name", fileName)
            .With("content_type", contentType)
            .With("size_bytes", sizeBytes)
            .With("storage_key", storageKey)
            .With("uploaded_by", uploadedBy);

        var row = await executor.QueryProcSingleAsync<AttachmentRow>(
            "dbo.sp_payment_attachment_add", arguments, cancellationToken: cancellationToken);

        return row!.ToDto();
    }

    public async Task<IReadOnlyList<PaymentAttachmentDto>> ListAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var rows = await executor.QueryProcAsync<AttachmentRow>(
            "dbo.sp_payment_attachment_list",
            SqlArgs.New().With("payment_id", paymentId),
            cancellationToken: cancellationToken);

        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<(PaymentAttachmentDto Attachment, string StorageKey)?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await executor.QueryProcSingleAsync<AttachmentRow>(
            "dbo.sp_payment_attachment_get",
            SqlArgs.New().With("id", id),
            cancellationToken: cancellationToken);

        return row is null ? null : (row.ToDto(), row.StorageKey);
    }

    public async Task<OrderProofStatusDto> GetProofStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var row = await executor.QueryProcSingleAsync<ProofRow>(
            "dbo.sp_order_payment_proof_status",
            SqlArgs.New().With("order_id", orderId),
            cancellationToken: cancellationToken);

        return new OrderProofStatusDto { Payments = row?.Payments ?? 0, WithoutProof = row?.WithoutProof ?? 0 };
    }

    private sealed class AttachmentRow
    {
        public Guid Id { get; set; }

        public Guid PaymentId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public int SizeBytes { get; set; }

        public DateTime UploadedAt { get; set; }

        public string StorageKey { get; set; } = string.Empty;

        public PaymentAttachmentDto ToDto() => new()
        {
            Id = Id,
            PaymentId = PaymentId,
            FileName = FileName,
            ContentType = ContentType,
            SizeBytes = SizeBytes,
            UploadedAt = DateTime.SpecifyKind(UploadedAt, DateTimeKind.Utc),
        };
    }

    private sealed class ProofRow
    {
        public int Payments { get; set; }

        public int? WithoutProof { get; set; }
    }
}
