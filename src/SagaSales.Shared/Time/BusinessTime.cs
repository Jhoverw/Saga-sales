namespace SagaSales.Shared.Time;

/// <summary>
/// Hora del negocio para lo que el servidor escribe como texto (CSV, nombres de archivo). Ecuador continental es
/// GMT−5 todo el año, sin horario de verano, así que el desfase es fijo y no depende de los datos de zona horaria
/// del contenedor. El panel convierte por su cuenta con la zona del navegador.
/// </summary>
public static class BusinessTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    /// <summary>Un instante UTC (o sin zona, como lo devuelve la base) en la hora de Ecuador.</summary>
    public static DateTime ToLocal(DateTime utc)
        => DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset), DateTimeKind.Unspecified);

    public static DateTime Now => ToLocal(DateTime.UtcNow);
}
