namespace OpenGitBase.Common.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "api";

    public string Audience { get; set; } = "api";

    public string Key { get; set; } = string.Empty;

    public long NumberOfSecondsToExpire { get; set; } = 2_678_400;
}
