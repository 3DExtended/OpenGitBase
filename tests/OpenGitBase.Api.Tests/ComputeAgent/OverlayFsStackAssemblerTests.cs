using OpenGitBase.ComputeAgent;

namespace OpenGitBase.Api.Tests.ComputeAgent;

public class OverlayFsStackAssemblerTests
{
    [Fact]
    public async Task AssembleAsync_BuildsBaseAndUpperLayers()
    {
        var workRoot = Path.Combine(Path.GetTempPath(), $"ogb-overlay-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workRoot);
        try
        {
            var lowerRoot = Path.Combine(workRoot, "base-layer");
            Directory.CreateDirectory(lowerRoot);
            await File.WriteAllTextAsync(Path.Combine(lowerRoot, ".ogb-base-image"), "test");

            var assembler = new OverlayFsStackAssembler();
            var result = await assembler.AssembleAsync(
                new OverlayFsStackRequest
                {
                    JobId = Guid.NewGuid(),
                    BaseImageArtifactPath = lowerRoot,
                    WorkRoot = workRoot,
                },
                CancellationToken.None
            );

            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.MergedRootPath));
            Assert.Contains(result.LogLines, line => line.Contains("base image layer", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(result.MergedRootPath!, ".ogb-base-image")));
        }
        finally
        {
            if (Directory.Exists(workRoot))
            {
                Directory.Delete(workRoot, recursive: true);
            }
        }
    }
}
