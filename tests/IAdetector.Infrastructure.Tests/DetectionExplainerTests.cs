using IAdetector.Core.Domain;
using IAdetector.Infrastructure.Ensemble;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

/// <summary>
/// Pruebas del explicador prescriptivo con pesos y rangos sintéticos: la contribución es
/// β × puntuación, el estado compara contra el rango humano y las acciones priorizadas
/// salen solo de señales con datos y fuera de rango, ordenadas por contribución.
/// </summary>
public class DetectionExplainerTests
{
    private static readonly EnsembleWeightsData Weights = new(
        Intercept: -1.0,
        Weights: new Dictionary<string, double> { ["burstiness"] = 2.0, ["perplexity"] = 3.0 });

    private static readonly Dictionary<string, HumanRange> Ranges = new()
    {
        ["burstiness"] = new HumanRange(0.1, 0.5, 0.9, 599),
        ["perplexity"] = new HumanRange(0.0, 0.15, 0.45, 838),
    };

    private static DetectionExplainer Explainer() => new(Weights, Ranges);

    private static DetectionResult ResultWith(params SignalScore[] signals) =>
        new(Verdict.LikelyAI, 0.8, 0.9, signals, ChunkInfo: null);

    [Fact]
    public void Explain_AboveHumanRange_ReportsContributionStatusAndPrioritizedAction()
    {
        var explanation = Explainer().Explain(ResultWith(
            new SignalScore("burstiness", 0.3, 0.95, 1.0, "")));

        var diagnostic = Assert.Single(explanation.Signals);
        Assert.Equal(SignalStatus.AboveHumanRange, diagnostic.Status);
        Assert.Equal(2.0, diagnostic.Coefficient);
        Assert.Equal(1.9, diagnostic.LogitContribution!.Value, precision: 10);
        Assert.Contains("burstiness", Assert.Single(explanation.PrioritizedActions));
        Assert.Equal(1, explanation.SignalsWithData);
    }

    [Fact]
    public void Explain_InHumanRange_NoAction()
    {
        var explanation = Explainer().Explain(ResultWith(
            new SignalScore("burstiness", 0.3, 0.5, 1.0, "")));

        Assert.Equal(SignalStatus.InHumanRange, explanation.Signals[0].Status);
        Assert.Empty(explanation.PrioritizedActions);
    }

    [Fact]
    public void Explain_NoData_MarksStatusAndExplainsWhy()
    {
        var explanation = Explainer().Explain(ResultWith(
            new SignalScore("burstiness", 0.0, 0.5, 1.0, "", HasData: false)));

        var diagnostic = explanation.Signals[0];
        Assert.Equal(SignalStatus.NoData, diagnostic.Status);
        Assert.Contains("Sin datos", diagnostic.Recommendation);
        Assert.Equal(0, explanation.SignalsWithData);
        Assert.Empty(explanation.PrioritizedActions);
    }

    [Fact]
    public void Explain_TwoSignalsAboveRange_ActionsOrderedByContribution()
    {
        var explanation = Explainer().Explain(ResultWith(
            new SignalScore("burstiness", 0.3, 0.95, 1.0, ""),   // contribución 1.9
            new SignalScore("perplexity", 120.0, 0.9, 1.0, ""))); // contribución 2.7

        Assert.Equal(2, explanation.PrioritizedActions.Count);
        Assert.Contains("perplexity", explanation.PrioritizedActions[0]);
        Assert.Contains("burstiness", explanation.PrioritizedActions[1]);
    }

    [Fact]
    public void Explain_SignalWithoutWeights_NullContributionButStillDiagnosed()
    {
        var explanation = Explainer().Explain(ResultWith(
            new SignalScore("desconocida", 1.0, 0.7, 1.0, "")));

        var diagnostic = explanation.Signals[0];
        Assert.Null(diagnostic.Coefficient);
        Assert.Null(diagnostic.LogitContribution);
        Assert.Equal(SignalStatus.NoReference, diagnostic.Status);
    }
}
