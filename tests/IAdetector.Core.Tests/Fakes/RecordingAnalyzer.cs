using IAdetector.Core.Abstractions;
using IAdetector.Core.Domain;

namespace IAdetector.Core.Tests.Fakes;

internal sealed class RecordingAnalyzer(string name, Func<TextSample, SignalScore> score) : ISignalAnalyzer
{
    public string Name => name;

    public Task<SignalScore> AnalyzeAsync(TextSample sample, CancellationToken cancellationToken = default) =>
        Task.FromResult(score(sample));
}
