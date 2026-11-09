using System.Text.Json;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.DependencyInjection;
using IAdetector.Infrastructure.Ml;
using Microsoft.Extensions.DependencyInjection;

namespace IAdetector.Integration.Tests;

/// <summary>
/// Guardrail de no-regresión (no gate estricto de CI): mide accuracy/AUC del ensemble completo
/// sobre un subconjunto de HC3 (open_qa) que NUNCA se usó para calibrar los pesos (calibrados
/// sobre wiki_csai) — chequea que el ensemble generaliza a otro dominio, no solo que memoriza el
/// set de calibración. Se omite si el dataset o los modelos ONNX no están disponibles localmente.
/// </summary>
public class EndToEndAccuracyTests
{
    // ~300 muestras: tiempo de ejecución razonable para un test que corre 2 modelos ONNX por
    // muestra, manteniendo suficiente tamaño para que la accuracy medida sea representativa.
    private const int MaxQuestions = 150;
    private const double MinAcceptableAccuracy = 0.80;

    [SkippableFact]
    public async Task DetectAsync_HeldOutHc3Subset_MeetsMinimumAccuracy()
    {
        // Omisión explícita con motivo (Xunit.SkippableFact): antes esto era un return
        // silencioso y la prueba contaba como superada sin haber medido nada.
        var datasetPath = FindDatasetPath();
        Skip.If(datasetPath is null, "Dataset held-out (scripts/calibration/data/open_qa.jsonl) no encontrado localmente.");

        var cache = new FileSystemModelCache();
        Skip.If(
            !cache.IsDownloaded(ModelCatalog.RobertaOpenAiDetector) || !cache.IsDownloaded(ModelCatalog.DistilGpt2Perplexity),
            "Modelos ONNX no descargados localmente (roberta-openai-detector, distilgpt2-perplexity).");

        var services = new ServiceCollection();
        services.AddIAdetectorInfrastructure();
        using var provider = services.BuildServiceProvider();
        var detectText = provider.GetRequiredService<DetectTextUseCase>();

        var samples = LoadSamples(datasetPath!, MaxQuestions);
        Assert.True(samples.Count > 0, "No se pudo cargar ninguna muestra del dataset held-out.");

        var correct = 0;
        var scoresWithLabels = new List<(double Score, int Label)>(samples.Count);

        foreach (var (text, label) in samples)
        {
            var result = await detectText.DetectAsync(text);
            var predictedAi = result.AiProbability >= 0.5 ? 1 : 0;
            if (predictedAi == label)
            {
                correct++;
            }

            scoresWithLabels.Add((result.AiProbability, label));
        }

        var accuracy = correct / (double)samples.Count;
        var auc = ComputeAuc(scoresWithLabels);

        Assert.True(
            accuracy >= MinAcceptableAccuracy,
            $"Accuracy {accuracy:P1} por debajo del mínimo aceptable {MinAcceptableAccuracy:P0} sobre {samples.Count} muestras held-out de otro dominio (AUC={auc:F3}).");
    }

    private static string? FindDatasetPath()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "calibration", "data", "open_qa.jsonl"));
        return File.Exists(path) ? path : null;
    }

    private static List<(string Text, int Label)> LoadSamples(string path, int maxQuestions)
    {
        var samples = new List<(string, int)>();
        var questionsProcessed = 0;

        foreach (var line in File.ReadLines(path))
        {
            if (questionsProcessed >= maxQuestions)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            AddAnswers(root, "human_answers", label: 0, samples);
            AddAnswers(root, "chatgpt_answers", label: 1, samples);

            questionsProcessed++;
        }

        return samples;
    }

    private static void AddAnswers(JsonElement root, string property, int label, List<(string, int)> samples)
    {
        if (!root.TryGetProperty(property, out var answers))
        {
            return;
        }

        foreach (var answer in answers.EnumerateArray())
        {
            var text = answer.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                samples.Add((text, label));
            }
        }
    }

    private static double ComputeAuc(List<(double Score, int Label)> data)
    {
        var positives = data.Where(d => d.Label == 1).Select(d => d.Score).ToList();
        var negatives = data.Where(d => d.Label == 0).Select(d => d.Score).ToList();
        if (positives.Count == 0 || negatives.Count == 0)
        {
            return double.NaN;
        }

        double concordantScore = 0;
        foreach (var p in positives)
        {
            foreach (var n in negatives)
            {
                if (p > n)
                {
                    concordantScore += 1.0;
                }
                else if (p == n)
                {
                    concordantScore += 0.5;
                }
            }
        }

        return concordantScore / (positives.Count * (double)negatives.Count);
    }
}
