using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SagaSales.EcuadorInvoicing.Utilities;

public static class DigitalSignatureHelper
{
    public static string SignXml(string xml, X509Certificate2 certificate)
    {
        // Simplified signing – real implementation should follow XML‑DSig spec
        var data = Encoding.UTF8.GetBytes(xml);
        using var rsa = certificate.GetRSAPrivateKey();
        var signature = rsa!.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }
}
