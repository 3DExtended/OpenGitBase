using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Configuration;
using OpenGitBase.Cli.Output;

namespace OpenGitBase.Cli.Commands;

public static class AuthCommandHandlers
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(30);

    public static async Task<int> LoginAsync(CliServices services)
    {
        try
        {
            var session = await services.LoopbackAuthServer.StartAsync().ConfigureAwait(false);
            var authUrl =
                $"{services.Host.TrimEnd('/')}/cli/auth?port={session.Port}&state={Uri.EscapeDataString(session.State)}";

            if (!services.JsonOutput)
            {
                services.Output.WriteLine($"Opening browser to {authUrl}");
            }

            services.BrowserLauncher.OpenUrl(authUrl);
            var token = await services.LoopbackAuthServer
                .WaitForTokenAsync(LoginTimeout)
                .ConfigureAwait(false);

            services.CredentialStore.SaveToken(services.Host, token);

            var config = services.ConfigStore.Load();
            config.ActiveHost = services.Host;
            config.LoggedInUsername = JwtTokenReader.TryGetUsername(token);
            config.LastLoginAt = DateTimeOffset.UtcNow;
            services.ConfigStore.Save(config);

            if (!services.JsonOutput)
            {
                services.Output.WriteLine("Login successful.");
            }

            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
        finally
        {
            await services.LoopbackAuthServer.StopAsync().ConfigureAwait(false);
        }
    }

    public static Task<int> StatusAsync(CliServices services)
    {
        try
        {
            var hasToken = services.CredentialStore.HasToken(services.Host);
            var username = hasToken
                ? JwtTokenReader.TryGetUsername(services.CredentialStore.GetToken(services.Host)!)
                : null;

            services.OutputWriter.WriteAuthStatus(new AuthStatusOutput
            {
                LoggedIn = hasToken,
                Hostname = hasToken ? services.Host : null,
                Username = username,
            });

            return Task.FromResult(CliExitCodes.Success);
        }
        catch (Exception ex)
        {
            return Task.FromResult(CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput));
        }
    }

    public static Task<int> LogoutAsync(CliServices services)
    {
        try
        {
            services.CredentialStore.DeleteToken(services.Host);

            var config = services.ConfigStore.Load();
            if (string.Equals(config.ActiveHost, services.Host, StringComparison.OrdinalIgnoreCase))
            {
                config.ActiveHost = null;
                config.LoggedInUsername = null;
                config.LastLoginAt = null;
                services.ConfigStore.Save(config);
            }

            if (!services.JsonOutput)
            {
                services.Output.WriteLine("Logged out.");
            }

            return Task.FromResult(CliExitCodes.Success);
        }
        catch (Exception ex)
        {
            return Task.FromResult(CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput));
        }
    }
}
