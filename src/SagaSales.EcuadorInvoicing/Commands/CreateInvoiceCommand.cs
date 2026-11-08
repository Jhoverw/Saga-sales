using MediatR;
using SagaSales.EcuadorInvoicing.Domain;
using SagaSales.EcuadorInvoicing.DTOs;

namespace SagaSales.EcuadorInvoicing.Commands;

public record CreateInvoiceCommand(CreateInvoiceDto Dto) : IRequest<Invoice>;
