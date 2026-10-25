namespace OpenGitBase.Api.Services;

public enum LfsQuorumStatus
{
    /// <summary>The primary and at least one encrypted replica hold the object.</summary>
    Reached = 0,

    /// <summary>No encrypted replica confirmed its copy in time.</summary>
    NotReached = 1,

    /// <summary>The repository has no Repository Key to encrypt with.</summary>
    KeyUnavailable = 2,
}
