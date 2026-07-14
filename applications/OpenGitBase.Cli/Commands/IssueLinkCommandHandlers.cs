using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

public static class IssueLinkCommandHandlers
{
    public static async Task<int> LinkAsync(
        CliServices services,
        int number,
        DiscussionRelationshipType relationshipType,
        int targetNumber)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var link = await services.ApiClient
                .CreateDiscussionLinkAsync(
                    repo,
                    number,
                    new CreateDiscussionLinkRequest
                    {
                        TargetDiscussionNumber = targetNumber,
                        RelationshipType = relationshipType.ToString().ToLowerInvariant(),
                    })
                .ConfigureAwait(false);

            services.OutputWriter.WriteIssueLinkCreated(number, link);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> LinksAsync(CliServices services, int number)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            var links = await services.ApiClient
                .ListDiscussionLinksAsync(repo, number)
                .ConfigureAwait(false);

            services.OutputWriter.WriteIssueLinks(number, links);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }

    public static async Task<int> UnlinkAsync(
        CliServices services,
        int number,
        DiscussionRelationshipType relationshipType,
        int targetNumber)
    {
        try
        {
            var repo = RepoContextResolver.ResolveRepo(services);
            await services.ApiClient
                .DeleteDiscussionLinkAsync(repo, number, targetNumber, relationshipType)
                .ConfigureAwait(false);

            services.OutputWriter.WriteIssueLinkRemoved(number, targetNumber, relationshipType);
            return CliExitCodes.Success;
        }
        catch (Exception ex)
        {
            return CliErrorHandler.HandleException(ex, services.OutputWriter, services.JsonOutput);
        }
    }
}
