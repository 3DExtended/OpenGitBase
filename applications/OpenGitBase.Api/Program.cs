using System.Diagnostics.CodeAnalysis;

namespace OpenGitBase.Api;

[ExcludeFromCodeCoverage]
public static partial class Program
{
    public static async Task Main(string[] args)
    {
        if (MigrateOnlyHost.IsEnabled())
        {
            Environment.Exit(await MigrateOnlyHost.RunAsync(args));
            return;
        }

        await CreateHostBuilder(args).Build().RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
}
