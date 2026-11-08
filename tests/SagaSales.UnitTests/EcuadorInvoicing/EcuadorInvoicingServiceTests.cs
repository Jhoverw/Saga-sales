using System;
using System.Threading.Tasks;
using Moq;
using SagaSales.EcuadorInvoicing.Domain;
using SagaSales.EcuadorInvoicing.Infrastructure;
using SagaSales.EcuadorInvoicing.Services;
using SagaSales.EcuadorInvoicing.DTOs;
using SagaSales.EcuadorInvoicing.Commands;
using MediatR;
using Xunit;

namespace SagaSales.UnitTests.EcuadorInvoicing;

public class EcuadorInvoicingServiceTests
{
    [Fact]
    public async Task CreateInvoice_Generates_ClaveAcceso_And_Sends_To_Sri()
    {
        // Arrange – mock SriClient to return an accepted response.
        var sriResponse = new SriResponse { Estado = "AUTORIZADO", Mensaje = "OK", ClaveAcceso = "FAKE-CLAVE" };
        var sriMock = new Mock<SriClient>();
        sriMock.Setup(s => s.SendInvoiceAsync(It.IsAny<string>()))
               .ReturnsAsync(sriResponse);

        // Self-signed certificate with a private key: SignXml needs one to sign.
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var certRequest = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=SagaSalesTest",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var cert = certRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var service = new EcuadorInvoicingService(sriMock.Object, cert);
        var dto = new CreateInvoiceDto
        {
            RucComprador = "9999999999",
            Serie = "001",
            Numero = 1,
            Items = new()
            {
                new InvoiceItemDto
                {
                    CodigoInterno = "PROD001",
                    Descripcion = "Producto Test",
                    Cantidad = 2,
                    PrecioUnitario = 50m,
                    Descuento = 0,
                    CodigoImpuesto = "2" // IVA 12%
                }
            }
        };

        // Act
        var invoice = await service.Handle(new CreateInvoiceCommand(dto), default);

        // Assert
        Assert.NotNull(invoice);
        Assert.Equal("AUTORIZADO", invoice.Estado);
        Assert.NotEmpty(invoice.ClaveAcceso);
        // ClaveAcceso length must be 49 digits.
        Assert.Equal(49, invoice.ClaveAcceso.Length);
    }
}
