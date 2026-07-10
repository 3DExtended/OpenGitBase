namespace OpenGitBase.Features.Organization.Contracts;

/// <summary>
/// Placement override. <see cref="Inherit"/> uses org default and org node tier count.
/// </summary>
public enum PlacementPolicy
{
    Inherit = 0,
    PlatformDefault = 1,
    MaxSelfHost = 2,
}
