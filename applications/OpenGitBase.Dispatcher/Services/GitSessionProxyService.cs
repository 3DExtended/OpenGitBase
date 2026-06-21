using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Options;
using Renci.SshNet;

namespace OpenGitBase.Dispatcher.Services;

public sealed class GitSessionProxyService
{
    private readonly DispatcherOptions _options;

    public GitSessionProxyService(DispatcherOptions options)
    {
        _options = options;
    }

    public static string BuildGitCommand(RepositoryOperation operation, string physicalPath)
    {
        var command = operation switch
        {
            RepositoryOperation.ReadGit => "git-upload-pack",
            RepositoryOperation.WriteGit => "git-receive-pack",
            _ => throw new InvalidOperationException($"Unsupported git operation: {operation}"),
        };

        return $"{command} '{physicalPath}'";
    }

    public async Task<int> ProxyAsync(
        RepositoryAccessCheckResponse accessCheck,
        RepositoryOperation operation,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(accessCheck.PhysicalPath))
        {
            throw new InvalidOperationException("Access check is missing storage routing fields.");
        }

        var target = operation == RepositoryOperation.WriteGit
            ? GitRoutingTargetSelector.SelectWriteTarget(accessCheck)
            : GitRoutingTargetSelector.SelectReadTarget(accessCheck);

        if (!File.Exists(_options.StorageSshPrivateKeyPath))
        {
            throw new FileNotFoundException(
                "Storage SSH private key was not found.",
                _options.StorageSshPrivateKeyPath
            );
        }

        var gitCommand = BuildGitCommand(operation, accessCheck.PhysicalPath);
        var keyFile = new PrivateKeyFile(_options.StorageSshPrivateKeyPath);
        var connectionInfo = new ConnectionInfo(
            target.InternalHost,
            target.InternalSshPort,
            _options.StorageSshUser,
            new PrivateKeyAuthenticationMethod(_options.StorageSshUser, keyFile)
        )
        {
            Timeout = TimeSpan.FromSeconds(_options.StorageSshConnectTimeoutSeconds),
        };

        using var client = new SshClient(connectionInfo);
        await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);

        using var command = client.CreateCommand(gitCommand);

        await using var localInput = Console.OpenStandardInput();
        await using var localOutput = Console.OpenStandardOutput();
        await using var localError = Console.OpenStandardError();

        var executeTask = command.ExecuteAsync(cancellationToken);

        var stdinTask = Task.Run(
            async () =>
            {
                await using var remoteInput = command.CreateInputStream();
                await localInput
                    .CopyToAsync(remoteInput, cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken
        );

        var stdoutTask = command.OutputStream.CopyToAsync(localOutput, cancellationToken);
        var stderrTask = command.ExtendedOutputStream.CopyToAsync(localError, cancellationToken);

        await Task.WhenAll(executeTask, stdinTask, stdoutTask, stderrTask).ConfigureAwait(false);
        return command.ExitStatus ?? 1;
    }
}
