using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGitBase.ComputeAgent;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHttpClient();
builder.Services.Configure<ComputeAgentOptions>(builder.Configuration.GetSection("ComputeAgent"));
builder.Services.AddHostedService<ComputeAgentWorker>();

await builder.Build().RunAsync();
