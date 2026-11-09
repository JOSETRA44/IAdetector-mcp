// Herramienta de desarrollo (no se distribuye con el producto): extrae las señales del ensemble
// para un dataset etiquetado humano/IA y las vuelca a CSV, para calibrar offline los pesos del
// ensemble (ver scripts/calibration/fit_logistic_regression.py). Reutiliza DetectTextUseCase real,
// así las features de entrenamiento son exactamente las que produce el motor en producción.
//
// Formato de salida (Fase 1.2): además de label y las 7 señales, cada fila lleva la pregunta
// de origen (question_id, para poder validar agrupando por pregunta y que las respuestas a
// una misma pregunta no queden a ambos lados de un split), el dataset de origen (source) y,
// por señal, si la señal midió de verdad (<señal>_hasdata = 1) o devolvió el 0.5 convencional
// de «sin datos» (0). Todos los números se escriben con cultura invariante: con la cultura
// del sistema, una máquina con coma decimal rompía el CSV.
using System.Globalization;
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
var source = Path.GetFileNameWithoutExtension(inputPath);

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
await writer.WriteLineAsync(
    "question_id,source,label," + string.Join(',', signalNames) + "," +
    string.Join(',', signalNames.Select(name => $"{name}_hasdata")));

var processed = 0;
var questionId = 0;

foreach (var line in File.ReadLines(inputPath))
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    using var doc = JsonDocument.Parse(line);
    var root = doc.RootElement;
    questionId++;

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
            var signals = signalNames.Select(name =>
                result.Signals.FirstOrDefault(s => s.Name == name)).ToArray();
            var scores = signals.Select(s => (s?.NormalizedScore ?? 0.5).ToString("F6", CultureInfo.InvariantCulture));
            var hasData = signals.Select(s => s is not null && s.HasData ? "1" : "0");

            await writer.WriteLineAsync(
                $"{questionId},{source},{label},{string.Join(',', scores)},{string.Join(',', hasData)}");
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
