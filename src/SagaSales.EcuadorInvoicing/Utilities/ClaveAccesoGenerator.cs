using System;

namespace SagaSales.EcuadorInvoicing.Utilities;

public static class ClaveAccesoGenerator
{
    // Returns the 49‑digit clave de acceso according to SRI spec
    public static string Generate(
        DateTime fechaEmision,
        string tipoComprobante,
        string ruc,
        string ambiente,
        string serie,
        int numero,
        string codigoNumerico)
    {
        // 1. Fecha de emisión: ddMMyyyy (8 dígitos; la hora NO forma parte de la clave)
        var fechaStr = fechaEmision.ToString("ddMMyyyy");
        // 2. Tipo de comprobante (01=Factura, 04=Exportación, etc.)
        var tipo = tipoComprobante.PadLeft(2, '0');
        // 3. RUC
        var rucStr = ruc.PadLeft(13, '0');
        // 4. Ambiente (1=pruebas, 2=producción)
        var amb = ambiente;
        // 5. Serie: establecimiento (3) + punto de emisión (3) = 6 dígitos; número (9)
        var serieStr = serie.PadLeft(6, '0');
        var numeroStr = numero.ToString().PadLeft(9, '0');
        // 6. Código numérico (8‑digitos) – random or sequential
        var codNum = codigoNumerico.PadLeft(8, '0');
        // 7. Tipo de Emisión (1=normal, 2=indetenible)
        var tipoEmision = "1";
        var baseClave = $"{fechaStr}{tipo}{rucStr}{amb}{serieStr}{numeroStr}{codNum}{tipoEmision}";
        var dv = CalculateDv(baseClave);
        return baseClave + dv;
    }

    private static int CalculateDv(string claveSinDv)
    {
        int total = 0;
        int factor = 2;
        for (int i = claveSinDv.Length - 1; i >= 0; i--)
        {
            int val = (claveSinDv[i] - '0') * factor;
            total += val;
            factor = factor == 7 ? 2 : factor + 1;
        }
        int resto = total % 11;
        int dv = 11 - resto;
        // SRI: 11 maps to 0 and 10 maps to 1, so the digit is always one character and the
        // clave stays at 49 digits.
        if (dv == 11) dv = 0;
        if (dv == 10) dv = 1;
        return dv;
    }
}
