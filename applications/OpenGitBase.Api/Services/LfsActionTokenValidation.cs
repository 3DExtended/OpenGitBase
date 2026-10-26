namespace OpenGitBase.Api.Services;

public sealed class LfsActionTokenValidation
{
    private LfsActionTokenValidation(LfsActionTokenClaims? claims, string? error)
    {
        Claims = claims;
        Error = error;
    }

    public LfsActionTokenClaims? Claims { get; }

    public string? Error { get; }

    public bool IsValid => Claims is not null;

    public static LfsActionTokenValidation Valid(LfsActionTokenClaims claims) => new(claims, null);

    public static LfsActionTokenValidation Invalid(string error) => new(null, error);
}
