using OpenGitBase.Pipeline;

namespace OpenGitBase.Pipeline.Tests;

public class DependencyRecipeKeysTests
{
    [Fact]
    public void Compute_SameBaseAndScript_ProducesStableKey()
    {
        var first = DependencyRecipeKeys.Compute("linux-base", "apt-get install git");
        var second = DependencyRecipeKeys.Compute("linux-base", "apt-get install git");
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Compute_NormalizesWhitespaceAndLineEndings()
    {
        var unix = DependencyRecipeKeys.Compute("linux-base", "apt-get install git\n");
        var windows = DependencyRecipeKeys.Compute("linux-base", "apt-get install git\r\n");
        var trailing = DependencyRecipeKeys.Compute("linux-base", "apt-get install git   ");
        Assert.Equal(unix, windows);
        Assert.Equal(unix, trailing);
    }

    [Fact]
    public void Compute_DifferentScripts_ProduceDifferentKeys()
    {
        var git = DependencyRecipeKeys.Compute("linux-base", "apt-get install git");
        var curl = DependencyRecipeKeys.Compute("linux-base", "apt-get install curl");
        Assert.NotEqual(git, curl);
    }
}

public class CiVariableComposerTests
{
    [Fact]
    public void BuildProjectPath_UsesOwnerAndRepositorySlug()
    {
        Assert.Equal("acme/widget", CiVariableComposer.BuildProjectPath("acme", "widget"));
    }

    [Fact]
    public void BuildProjectPathSlug_SlugifiesSegments()
    {
        Assert.Equal("acme-corp-widget-app", CiVariableComposer.BuildProjectPathSlug("Acme-Corp", "Widget-App"));
    }
}
