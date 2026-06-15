namespace OpenGitBase.Common.Options;

public sealed class AdminSeedOptions
{
    public bool Enabled { get; set; } = true;

    public string Username { get; set; } = "admin";

    public string Password { get; set; } = "change-me-admin";

    public string Email { get; set; } = "admin@localhost";
}
