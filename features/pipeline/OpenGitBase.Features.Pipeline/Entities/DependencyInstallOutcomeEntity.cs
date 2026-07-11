using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Entities;

public sealed class DependencyInstallOutcomeEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string RecipeKey { get; set; } = string.Empty;

    public bool Success { get; set; }

    public int ExitCode { get; set; }

    public long DurationMs { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
