using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

public static class DocsCommandHandlers
{
    public static async Task<int> PullAsync(
        CliServices services,
        DirectoryInfo outputDir,
        IReadOnlyList<string>? prefixFilters)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var discussions = await services.ApiClient
                .ListDiscussionsAsync(repo, status: null)
                .ConfigureAwait(false);

            var candidates = DocsMirrorExporter.FilterByPrefix(discussions, prefixFilters);
            var exported = new List<DocsPullFileModel>();

            foreach (var summary in candidates)
            {
                var discussion = await services.ApiClient
                    .GetDiscussionAsync(repo, summary.Number, includeComments: false)
                    .ConfigureAwait(false);

                exported.Add(
                    DocsMirrorExporter.BuildExportFile(discussion, outputDir.FullName));
            }

            services.OutputWriter.WriteDocsPull(exported);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }
}
