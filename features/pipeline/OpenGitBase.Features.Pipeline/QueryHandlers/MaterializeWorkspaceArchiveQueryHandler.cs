using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class MaterializeWorkspaceArchiveQueryHandler
    : IQueryHandler<MaterializeWorkspaceArchiveQuery, WorkspaceArchiveResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasherService;

    public MaterializeWorkspaceArchiveQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasherService
    )
    {
        _contextFactory = contextFactory;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<Option<WorkspaceArchiveResult>> RunQueryAsync(
        MaterializeWorkspaceArchiveQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.JobId.Value == Guid.Empty
            || string.IsNullOrWhiteSpace(query.JobIdentityToken)
            || !JobIdentityTokens.TryParseJobId(query.JobIdentityToken, out var tokenJobId)
            || tokenJobId != query.JobId.Value
        )
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var identity = await context
            .Set<JobIdentityEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.JobId == query.JobId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (
            identity is null
            || identity.RevokedAt is not null
            || identity.ExpiresAt <= DateTimeOffset.UtcNow
            || !_passwordHasherService.VerifyPassword(identity.TokenHash, query.JobIdentityToken)
        )
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        var job = await context
            .Set<PipelineJobEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == query.JobId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        var run = await context
            .Set<PipelineRunEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == job.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null || !IsObjectId(run.AfterSha))
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        var repository = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == run.RepositoryId, cancellationToken)
            .ConfigureAwait(false);
        if (repository is null || string.IsNullOrWhiteSpace(repository.PhysicalPath))
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        var archiveBytes = job.GitDepth > 0
            ? await BuildShallowArchiveAsync(
                repository.PhysicalPath,
                run.AfterSha,
                job.GitDepth,
                cancellationToken
            ).ConfigureAwait(false)
            : await BuildFullArchiveAsync(repository.PhysicalPath, run.AfterSha, cancellationToken)
                .ConfigureAwait(false);
        if (archiveBytes.Length == 0)
        {
            return Option<WorkspaceArchiveResult>.None;
        }

        return Option.From(
            new WorkspaceArchiveResult
            {
                ArchiveBytes = archiveBytes,
                FileName = "workspace.tar.gz",
                IsShallow = job.GitDepth > 0,
            }
        );
    }

    private static async Task<byte[]> BuildFullArchiveAsync(
        string repositoryPath,
        string afterSha,
        CancellationToken cancellationToken
    )
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ogb-ws-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var archivePath = Path.Combine(tempDir, "workspace.tar.gz");
            var exitCode = await RunProcessAsync(
                "git",
                tempDir,
                cancellationToken,
                $"--git-dir={repositoryPath}",
                "archive",
                "--format=tar.gz",
                $"--output={archivePath}",
                afterSha
            ).ConfigureAwait(false);
            if (exitCode != 0 || !File.Exists(archivePath))
            {
                return [];
            }

            return await File.ReadAllBytesAsync(archivePath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static async Task<byte[]> BuildShallowArchiveAsync(
        string repositoryPath,
        string afterSha,
        int gitDepth,
        CancellationToken cancellationToken
    )
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ogb-ws-{Guid.NewGuid():N}");
        var cloneDir = Path.Combine(tempDir, "repo");
        Directory.CreateDirectory(tempDir);
        try
        {
            var cloneExit = await RunProcessAsync(
                "git",
                tempDir,
                cancellationToken,
                "clone",
                "--depth",
                gitDepth.ToString(CultureInfo.InvariantCulture),
                "--",
                repositoryPath,
                cloneDir
            ).ConfigureAwait(false);
            if (cloneExit != 0)
            {
                return [];
            }

            var checkoutExit = await RunProcessAsync(
                "git",
                cloneDir,
                cancellationToken,
                "checkout",
                "--detach",
                afterSha
            ).ConfigureAwait(false);
            if (checkoutExit != 0)
            {
                return [];
            }

            var archivePath = Path.Combine(tempDir, "workspace.tar.gz");
            var tarExit = await RunProcessAsync(
                "tar",
                tempDir,
                cancellationToken,
                "-czf",
                archivePath,
                "-C",
                cloneDir,
                "."
            ).ConfigureAwait(false);
            if (tarExit != 0 || !File.Exists(archivePath))
            {
                return [];
            }

            return await File.ReadAllBytesAsync(archivePath, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    // Only full commit ids reach git; refs, revision expressions and options are refused.
    private static bool IsObjectId(string? value) =>
        value is { Length: 40 or 64 } && value.All(char.IsAsciiHexDigitLower);

    private static async Task<int> RunProcessAsync(
        string fileName,
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments
    )
    {
        var info = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info);
        if (process is null)
        {
            return -1;
        }

        var drainOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var drainError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(drainOutput, drainError).ConfigureAwait(false);
        return process.ExitCode;
    }
}
