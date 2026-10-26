namespace OpenGitBase.Features.Repository.Contracts;

public sealed class LfsObjectDto
{
    public string Oid { get; init; } = string.Empty;

    public long Size { get; init; }
}
