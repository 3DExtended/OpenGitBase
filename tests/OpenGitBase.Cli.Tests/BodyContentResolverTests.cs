namespace OpenGitBase.Cli.Tests;

public sealed class BodyContentResolverTests : IDisposable
{
    private readonly string? _tempFile;

    public BodyContentResolverTests()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, "file body");
    }

    public void Dispose()
    {
        if (_tempFile is not null && File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Fact]
    public void Prefers_body_file_over_inline_body()
    {
        var result = BodyContentResolver.Resolve("inline", new FileInfo(_tempFile!));
        Assert.Equal("file body", result);
    }

    [Fact]
    public void Missing_body_file_throws()
    {
        var missing = new FileInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md"));
        Assert.Throws<FileNotFoundException>(() => BodyContentResolver.Resolve(null, missing));
    }
}
