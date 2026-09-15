using System.Collections;

namespace IAdetector.Cli.Output;

/// <summary>
/// Una fila de la proyección tabular: pares nombre/valor en orden de inserción. Es la unidad
/// común a los cinco formatos — <c>table</c>, <c>csv</c> y <c>md</c> pintan sus <see cref="Cell.Display"/>,
/// <c>json</c> y <c>jsonl</c> emiten los valores tipados. Con eso, un lote analizado sale como
/// una tabla plana que se abre tal cual en una hoja de cálculo.
/// </summary>
internal sealed class Row : IReadOnlyList<KeyValuePair<string, Cell>>
{
    private readonly List<KeyValuePair<string, Cell>> _cells = [];

    public int Count => _cells.Count;

    public KeyValuePair<string, Cell> this[int index] => _cells[index];

    public Row Add(string name, Cell value)
    {
        _cells.Add(new KeyValuePair<string, Cell>(name, value));
        return this;
    }

    public Row Add(string name, string? value) => Add(name, Cell.Of(value));

    public Row Add(string name, long value) => Add(name, Cell.Of(value));

    public Row Add(string name, double value, int decimals = 4) => Add(name, Cell.Of(value, decimals));

    public Row Add(string name, bool value) => Add(name, Cell.Of(value));

    public IEnumerator<KeyValuePair<string, Cell>> GetEnumerator() => _cells.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Cabecera del conjunto de filas. No se toma solo de la primera fila: en un lote con
    /// <c>--verbose</c>, un archivo que falló no tiene columnas de señales y otro sí, y la unión
    /// ordenada por primera aparición evita que el CSV pierda columnas según qué archivo salga primero.
    /// </summary>
    public static IReadOnlyList<string> HeaderOf(IEnumerable<Row> rows)
    {
        var header = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            foreach (var (name, _) in row)
            {
                if (seen.Add(name))
                {
                    header.Add(name);
                }
            }
        }

        return header;
    }

    public Cell Get(string name)
    {
        foreach (var (key, value) in _cells)
        {
            if (string.Equals(key, name, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return Cell.Empty;
    }
}
