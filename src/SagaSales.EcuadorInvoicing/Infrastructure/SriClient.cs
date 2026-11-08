using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace SagaSales.EcuadorInvoicing.Infrastructure;

public class SriClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpointUrl; // SOAP or REST endpoint

    public SriClient(HttpClient httpClient, string endpointUrl)
    {
        _httpClient = httpClient;
        _endpointUrl = endpointUrl;
    }

    /// <summary>Parameterless constructor so the client can be mocked in unit tests.</summary>
    public SriClient()
        : this(new HttpClient(), string.Empty)
    {
    }

    public virtual async Task<SriResponse> SendInvoiceAsync(string xml)
    {
        var content = new StringContent(xml, Encoding.UTF8, "application/xml");
        var response = await _httpClient.PostAsync(_endpointUrl, content);
        response.EnsureSuccessStatusCode();
        var responseXml = await response.Content.ReadAsStringAsync();
        // Very simple parsing – in production a proper XSD‑validated parser is required
        var doc = XDocument.Parse(responseXml);
        var status = doc.Root?.Element("Respuesta")?.Element("Estado")?.Value ?? "UNKNOWN";
        var mensaje = doc.Root?.Element("Respuesta")?.Element("Mensaje")?.Value;
        var claveAcceso = doc.Root?.Element("Respuesta")?.Element("ClaveAcceso")?.Value;
        return new SriResponse
        {
            Estado = status,
            Mensaje = mensaje,
            ClaveAcceso = claveAcceso
        };
    }
}

public class SriResponse
{
    public string Estado { get; set; } = default!;
    public string? Mensaje { get; set; }
    public string? ClaveAcceso { get; set; }
}
