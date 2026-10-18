namespace OpenGitBase.Api.Models;

/// <summary>A new set of recovery codes, formatted <c>xxxxx-xxxxx</c>; they are never shown again.</summary>
public class RecoveryCodesResponse
{
    public IReadOnlyList<string> RecoveryCodes { get; set; } = [];
}
