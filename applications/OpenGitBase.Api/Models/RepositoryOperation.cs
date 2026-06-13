namespace OpenGitBase.Api.Models;

public enum RepositoryOperation
{
    ReadGit, // git-upload-pack
    WriteGit, // git-receive-pack
    Unknown,
}
