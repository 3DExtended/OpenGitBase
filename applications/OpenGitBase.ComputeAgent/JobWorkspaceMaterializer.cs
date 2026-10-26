using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace OpenGitBase.ComputeAgent;

/// <summary>
/// Downloads a job's workspace archive from the API and unpacks it where the script runs.
/// Any failure fails the job: a script run in an empty directory would pass or fail for the
/// wrong reason.
/// </summary>
public static class JobWorkspaceMaterializer
{
    public static async Task<JobWorkspaceResult> MaterializeAsync(
        HttpClient apiClient,
        Guid jobId,
        string jobIdentityToken,
        string projectDir,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(jobIdentityToken))
        {
            return Failed("The claim carried no job identity token to fetch the workspace with.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"pipeline/jobs/{jobId}/workspace-archive");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jobIdentityToken);
            using var response = await apiClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Failed($"Workspace download returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var archive = new GZipStream(body, CompressionMode.Decompress);
            await TarFile
                .ExtractToDirectoryAsync(archive, projectDir, overwriteFiles: false, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
            when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return Failed($"Workspace could not be prepared: {ex.Message}");
        }

        return new JobWorkspaceResult { Success = true };
    }

    private static JobWorkspaceResult Failed(string message) => new() { Success = false, ErrorMessage = message };
}
