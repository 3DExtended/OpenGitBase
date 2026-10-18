using System.Diagnostics.CodeAnalysis;

namespace OpenGitBase.Api;

[ExcludeFromCodeCoverage]
public static partial class Program
{
    public static async Task Main(string[] args)
    {
        if (EncryptionKeyRotationHost.IsEnabled())
        {
            Environment.Exit(await EncryptionKeyRotationHost.RunAsync(args));
            return;
        }

        if (MigrateOnlyHost.IsEnabled())
        {
            Environment.Exit(await MigrateOnlyHost.RunAsync(args));
            return;
        }

        try
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }
        catch (Exception ex)
        {
            // An exception that escapes Main aborts the runtime with SIGABRT. In the container
            // dotnet runs as PID 1, and the kernel drops that signal for PID 1, so a failed startup
            // such as a failed migration left a running, unhealthy API that the restart policy never
            // recovered. Exit with a code instead.
            await Console.Error.WriteLineAsync($"Unhandled exception. {ex}");
            Environment.Exit(1);
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
}
