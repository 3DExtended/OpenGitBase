using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenGitBase.Dispatcher.Options;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher;

internal static class HttpServerHost
{
    public static Task RunAsync(string[] arguments)
    {
        var builder = WebApplication.CreateBuilder(arguments);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        SshSessionRunner.ConfigureDispatcherServices(builder.Services, builder.Configuration);

        var app = builder.Build();
        var handler = app.Services.GetRequiredService<GitSmartHttpHandler>();
        var options = app.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<DispatcherOptions>>()
            .Value;

        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

        app.Map(
            "{*path}",
            async context =>
            {
                await handler.HandleAsync(context).ConfigureAwait(false);
            }
        );

        app.Urls.Add($"http://0.0.0.0:{options.HttpPort}");
        return app.RunAsync();
    }
}
