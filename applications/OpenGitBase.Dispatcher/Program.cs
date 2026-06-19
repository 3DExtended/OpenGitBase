using OpenGitBase.Dispatcher;

var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();

if (arguments.Length > 0 && arguments[0] == "--serve-http")
{
    await HttpServerHost.RunAsync(arguments.Skip(1).ToArray());
    return;
}

Environment.Exit(await SshSessionRunner.RunAsync(arguments));
