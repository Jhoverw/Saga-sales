using SagaSales.Payment.Services;
using Xunit;

namespace SagaSales.UnitTests.Payment;

/// <summary>Spec 007, FR-001: el tipo del comprobante se decide por su firma, no por el nombre.</summary>
public class ReceiptFileTypeTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 }, "application/pdf")]
    public void Known_signatures_are_detected(byte[] head, string expected)
    {
        Assert.Equal(expected, ReceiptFileType.Detect(head));
    }

    [Theory]
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E })] // <html>
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 })] // ejecutable
    [InlineData(new byte[] { 0xFF, 0xD8 })] // demasiado corto
    [InlineData(new byte[] { })]
    public void Anything_else_is_refused(byte[] head)
    {
        Assert.Null(ReceiptFileType.Detect(head));
    }

    [Fact]
    public void The_storage_key_never_uses_the_client_file_name()
    {
        var tenant = Guid.NewGuid();
        var id = Guid.NewGuid();

        var key = ReceiptStorage.KeyFor(tenant, id, "application/pdf");

        Assert.Equal($"{tenant:N}/{id:N}.pdf", key);
    }
}
