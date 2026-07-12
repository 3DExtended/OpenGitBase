using System.Diagnostics;
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
        if (run is null || string.IsNullOrWhiteSpace(run.AfterSha))
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
            var exitCode = await RunShellAsync(
                $"git --git-dir=\"{repositoryPath}\" archive --format=tar.gz --output=\"{archivePath}\" {afterSha}",
                tempDir,
                cancellationToken
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
            var cloneExit = await RunShellAsync(
                $"git clone --depth {gitDepth} \"{repositoryPath}\" \"{cloneDir}\"",
                tempDir,
                cancellationToken
            ).ConfigureAwait(false);
            if (cloneExit != 0)
            {
                return [];
            }

            var checkoutExit = await RunShellAsync(
                $"git checkout \"{afterSha}\"",
                cloneDir,
                cancellationToken
            ).ConfigureAwait(false);
            if (checkoutExit != 0)
            {
                return [];
            }

            var archivePath = Path.Combine(tempDir, "workspace.tar.gz");
            var tarExit = await RunShellAsync(
                $"tar -czf \"{archivePath}\" -C \"{cloneDir}\" .",
                tempDir,
                cancellationToken
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

    private static async Task<int> RunShellAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        var info = new ProcessStartInfo("sh")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = workingDirectory,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(command);
        using var process = Process.Start(info);
        if (process is null)
        {
            return -1;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}
