using System.Security.Cryptography;
using IAdetector.Infrastructure.Ml;
using Xunit;

namespace IAdetector.Infrastructure.Tests;

public class ModelIntegrityTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("iadetector-integrity").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task VerifyAsync_EmptyDirectory_AllMissingAndNotIntact()
    {
        var report = await ModelIntegrity.VerifyAsync(ModelCatalog.RobertaOpenAiDetector, _dir);

        Assert.False(report.IsIntact);
        Assert.All(report.Files, f => Assert.Equal(ModelFileState.Missing, f.State));
    }

    [Fact]
    public async Task VerifyAsync_PresentButWrongContent_ReportsCorrupt()
    {
        // Contenido cualquiera bajo el nombre de un archivo catalogado: su hash no puede
        // coincidir con el del catálogo, así que debe salir Corrupt (no Missing ni Ok).
        await File.WriteAllTextAsync(Path.Combine(_dir, "vocab.json"), "esto no es el vocabulario");

        var report = await ModelIntegrity.VerifyAsync(ModelCatalog.RobertaOpenAiDetector, _dir);

        var vocab = Assert.Single(report.Files, f => f.FileName == "vocab.json");
        Assert.Equal(ModelFileState.Corrupt, vocab.State);
        Assert.False(report.IsIntact);
    }

    [Fact]
    public async Task VerifyFileAsync_MatchingHash_True_MismatchAndMissing_False()
    {
        var path = Path.Combine(_dir, "file.bin");
        var content = "contenido de prueba"u8.ToArray();
        await File.WriteAllBytesAsync(path, content);
        var hash = Convert.ToHexStringLower(SHA256.HashData(content));

        Assert.True(await ModelIntegrity.VerifyFileAsync(path, hash));
        Assert.False(await ModelIntegrity.VerifyFileAsync(path, new string('0', 64)));
        Assert.False(await ModelIntegrity.VerifyFileAsync(Path.Combine(_dir, "no-existe.bin"), hash));
    }
}
