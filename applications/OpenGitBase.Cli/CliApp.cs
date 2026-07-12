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

    private static CliServices CreateServices(
        ParseResult parseResult,
        TextWriter output,
        TextWriter error,
        CliServiceOverrides? overrides) =>
        CliServices.CreateDefault(parseResult, output, error, overrides);
}
