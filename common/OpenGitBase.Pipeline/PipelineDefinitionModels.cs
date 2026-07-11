namespace OpenGitBase.Pipeline;

public sealed class ParsePipelineResult
{
    public PipelineDefinition? Definition { get; init; }

    public IReadOnlyList<PipelineValidationError> Errors { get; init; } =
        Array.Empty<PipelineValidationError>();

    public bool IsValid => Definition is not null && Errors.Count == 0;
}

public sealed class PipelineValidationError
{
    public string Path { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

public sealed class PipelineDefinition
{
    public string? DefaultImage { get; init; }

    public IReadOnlyDictionary<string, string> DefaultVariables { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<DependencyRecipe> DefaultDependencies { get; init; } =
        Array.Empty<DependencyRecipe>();

    public IReadOnlyList<string> Stages { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ResolvedJob> Jobs { get; init; } = Array.Empty<ResolvedJob>();
}

public sealed class DependencyRecipe
{
    public string Name { get; init; } = string.Empty;

    public string? Version { get; init; }

    public string? InstallScript { get; init; }
}

public sealed class ResolvedJob
{
    public string Name { get; init; } = string.Empty;

    public string Stage { get; init; } = string.Empty;

    public string RunsOn { get; init; } = string.Empty;

    public string Image { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Variables { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<DependencyRecipe> Dependencies { get; init; } =
        Array.Empty<DependencyRecipe>();

    public IReadOnlyList<string> Only { get; init; } = Array.Empty<string>();

    public string Script { get; init; } = string.Empty;

    public string ScriptUser { get; init; } = "ogb";

    public string InstallScriptUser { get; init; } = "root";
}
