// Herramienta de desarrollo (no se distribuye con el producto): mide la latencia de
// detección de extremo a extremo para tamaños de texto representativos, sin caché de
// resultados (cómputo puro) y con ella (acierto). Sirve para comparar antes/después de
// cambios de rendimiento con el mismo procedimiento (ver docs/PERFORMANCE.md).
//
// Uso: IAdetector.Benchmarks [repeticionesEnCaliente]
// Requiere los modelos ONNX descargados para medir la ruta completa; sin ellos mide la
// ruta degradada (y lo dice la probabilidad resultante no cambia, pero sí las señales).
using System.Diagnostics;
using IAdetector.Core.Abstractions;
using IAdetector.Core.UseCases;
using IAdetector.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var warmRepeats = args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : 5;

var services = new ServiceCollection();
services.AddIAdetectorInfrastructure();
using var provider = services.BuildServiceProvider();

// Sin caché: el caso de uso de DI la lleva conectada; aquí interesa el costo de cómputo.
var computeOnly = new DetectTextUseCase(
    provider.GetServices<ISignalAnalyzer>(),
    provider.GetRequiredService<IEnsembleScorer>());
var withCache = provider.GetRequiredService<DetectTextUseCase>();

int[] sizes = [150, 800, 3_000, 12_000];

Console.WriteLine("words,cold_ms,warm_median_ms,cached_ms,ai_probability,chunks");
foreach (var size in sizes)
{
    var text = SyntheticText.Build(size);

    var coldWatch = Stopwatch.StartNew();
    var result = await computeOnly.DetectAsync(text);
    coldWatch.Stop();

    var warm = new List<long>();
    for (var i = 0; i < warmRepeats; i++)
    {
        var watch = Stopwatch.StartNew();
        await computeOnly.DetectAsync(text);
        watch.Stop();
        warm.Add(watch.ElapsedMilliseconds);
    }

    warm.Sort();

    // Primera pasada con la caché real (puebla) y segunda (acierto medido).
    await withCache.DetectAsync(text);
    var cachedWatch = Stopwatch.StartNew();
    await withCache.DetectAsync(text);
    cachedWatch.Stop();

    Console.WriteLine(
        $"{size},{coldWatch.ElapsedMilliseconds},{warm[warm.Count / 2]},{cachedWatch.ElapsedMilliseconds}," +
        $"{result.AiProbability:F3},{result.ChunkInfo?.TotalChunks.ToString() ?? "-"}");
}

/// <summary>
/// Texto sintético determinista: un fondo de frases variadas (longitudes y temas distintos,
/// para que burstiness y compañía midan algo plausible) barajado con semilla fija y cortado
/// al número de palabras pedido. No pretende engañar al detector: pretende costar lo mismo
/// que un texto real de ese tamaño en tokenización e inferencia.
/// </summary>
internal static class SyntheticText
{
    private static readonly string[] Sentences =
    [
        "The committee published its findings after a long and occasionally heated debate.",
        "It rained on Tuesday.",
        "María repasó la lista una vez más, tachando los nombres que ya no vivían en el barrio.",
        "According to the report, exports grew by four percent during the second quarter, driven mainly by agricultural goods and a weak currency.",
        "Nobody answered.",
        "The old lighthouse had survived storms that swallowed larger and prouder buildings along the coast.",
        "Please submit the form before Friday, and remember to attach two copies of the invoice, the signed annex, and a photograph of the installed equipment.",
        "He laughed.",
        "In 1873 the railway finally reached the valley, and with it came merchants, newspapers, and a modest hotel with twelve rooms.",
        "Why?",
        "The recipe calls for patience more than for ingredients: a slow fire, a heavy pot, and the discipline not to lift the lid too often.",
        "Sales of the new model exceeded every forecast the company had dared to print that spring.",
        "A veces el río baja tan despacio que parece detenido, y los niños aprovechan para cruzarlo a pie.",
        "The algorithm processes each document in a single pass, although the intermediate representation can grow considerably when the input is adversarial.",
        "Three.",
        "When the meeting ended, Lucía stayed behind to water the plants and rearrange the chairs the way she liked them.",
        "It is worth noting that the effect, while statistically significant, remains small in absolute terms and may not justify the cost of the intervention.",
        "The dog barked at the mailman, as it did every morning, with great conviction and no success.",
        "Bring water.",
        "The museum will remain closed during the renovation, which the director hopes — perhaps optimistically — to finish before the winter season begins.",
        "Every autumn the swallows gather on the wires above the plaza, thousands of them, chattering like a market at dawn.",
        "The contract, all forty pages of it, said remarkably little about what would happen if either party simply walked away.",
    ];

    public static string Build(int words)
    {
        var random = new Random(42);
        var pool = new List<string>();
        var total = 0;
        while (total < words)
        {
            var sentence = Sentences[random.Next(Sentences.Length)];
            pool.Add(sentence);
            total += sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        var joined = string.Join(' ', pool);
        var tokens = joined.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', tokens.Take(words));
    }
}
