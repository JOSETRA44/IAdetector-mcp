using System.ComponentModel;
using System.Text.Json;
using IAdetector.Core.Domain;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.Serialization;
using ModelContextProtocol.Server;

namespace IAdetector.Mcp.Tools;

/// <summary>
/// Herramientas MCP para detección de texto generado por IA, 100% locales: ninguna de estas
/// tools llama a un servicio externo. El clasificador ONNX (Fase 2) se suma automáticamente a
/// las señales estadísticas cuando el modelo correspondiente esté descargado.
/// </summary>
[McpServerToolType]
public sealed class DetectionTools(DetectTextUseCase detectText, BatchDetectUseCase batchDetect)
{
    private const int MaxInlineTextLength = 20_000;
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    [McpServerTool(Name = "detect_text")]
    [Description("Analiza un texto y estima la probabilidad de que haya sido generado por IA, usando señales estadísticas/estilométricas locales (sin llamar a ninguna API externa).")]
    public async Task<string> DetectText(
        [Description("Texto a analizar (máximo 20000 caracteres; para textos más largos usar detect_file).")] string text,
        [Description("Si es true, incluye el desglose completo de cada señal individual con su explicación.")] bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("El texto no puede estar vacío.", nameof(text));
        }

        if (text.Length > MaxInlineTextLength)
        {
            throw new ArgumentException(
                $"El texto excede el límite de {MaxInlineTextLength} caracteres para análisis inline ({text.Length} recibidos). Usa 'detect_file' para archivos largos.",
                nameof(text));
        }

        var result = await detectText.DetectAsync(text, cancellationToken).ConfigureAwait(false);
        return Serialize(result, verbose);
    }

    [McpServerTool(Name = "detect_file")]
    [Description("Analiza el contenido de un archivo de texto local y estima la probabilidad de que haya sido generado por IA. Los archivos largos se fragmentan automáticamente.")]
    public async Task<string> DetectFile(
        [Description("Ruta absoluta del archivo de texto a analizar.")] string path,
        [Description("Si es true, incluye el desglose completo de cada señal individual.")] bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No se encontró el archivo: {path}", path);
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException(
                $"El archivo pesa {fileInfo.Length / (1024 * 1024)} MB, excede el límite de {MaxFileSizeBytes / (1024 * 1024)} MB.",
                nameof(path));
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var result = await detectText.DetectAsync(text, cancellationToken).ConfigureAwait(false);
        return Serialize(result, verbose);
    }

    [McpServerTool(Name = "batch_detect_directory")]
    [Description("Analiza todos los archivos de texto de un directorio que coincidan con un patrón, devolviendo un resultado por archivo.")]
    public async Task<string> BatchDetectDirectory(
        [Description("Ruta absoluta del directorio a analizar.")] string path,
        [Description("Patrón glob de archivos a incluir, ej. '*.txt'.")] string pattern = "*.txt",
        [Description("Si es true, busca también en subdirectorios.")] bool recursive = false,
        [Description("Número máximo de archivos a procesar en esta llamada.")] int maxFiles = 100,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"No se encontró el directorio: {path}");
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(path, pattern, searchOption).Take(maxFiles).ToList();

        if (files.Count == 0)
        {
            return JsonSerializer.Serialize(
                new NoFilesFoundMessage("No se encontraron archivos que coincidan con el patrón.", path, pattern),
                DetectionJson.Options);
        }

        var entries = await batchDetect.DetectFilesAsync(files, maxDegreeOfParallelism: 4, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.Serialize(
            entries.Select(e => DetectionResultProjection.Apply(e, verbose: false)).ToList(),
            DetectionJson.Options);
    }

    [McpServerTool(Name = "explain_detection")]
    [Description("Como detect_text, pero siempre devuelve el desglose completo de señales con explicación en lenguaje natural de cada una.")]
    public Task<string> ExplainDetection(
        [Description("Texto a analizar (máximo 20000 caracteres).")] string text,
        CancellationToken cancellationToken = default)
        => DetectText(text, verbose: true, cancellationToken);

    private static string Serialize(DetectionResult result, bool verbose) =>
        JsonSerializer.Serialize(DetectionResultProjection.Apply(result, verbose), DetectionJson.Options);
}
