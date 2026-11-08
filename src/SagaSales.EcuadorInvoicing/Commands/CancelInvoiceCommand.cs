using MediatR;
using SagaSales.EcuadorInvoicing.Domain;

namespace SagaSales.EcuadorInvoicing.Commands;

public record CancelInvoiceCommand(Guid InvoiceId) : IRequest;
