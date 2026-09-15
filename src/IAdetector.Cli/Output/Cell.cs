using System.Globalization;
using System.Text.Json;

namespace IAdetector.Cli.Output;

internal enum CellKind
{
    Null,
    Text,
    Integer,
    Real,
    Boolean,
}

/// <summary>
/// Un valor de celda con su tipo JSON conservado. Guardar los valores ya formateados como string
/// haría que <c>-f json</c> emitiera <c>"0.8123"</c> en vez de <c>0.8123</c>, y entonces un
/// <c>jq 'select(.ai_probability &gt; 0.8)'</c> — el uso típico de un agente — dejaría de funcionar.
/// Es un struct de campos fijos en vez de <c>object</c> para no depender de serialización por
/// reflexión, incompatible con Native AOT.
/// </summary>
internal readonly record struct Cell(CellKind Kind, string? Text, double Number, int Decimals)
{
    public static readonly Cell Empty = new(CellKind.Null, null, 0, 0);

    public static Cell Of(string? value) => value is null ? Empty : new Cell(CellKind.Text, value, 0, 0);

    public static Cell Of(long value) => new(CellKind.Integer, null, value, 0);

    public static Cell Of(double value, int decimals = 4) => new(CellKind.Real, null, value, decimals);

    public static Cell Of(bool value) => new(CellKind.Boolean, null, value ? 1 : 0, 0);

    /// <summary>Representación textual para <c>table</c>, <c>csv</c> y <c>md</c>. Siempre en cultura invariante.</summary>
    public string Display => Kind switch
    {
        CellKind.Text => Text ?? string.Empty,
        CellKind.Integer => ((long)Number).ToString(CultureInfo.InvariantCulture),
        CellKind.Real => Number.ToString("F" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
        CellKind.Boolean => Number != 0 ? "true" : "false",
        _ => string.Empty,
    };

    public void WriteValue(Utf8JsonWriter writer)
    {
        switch (Kind)
        {
            case CellKind.Text:
                writer.WriteStringValue(Text);
                break;
            case CellKind.Integer:
                writer.WriteNumberValue((long)Number);
                break;
            case CellKind.Real:
                writer.WriteNumberValue(Math.Round(Number, Decimals));
                break;
            case CellKind.Boolean:
                writer.WriteBooleanValue(Number != 0);
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
