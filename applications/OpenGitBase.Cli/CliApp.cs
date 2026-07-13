using System.CommandLine;
using System.Reflection;
using OpenGitBase.Cli.Commands;

namespace OpenGitBase.Cli;

public static class CliApp
{
    public static RootCommand BuildRootCommand(
        TextWriter output,
        TextWriter error,
        CliServiceOverrides? overrides = null)
    {
        var rootCommand = new RootCommand("OpenGitBase command-line tool")
        {
            new VersionOption(),
            CliOptions.HostnameOption,
            CliOptions.JsonOption,
        };

        rootCommand.Subcommands.Add(BuildAuthCommand(output, error, overrides));
        rootCommand.Subcommands.Add(BuildIssueCommand(output, error, overrides));
        rootCommand.Subcommands.Add(BuildMergeRequestCommand(output, error, overrides));
        return rootCommand;
    }

    public static Task<int> RunAsync(
        string[] args,
        TextWriter? output = null,
        TextWriter? error = null,
        CliServiceOverrides? overrides = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;
        var parseResult = BuildRootCommand(stdout, stderr, overrides).Parse(args);
        var configuration = new InvocationConfiguration
        {
            Output = stdout,
            Error = stderr,
        };

        return parseResult.InvokeAsync(configuration);
    }

    public static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    private static Command BuildAuthCommand(TextWriter output, TextWriter error, CliServiceOverrides? overrides)
    {
        var authCommand = new Command("auth", "Manage authentication");

        var loginCommand = new Command("login", "Log in via browser");
        loginCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await AuthCommandHandlers.LoginAsync(services).ConfigureAwait(false);
        });

        var statusCommand = new Command("status", "Show authentication status");
        statusCommand.SetAction((parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return AuthCommandHandlers.StatusAsync(services);
        });

        var logoutCommand = new Command("logout", "Log out and clear stored credentials");
        logoutCommand.SetAction((parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return AuthCommandHandlers.LogoutAsync(services);
        });

        authCommand.Subcommands.Add(loginCommand);
        authCommand.Subcommands.Add(statusCommand);
        authCommand.Subcommands.Add(logoutCommand);
        return authCommand;
    }

    private static Command BuildIssueCommand(TextWriter output, TextWriter error, CliServiceOverrides? overrides)
    {
        var issueCommand = new Command("issue", "Manage repository issues (discussions)")
        {
            CliOptions.RepoOption,
        };

        var createCommand = new Command("create", "Create an issue")
        {
            CliOptions.TitleOption,
            CliOptions.BodyOption,
            CliOptions.BodyFileOption,
        };
        createCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await IssueCommandHandlers.CreateAsync(
                services,
                parse.GetValue(CliOptions.TitleOption)!,
                parse.GetValue(CliOptions.BodyOption),
                parse.GetValue(CliOptions.BodyFileOption)).ConfigureAwait(false);
        });

        var commentCommand = new Command("comment", "Comment on an issue")
        {
            CliOptions.IssueNumberArgument,
            CliOptions.BodyOption,
            CliOptions.BodyFileOption,
        };
        commentCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await IssueCommandHandlers.CommentAsync(
                services,
                parse.GetValue(CliOptions.IssueNumberArgument),
                parse.GetValue(CliOptions.BodyOption),
                parse.GetValue(CliOptions.BodyFileOption)).ConfigureAwait(false);
        });

        var closeCommand = new Command("close", "Close an issue")
        {
            CliOptions.IssueNumberArgument,
            CliOptions.ReasonOption,
        };
        closeCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await IssueCommandHandlers.CloseAsync(
                services,
                parse.GetValue(CliOptions.IssueNumberArgument),
                parse.GetValue(CliOptions.ReasonOption)).ConfigureAwait(false);
        });

        var listCommand = new Command("list", "List issues")
        {
            CliOptions.StatusOption,
        };
        listCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await IssueCommandHandlers.ListAsync(
                services,
                parse.GetValue(CliOptions.StatusOption)).ConfigureAwait(false);
        });

        var viewCommand = new Command("view", "View an issue")
        {
            CliOptions.IssueNumberArgument,
        };
        viewCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await IssueCommandHandlers.ViewAsync(
                services,
                parse.GetValue(CliOptions.IssueNumberArgument)).ConfigureAwait(false);
        });

        var statusCommand = new Command("status", "Show issue status")
        {
            CliOptions.IssueNumberArgument,
        };
        statusCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await IssueCommandHandlers.StatusAsync(
                services,
                parse.GetValue(CliOptions.IssueNumberArgument)).ConfigureAwait(false);
        });

        issueCommand.Subcommands.Add(createCommand);
        issueCommand.Subcommands.Add(commentCommand);
        issueCommand.Subcommands.Add(closeCommand);
        issueCommand.Subcommands.Add(listCommand);
        issueCommand.Subcommands.Add(viewCommand);
        issueCommand.Subcommands.Add(statusCommand);
        return issueCommand;
    }

    private static Command BuildMergeRequestCommand(
        TextWriter output,
        TextWriter error,
        CliServiceOverrides? overrides)
    {
        var mergeRequestCommand = new Command("mr", "Manage merge requests")
        {
            CliOptions.RepoOption,
        };

        var listCommand = new Command("list", "List merge requests")
        {
            CliOptions.MrStatusOption,
        };
        listCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.ListAsync(
                services,
                parse.GetValue(CliOptions.MrStatusOption)).ConfigureAwait(false);
        });

        var viewCommand = new Command("view", "View a merge request")
        {
            CliOptions.MergeRequestNumberArgument,
            CliOptions.CommitsOption,
        };
        viewCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.ViewAsync(
                services,
                parse.GetValue(CliOptions.MergeRequestNumberArgument),
                parse.GetValue(CliOptions.CommitsOption)).ConfigureAwait(false);
        });

        var statusCommand = new Command("status", "Show merge request status and mergeability")
        {
            CliOptions.MergeRequestNumberArgument,
        };
        statusCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.StatusAsync(
                services,
                parse.GetValue(CliOptions.MergeRequestNumberArgument)).ConfigureAwait(false);
        });

        var diffCommand = new Command("diff", "Show merge request diff")
        {
            CliOptions.MergeRequestNumberArgument,
        };
        diffCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.DiffAsync(
                services,
                parse.GetValue(CliOptions.MergeRequestNumberArgument)).ConfigureAwait(false);
        });

        var createCommand = new Command("create", "Create a merge request")
        {
            CliOptions.TitleOption,
            CliOptions.BodyOption,
            CliOptions.BodyFileOption,
            CliOptions.HeadOption,
            CliOptions.BaseOption,
            CliOptions.DraftOption,
        };
        createCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.CreateAsync(
                services,
                parse.GetValue(CliOptions.TitleOption)!,
                parse.GetValue(CliOptions.BodyOption),
                parse.GetValue(CliOptions.BodyFileOption),
                parse.GetValue(CliOptions.HeadOption),
                parse.GetValue(CliOptions.BaseOption),
                parse.GetValue(CliOptions.DraftOption)).ConfigureAwait(false);
        });

        var closeCommand = new Command("close", "Close a merge request")
        {
            CliOptions.MergeRequestNumberArgument,
        };
        closeCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.CloseAsync(
                services,
                parse.GetValue(CliOptions.MergeRequestNumberArgument)).ConfigureAwait(false);
        });

        var readyCommand = new Command("ready", "Publish a draft merge request")
        {
            CliOptions.MergeRequestNumberArgument,
        };
        readyCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.ReadyAsync(
                services,
                parse.GetValue(CliOptions.MergeRequestNumberArgument)).ConfigureAwait(false);
        });

        var editCommand = new Command("edit", "Edit merge request metadata")
        {
            CliOptions.MergeRequestNumberArgument,
            CliOptions.MrTitleOption,
            CliOptions.BodyOption,
            CliOptions.BodyFileOption,
        };
        editCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.EditAsync(
                services,
                parse.GetValue(CliOptions.MergeRequestNumberArgument),
                parse.GetValue(CliOptions.MrTitleOption),
                parse.GetValue(CliOptions.BodyOption),
                parse.GetValue(CliOptions.BodyFileOption)).ConfigureAwait(false);
        });

        var approveCommand = new Command("approve", "Approve a merge request")
        {
            CliOptions.MergeRequestNumberArgument,
        };
        approveCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.ApproveAsync(
                services,
                parse.GetValue(CliOptions.MergeRequestNumberArgument)).ConfigureAwait(false);
        });

        var mergeCommand = new Command("merge", "Merge an approved merge request")
        {
            CliOptions.MergeRequestNumberArgument,
            CliOptions.StrategyOption,
            CliOptions.DeleteBranchOption,
        };
        mergeCommand.SetAction(async (parse, _) =>
        {
            var services = CreateServices(parse, output, error, overrides);
            return await MergeRequestCommandHandlers.MergeAsync(
                services,
                parse.GetValue(CliOptions.MergeRequestNumberArgument),
                parse.GetValue(CliOptions.StrategyOption),
                parse.GetValue(CliOptions.DeleteBranchOption)).ConfigureAwait(false);
        });

        mergeRequestCommand.Subcommands.Add(listCommand);
        mergeRequestCommand.Subcommands.Add(viewCommand);
        mergeRequestCommand.Subcommands.Add(statusCommand);
        mergeRequestCommand.Subcommands.Add(diffCommand);
        mergeRequestCommand.Subcommands.Add(createCommand);
        mergeRequestCommand.Subcommands.Add(closeCommand);
        mergeRequestCommand.Subcommands.Add(readyCommand);
        mergeRequestCommand.Subcommands.Add(editCommand);
        mergeRequestCommand.Subcommands.Add(approveCommand);
        mergeRequestCommand.Subcommands.Add(mergeCommand);
        return mergeRequestCommand;
    }

    private static CliServices CreateServices(
        ParseResult parseResult,
        TextWriter output,
        TextWriter error,
        CliServiceOverrides? overrides) =>
        CliServices.CreateDefault(parseResult, output, error, overrides);
}
