using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Options;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher;

internal static class SshSessionRunner
{
    public static async Task<int> RunAsync(string[] arguments)
    {
        var sshOriginalCommand = arguments.Length > 0 ? arguments[0] : null;
        var sshKeyFingerprint = arguments.Length > 1 ? arguments[1] : null;
        var sshPublicKey = arguments.Length > 2 ? arguments[2] : null;
        var sshUser = arguments.Length > 3 ? arguments[3] : "git";

        if (sshOriginalCommand == null)
        {
            await Console.Error.WriteLineAsync("Missing SSH_ORIGINAL_COMMAND");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(sshKeyFingerprint))
        {
            await Console.Error.WriteLineAsync("Missing SSH key fingerprint");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(sshPublicKey))
        {
            await Console.Error.WriteLineAsync("Missing SSH public key");
            return 1;
        }

        var parser = new GitCommandParser();
        if (!parser.TryParse(sshOriginalCommand, out var operation, out var repositoryPath))
        {
            await Console.Error.WriteLineAsync($"Unsupported SSH command: {sshOriginalCommand}");
            return 1;
        }

        var builder = Host.CreateApplicationBuilder(arguments);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        ConfigureDispatcherServices(builder);

        using var host = builder.Build();
        var accessCheckClient = host.Services.GetRequiredService<RepositoryAccessCheckClient>();
        var gitSessionProxyService = host.Services.GetRequiredService<GitSessionProxyService>();

        await Console.Error.WriteLineAsync($"User: {sshUser}");
        await Console.Error.WriteLineAsync($"Key Fingerprint: {sshKeyFingerprint}");
        await Console.Error.WriteLineAsync($"Command: {sshOriginalCommand}");
        await Console.Error.WriteLineAsync($"RepositoryPath: {repositoryPath}");
        await Console.Error.WriteLineAsync($"Operation: {operation}");

        try
        {
            RepositoryAccessCheckResponse accessCheck;
            if (operation == RepositoryOperation.WriteGit)
            {
                await using var stdin = Console.OpenStandardInput();
                var (prefix, refUpdates) = await GitReceivePackParser
                    .ReadPrefixAsync(stdin, CancellationToken.None)
                    .ConfigureAwait(false);

                accessCheck = await accessCheckClient
                    .CheckWithPublicKeyAsync(
                        sshPublicKey,
                        repositoryPath,
                        operation,
                        refUpdates,
                        packSizeBytes: 0,
                        maxFileBytes: 0,
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);

                if (!accessCheck.Allowed)
                {
                    await Console.Error.WriteLineAsync(
                        accessCheck.Reason ?? "Repository access denied for user."
                    );
                    return 1;
                }

                await Console.Error.WriteLineAsync(
                    "Repository access allowed. Proxying git session to storage."
                );
                return await gitSessionProxyService
                    .ProxyAsync(accessCheck, operation, prefix, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            accessCheck = await accessCheckClient.CheckWithPublicKeyAsync(
                sshPublicKey,
                repositoryPath,
                operation,
                CancellationToken.None
            );

            if (!accessCheck.Allowed)
            {
                await Console.Error.WriteLineAsync(
                    accessCheck.Reason ?? "Repository access denied for user."
                );
                return 1;
            }

            await Console.Error.WriteLineAsync(
                "Repository access allowed. Proxying git session to storage."
            );
            return await gitSessionProxyService
                .ProxyAsync(accessCheck, operation, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Authorization or proxy failed: {ex.Message}");
            return 1;
        }
    }

    internal static void ConfigureDispatcherServices(HostApplicationBuilder builder) =>
        ConfigureDispatcherServices(builder.Services, builder.Configuration);

    internal static void ConfigureDispatcherServices(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<DispatcherOptions>(options =>
        {
            options.ApiUrl = configuration["Dispatcher:ApiUrl"] ?? "http://api:8080";
            options.DispatcherId = configuration["Dispatcher:DispatcherId"] ?? "dispatcher-1";
            options.HttpPort = int.TryParse(configuration["Dispatcher:HttpPort"], out var httpPort)
                ? httpPort
                : 8082;
            options.StorageSshPrivateKeyPath =
                configuration["Dispatcher:StorageSshPrivateKeyPath"]
                ?? "/run/secrets/storage_ssh_key";
            options.StorageSshUser = configuration["Dispatcher:StorageSshUser"] ?? "git";
            options.StorageSshConnectTimeoutSeconds = int.TryParse(
                configuration["Dispatcher:StorageSshConnectTimeoutSeconds"],
                out var timeoutSeconds
            )
                ? timeoutSeconds
                : 30;
        });

        services.AddHttpClient<RepositoryAccessCheckClient>();
        services.AddHttpClient<FleetComponentRegistrationClient>();
        services.AddHostedService<DispatcherFleetComponentRegistrationService>();
        services.AddHttpClient<GitHttpProxyService>(client =>
            client.Timeout = TimeSpan.FromMinutes(5));
        services.AddSingleton<GitSmartHttpPathParser>();
        services.AddSingleton<GitSmartHttpHandler>();
        services.AddSingleton<GitSessionProxyService>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DispatcherOptions>>().Value;
            return new GitSessionProxyService(options);
        });
    }
}
