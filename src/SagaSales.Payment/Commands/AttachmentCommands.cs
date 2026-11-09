namespace SagaSales.Payment.Commands;

using SagaSales.Payment.Services;

/// <summary>Sube un comprobante a un pago (spec 007). El tipo se decide por el contenido del archivo.</summary>
public record UploadPaymentAttachmentCommand(Guid PaymentId, string FileName, byte[] Content) : ICommand<PaymentAttachmentDto>;

public record GetPaymentAttachmentsQuery(Guid PaymentId) : IQuery<IReadOnlyList<PaymentAttachmentDto>>;

public record DownloadPaymentAttachmentQuery(Guid AttachmentId) : IQuery<PaymentAttachmentFile>;

public record GetOrderProofStatusQuery(Guid OrderId) : IQuery<OrderProofStatusDto>;

public sealed record PaymentAttachmentFile(string FileName, string ContentType, byte[] Content);

public sealed class AttachmentHandlers(
    IAttachmentRepository repository,
    IReceiptStorage storage,
    ICurrentUser currentUser) :
    ICommandHandler<UploadPaymentAttachmentCommand, PaymentAttachmentDto>,
    IQueryHandler<GetPaymentAttachmentsQuery, IReadOnlyList<PaymentAttachmentDto>>,
    IQueryHandler<DownloadPaymentAttachmentQuery, PaymentAttachmentFile>,
    IQueryHandler<GetOrderProofStatusQuery, OrderProofStatusDto>
{
    public async Task<Result<PaymentAttachmentDto>> Handle(
        UploadPaymentAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Content.Length == 0)
        {
            throw new SagaSales.Shared.Exceptions.ValidationException("Selecciona la foto o el PDF del comprobante.");
        }

        if (request.Content.Length > ReceiptFileType.MaxBytes)
        {
            throw new SagaSales.Shared.Exceptions.ValidationException("El comprobante no puede superar los 5 MB.");
        }

        var contentType = ReceiptFileType.Detect(request.Content)
            ?? throw new SagaSales.Shared.Exceptions.ValidationException("Formato no admitido. Sube una foto (JPG, PNG, WEBP) o un PDF.");

        var id = Guid.NewGuid();
        var key = ReceiptStorage.KeyFor(currentUser.RequireTenantId(), id, contentType);

        // Primero el archivo: si el registro falla queda un archivo huérfano, nunca un registro sin archivo.
        await storage.SaveAsync(key, request.Content, cancellationToken);

        var attachment = await repository.AddAsync(
            id,
            request.PaymentId,
            Path.GetFileName(request.FileName ?? string.Empty),
            contentType,
            request.Content.Length,
            key,
            currentUser.RequireUserId(),
            cancellationToken);

        return Result<PaymentAttachmentDto>.Success(attachment);
    }

    public async Task<Result<IReadOnlyList<PaymentAttachmentDto>>> Handle(
        GetPaymentAttachmentsQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await repository.ListAsync(request.PaymentId, cancellationToken);
        return Result<IReadOnlyList<PaymentAttachmentDto>>.Success(rows);
    }

    public async Task<Result<PaymentAttachmentFile>> Handle(
        DownloadPaymentAttachmentQuery request,
        CancellationToken cancellationToken)
    {
        var found = await repository.GetAsync(request.AttachmentId, cancellationToken)
            ?? throw new NotFoundException("PaymentAttachment", request.AttachmentId);

        var content = await storage.ReadAsync(found.StorageKey, cancellationToken)
            ?? throw new NotFoundException("PaymentAttachment", request.AttachmentId);

        return Result<PaymentAttachmentFile>.Success(
            new PaymentAttachmentFile(found.Attachment.FileName, found.Attachment.ContentType, content));
    }

    public async Task<Result<OrderProofStatusDto>> Handle(
        GetOrderProofStatusQuery request,
        CancellationToken cancellationToken)
    {
        return Result<OrderProofStatusDto>.Success(await repository.GetProofStatusAsync(request.OrderId, cancellationToken));
    }
}
