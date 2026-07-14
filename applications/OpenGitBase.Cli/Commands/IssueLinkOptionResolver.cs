using System.CommandLine;
using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

internal static class IssueLinkOptionResolver
{
    public static (DiscussionRelationshipType RelationshipType, int TargetNumber) ResolveTarget(
        ParseResult parse)
    {
        var matches = new List<(DiscussionRelationshipType Type, int Target)>();
        TryAdd(parse, CliOptions.LinkParentOption, DiscussionRelationshipType.Parent, matches);
        TryAdd(parse, CliOptions.LinkChildOption, DiscussionRelationshipType.Child, matches);
        TryAdd(parse, CliOptions.LinkRelatedOption, DiscussionRelationshipType.Related, matches);
        TryAdd(parse, CliOptions.LinkBlocksOption, DiscussionRelationshipType.Blocks, matches);

        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                "Specify exactly one of --parent, --child, --related, or --blocks with a target issue number.");
        }

        return (matches[0].Type, matches[0].Target);
    }

    private static void TryAdd(
        ParseResult parse,
        Option<int?> option,
        DiscussionRelationshipType type,
        ICollection<(DiscussionRelationshipType Type, int Target)> matches)
    {
        if (parse.GetValue(option) is int target)
        {
            matches.Add((type, target));
        }
    }
}
