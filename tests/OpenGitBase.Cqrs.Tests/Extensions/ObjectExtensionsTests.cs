namespace OpenGitBase.Cqrs.Tests.Extensions;

public class ObjectExtensionsTests
{
    [Fact]
    public void ThrowIfNull_WhenNotNull_DoesNotThrow()
    {
        var value = "value";

        var exception = Record.Exception(() => value.ThrowIfNull(nameof(value)));

        Assert.Null(exception);
    }

    [Fact]
    public void ThrowIfNull_WhenNull_ThrowsArgumentNullException()
    {
        string? value = null;

        var exception = Assert.Throws<ArgumentNullException>(() => value.ThrowIfNull("param"));

        Assert.Equal("param", exception.ParamName);
    }
}
