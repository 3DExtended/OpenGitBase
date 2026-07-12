using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGitBase.Common.Options;
using OpenGitBase.ComputeAgent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient();
builder.Services.Configure<ComputeAgentOptions>(builder.Configuration.GetSection("ComputeAgent"));
builder.Services.Configure<LayerStoreFetchOptions>(builder.Configuration.GetSection("LayerStore"));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<IBaseImageArtifactResolver, BaseImageArtifactResolver>();
builder.Services.AddHostedService<ComputeAgentWorker>();

await builder.Build().RunAsync();
