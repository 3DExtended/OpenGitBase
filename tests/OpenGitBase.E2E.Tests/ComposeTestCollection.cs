using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests;

public sealed class ComposeHealthFixture
{
    public ComposeHealthFixture()
    {
        ComposeHealthGate.Refresh();
    }
}

[CollectionDefinition("Compose", DisableParallelization = true)]
public sealed class ComposeTestCollection : ICollectionFixture<ComposeHealthFixture>;
