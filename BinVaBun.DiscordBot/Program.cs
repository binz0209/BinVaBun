using BinVaBun.DiscordBot.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Local development convenience: load .env (git-ignored). Real environment variables always win.
DotEnv.Load();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
if (string.Equals(Environment.GetEnvironmentVariable("LOG_FORMAT"), "json", StringComparison.OrdinalIgnoreCase))
{
    builder.Logging.AddJsonConsole(o => o.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ ");
}
else
{
    builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
}
// The default HttpClient logging records full request URLs; keep it quiet.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Services.AddCompanion(builder.Configuration);

await builder.Build().RunAsync();
