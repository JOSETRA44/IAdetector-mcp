// Herramienta de desarrollo (no se distribuye con el producto): extrae las señales del ensemble
// para un dataset etiquetado humano/IA y las vuelca a CSV, para calibrar offline los pesos del
// ensemble (ver scripts/calibration/fit_logistic_regression.py). Reutiliza DetectTextUseCase real,
// así las features de entrenamiento son exactamente las que produce el motor en producción.
using System.Text.Json;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: IAdetector.Calibration <entrada.jsonl (formato HC3)> <salida.csv>");
    return 1;
}

var inputPath = args[0];
var outputPath = args[1];

var services = new ServiceCollection();
services.AddIAdetectorInfrastructure();
using var provider = services.BuildServiceProvider();
var detectText = provider.GetRequiredService<DetectTextUseCase>();

string[] signalNames =
[
    "burstiness",
    "type-token-ratio",
    "lexical-entropy",
    "compression-ratio",
    "ngram-repetition",
    "onnx-classifier",
    "perplexity",
];

await using var writer = new StreamWriter(outputPath);
await writer.WriteLineAsync("label," + string.Join(',', signalNames));

var processed = 0;

foreach (var line in File.ReadLines(inputPath))
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    using var doc = JsonDocument.Parse(line);
    var root = doc.RootElement;

    foreach (var (property, label) in new (string, int)[] { ("human_answers", 0), ("chatgpt_answers", 1) })
    {
        if (!root.TryGetProperty(property, out var answers))
        {
            continue;
        }

        foreach (var answerElement in answers.EnumerateArray())
        {
            var text = answerElement.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var result = await detectText.DetectAsync(text);
            var scores = signalNames.Select(name =>
                result.Signals.FirstOrDefault(s => s.Name == name)?.NormalizedScore ?? 0.5);

            await writer.WriteLineAsync($"{label},{string.Join(',', scores.Select(s => s.ToString("F6")))}");
            processed++;

            if (processed % 100 == 0)
            {
                Console.Error.WriteLine($"Procesados: {processed}");
            }
        }
    }
}

Console.Error.WriteLine($"Listo: {processed} muestras escritas en {outputPath}");
return 0;
