namespace OpenGitBase.Features.Pipeline;

public static class GitObjectIds
{
    // A full lowercase SHA-1 or SHA-256 object id, as git reports pushed commits. Anything else
    // (refs, abbreviations, revision expressions, options) never reaches a git command line.
    public static bool IsValid(string? value) =>
        value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigitLower);

    // Git reports a deleted ref as a push whose new value is the all-zero object id.
    public static bool IsNull(string? value) =>
        value is { Length: 40 or 64 } && value.All(character => character == '0');
}
