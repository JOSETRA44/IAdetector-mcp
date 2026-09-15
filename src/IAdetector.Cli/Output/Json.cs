using System.Text.Encodings.Web;
using System.Text.Json;

namespace IAdetector.Cli.Output;

/// <summary>Opciones compartidas del escritor JSON.</summary>
/// <remarks>
/// El codificador por defecto escapa todo lo que no sea ASCII, así que un veredicto sobre un
/// texto en español sale como <c>"análisis"</c>: sigue siendo JSON válido, pero ilegible
/// para quien lo revisa y ruidoso en un diff. El codificador relajado escribe UTF-8 tal cual,
/// que es lo correcto para una salida de terminal (el escapado estricto existe para incrustar
/// JSON dentro de HTML, cosa que aquí no ocurre).
/// </remarks>
internal static class Json
{
    // "Unsafe" se refiere a incrustar el resultado en HTML sin escapar; aquí la salida va a
    // una terminal o a un archivo, nunca a una página, así que lo apropiado es no escapar
    // apóstrofos ni signos de menor/mayor y dejar el JSON legible.
    public static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    public static JsonWriterOptions Options(bool indented) => new()
    {
        Indented = indented,
        Encoder = Encoder,
    };
}
