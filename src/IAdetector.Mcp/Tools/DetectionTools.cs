using System.ComponentModel;
using System.Text.Json;
using IAdetector.Core.Domain;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.Serialization;
using ModelContextProtocol;
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
    private const int HardMaxFilesPerBatch = 1_000;

    [McpServerTool(Name = "detect_text", ReadOnly = true, OpenWorld = false)]
    [Description("Analiza un texto pegado directamente (no un archivo) y estima la probabilidad de que haya sido generado por IA, combinando 7 señales estadísticas y 2 modelos ONNX locales (sin llamar a ninguna API externa). Devuelve un JSON con veredicto, probabilidad de IA, confianza y desglose de señales. Para el contenido de un archivo en disco usa detect_file en su lugar — no lo leas tú mismo para pegarlo aquí. El texto analizado NUNCA sale de la máquina. El resultado se cachea con clave (texto + versión de los modelos), así que reanalizar el mismo texto es instantáneo. AL COMUNICAR EL RESULTADO: es evidencia probabilística, nunca prueba. Los falsos positivos afectan de forma desproporcionada a quien escribe en un idioma que no es el suyo y a la prosa técnica formulaica, y un texto de menos de un párrafo da confianza baja y poco informativa. No lo presentes como base suficiente para acusar a nadie.")]
    public async Task<string> DetectText(
        [Description("Texto a analizar (máximo 20000 caracteres; para textos más largos usar detect_file).")] string text,
        [Description("Si es true, incluye el desglose completo de cada señal individual con su explicación.")] bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new McpException("El texto no puede estar vacío.");
        }

        if (text.Length > MaxInlineTextLength)
        {
            throw new McpException(
                $"El texto excede el límite de {MaxInlineTextLength} caracteres para análisis inline ({text.Length} recibidos). Usa 'detect_file' para archivos largos.");
        }

        var result = await detectText.DetectAsync(text, cancellationToken).ConfigureAwait(false);
        return Serialize(result, verbose);
    }

    [McpServerTool(Name = "detect_file", ReadOnly = true, OpenWorld = false)]
    [Description("Analiza el contenido de un archivo de texto local (máximo 5MB) y estima la probabilidad de que haya sido generado por IA; fragmenta y, si es muy largo, muestrea automáticamente el documento. Devuelve el mismo JSON que detect_text (veredicto, probabilidad, confianza, señales). Para texto pegado directamente en la conversación usa detect_text en su lugar; para varios archivos a la vez usa batch_detect_directory.")]
    public async Task<string> DetectFile(
        [Description("Ruta absoluta del archivo de texto a analizar.")] string path,
        [Description("Si es true, incluye el desglose completo de cada señal individual.")] bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new McpException($"No se encontró el archivo: {path}");
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaxFileSizeBytes)
        {
            throw new McpException(
                $"El archivo pesa {fileInfo.Length / (1024 * 1024)} MB, excede el límite de {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var result = await detectText.DetectAsync(text, cancellationToken).ConfigureAwait(false);
        return Serialize(result, verbose);
    }

    [McpServerTool(Name = "batch_detect_directory", ReadOnly = true, OpenWorld = false)]
    [Description("Analiza todos los archivos de un directorio que coincidan con un patrón (por defecto *.txt), devolviendo un array JSON con un resultado (o error) por archivo. Úsala en vez de llamar a detect_file repetidamente cuando hay más de un archivo que analizar.")]
    public async Task<string> BatchDetectDirectory(
        [Description("Ruta absoluta del directorio a analizar.")] string path,
        [Description("Patrón glob de archivos a incluir, ej. '*.txt'.")] string pattern = "*.txt",
        [Description("Si es true, busca también en subdirectorios.")] bool recursive = false,
        [Description("Número máximo de archivos a procesar en esta llamada (entre 1 y 1000; por defecto 100).")] int maxFiles = 100,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(path))
        {
            throw new McpException($"No se encontró el directorio: {path}");
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var effectiveMaxFiles = Math.Clamp(maxFiles, 1, HardMaxFilesPerBatch);
        var files = Directory.EnumerateFiles(path, pattern, searchOption).Take(effectiveMaxFiles).ToList();

        if (files.Count == 0)
        {
            return JsonSerializer.Serialize(
                new NoFilesFoundMessage("No se encontraron archivos que coincidan con el patrón.", path, pattern),
                IAdetectorJsonContext.Default.NoFilesFoundMessage);
        }

        var entries = await batchDetect.DetectFilesAsync(files, maxDegreeOfParallelism: 4, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.Serialize(
            entries.Select(e => DetectionResultProjection.Apply(e, verbose: false)).ToList(),
            IAdetectorJsonContext.Default.ListBatchDetectionEntry);
    }

    [McpServerTool(Name = "explain_detection", ReadOnly = true, OpenWorld = false)]
    [Description("Equivalente a detect_text con verbose=true: analiza un texto pegado directamente y siempre devuelve el JSON con el desglose completo de señales y su explicación en lenguaje natural. Úsala cuando el usuario pida explícitamente el porqué de un veredicto; para el uso normal (solo veredicto/probabilidad) usa detect_text.")]
    public Task<string> ExplainDetection(
        [Description("Texto a analizar (máximo 20000 caracteres).")] string text,
        CancellationToken cancellationToken = default)
        => DetectText(text, verbose: true, cancellationToken);

    private static string Serialize(DetectionResult result, bool verbose) =>
        JsonSerializer.Serialize(DetectionResultProjection.Apply(result, verbose), IAdetectorJsonContext.Default.DetectionResult);
}
