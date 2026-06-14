using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenGitBase.Dispatcher.Options;
using OpenGitBase.Dispatcher.Services;

var arguments = Environment.GetCommandLineArgs();

var sshOriginalCommand = arguments.Length > 1 ? arguments[1] : null;
var sshKeyFingerprint = arguments.Length > 2 ? arguments[2] : null;
var sshPublicKey = arguments.Length > 3 ? arguments[3] : null;
var sshUser = arguments.Length > 4 ? arguments[4] : "git";

if (sshOriginalCommand == null)
{
    Console.Error.WriteLine("Missing SSH_ORIGINAL_COMMAND");
    Environment.Exit(1);
}

if (string.IsNullOrWhiteSpace(sshKeyFingerprint))
{
    Console.Error.WriteLine("Missing SSH key fingerprint");
    Environment.Exit(1);
}

if (string.IsNullOrWhiteSpace(sshPublicKey))
{
    Console.Error.WriteLine("Missing SSH public key");
    Environment.Exit(1);
}

var parser = new GitCommandParser();
if (
    !parser.TryParse(sshOriginalCommand, out var operation, out var repositoryPath)
)
{
    Console.Error.WriteLine($"Unsupported SSH command: {sshOriginalCommand}");
    Environment.Exit(1);
}

var builder = Host.CreateApplicationBuilder(arguments);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.Configure<DispatcherOptions>(options =>
{
    options.ApiUrl = builder.Configuration["Dispatcher:ApiUrl"] ?? "http://api:8080";
    options.DispatcherId = builder.Configuration["Dispatcher:DispatcherId"] ?? "dispatcher-1";
});

builder.Services.AddHttpClient<RepositoryAccessCheckClient>();

using var host = builder.Build();
var accessCheckClient = host.Services.GetRequiredService<RepositoryAccessCheckClient>();

Console.Error.WriteLine($"User: {sshUser}");
Console.Error.WriteLine($"Key Fingerprint: {sshKeyFingerprint}");
Console.Error.WriteLine($"Command: {sshOriginalCommand}");
Console.Error.WriteLine($"RepositoryPath: {repositoryPath}");
Console.Error.WriteLine($"Operation: {operation}");

try
{
    var accessCheck = await accessCheckClient.CheckAsync(
        sshPublicKey,
        repositoryPath,
        operation,
        CancellationToken.None
    );

    if (!accessCheck.Allowed)
    {
        Console.Error.WriteLine(
            accessCheck.Reason ?? "Repository access denied for user."
        );
        Environment.Exit(1);
    }

    Console.Error.WriteLine("Repository access allowed.");
    Environment.Exit(0);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Authorization check failed: {ex.Message}");
    Environment.Exit(1);
}
