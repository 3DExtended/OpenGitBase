namespace OpenGitBase.Cli.Tests;

internal static class AuthCommandTestsHelpers
{
    public static string CreateJwt(string username)
    {
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var payload = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{{\"sub\":\"{username}\",\"name\":\"{username}\"}}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"{header}.{payload}.";
    }
}
