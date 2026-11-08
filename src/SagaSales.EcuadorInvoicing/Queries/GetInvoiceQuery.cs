using MediatR;
using SagaSales.EcuadorInvoicing.Domain;

namespace SagaSales.EcuadorInvoicing.Queries;

public record GetInvoiceQuery(Guid InvoiceId) : IRequest<Invoice?>;
