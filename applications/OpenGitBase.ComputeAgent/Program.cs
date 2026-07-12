using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGitBase.Common.Options;
using OpenGitBase.ComputeAgent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient();
builder.Services.Configure<ComputeAgentOptions>(builder.Configuration.GetSection("ComputeAgent"));
builder.Services.Configure<LayerStoreFetchOptions>(builder.Configuration.GetSection("LayerStore"));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<ProcessSandboxExecutor>();
builder.Services.AddSingleton<IFirecrackerLauncher, ProcessFirecrackerLauncher>();
builder.Services.AddSingleton<FirecrackerSandboxExecutor>();
builder.Services.AddSingleton<ISandboxExecutor>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComputeAgentOptions>>().Value;
    return new SandboxExecutorSelector(
        options,
        sp.GetRequiredService<ProcessSandboxExecutor>(),
        sp.GetRequiredService<FirecrackerSandboxExecutor>()
    );
});
builder.Services.AddSingleton<IBaseImageArtifactResolver>(sp =>
    new BaseImageArtifactResolver(
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LayerStoreFetchOptions>>().Value
    ));
builder.Services.AddHostedService<ComputeAgentWorker>();

await builder.Build().RunAsync();
