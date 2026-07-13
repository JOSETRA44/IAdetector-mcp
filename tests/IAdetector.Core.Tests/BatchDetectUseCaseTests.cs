using IAdetector.Core.Domain;
using IAdetector.Core.Tests.Fakes;
using IAdetector.Core.UseCases;
using Xunit;

namespace IAdetector.Core.Tests;

public class BatchDetectUseCaseTests
{
    [Fact]
    public async Task DetectFilesAsync_MixOfValidAndMissingFiles_ReportsErrorPerFileWithoutAbortingBatch()
    {
        var analyzer = new RecordingAnalyzer("a", s => new SignalScore("a", 0.1, 0.1, 1.0, "a"));
        var detectText = new DetectTextUseCase([analyzer], new RecordingScorer());
        var batch = new BatchDetectUseCase(detectText);

        var validFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(validFile, "some perfectly valid text content here");
        var missingFile = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.txt");

        try
        {
            var entries = await batch.DetectFilesAsync([validFile, missingFile]);

            Assert.Equal(2, entries.Count);

            var validEntry = entries.Single(e => e.Path == validFile);
            Assert.NotNull(validEntry.Result);
            Assert.Null(validEntry.Error);

            var missingEntry = entries.Single(e => e.Path == missingFile);
            Assert.Null(missingEntry.Result);
            Assert.NotNull(missingEntry.Error);
        }
        finally
        {
            File.Delete(validFile);
        }
    }
}
