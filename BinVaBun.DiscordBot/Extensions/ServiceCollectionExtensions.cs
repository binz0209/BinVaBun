using BinVaBun.DiscordBot.BackgroundServices;
using BinVaBun.DiscordBot.Configuration;
using BinVaBun.DiscordBot.Hosting;
using BinVaBun.DiscordBot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCompanion(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<DiscordOptions>().Bind(config.GetSection(DiscordOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ApiOptions>().Bind(config.GetSection(ApiOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<CompanionOptions>().Bind(config.GetSection(CompanionOptions.Section));
        services.Configure<List<OwnerOptions>>(config.GetSection("Owners"));

        // Discord. Only the non-privileged Guilds intent is needed for slash commands.
        services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds,
            LogLevel = LogSeverity.Info,
            AlwaysDownloadUsers = false
        }));
        services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>(), new InteractionServiceConfig
        {
            DefaultRunMode = RunMode.Async,
            LogLevel = LogSeverity.Info
        }));

        // MemoriesApp.Api access (HttpClientFactory; credentials added once here).
        services.AddHttpClient<ApiTransport>((sp, http) =>
            {
                var api = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
                http.BaseAddress = new Uri(api.BaseUrl.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(Math.Clamp(api.TimeoutSeconds, 5, 180));
                http.DefaultRequestHeaders.Add("X-Bot-Key", api.BotKey);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        services.AddHttpClient(ImageAttachmentService.HttpClientName, http => http.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton(sp => new OwnerDirectory(sp.GetRequiredService<IOptions<List<OwnerOptions>>>()));
        services.AddSingleton<IClock, SystemClock>();
        services.AddTransient<MemoriesApiClient>();
        services.AddTransient<BotStore>();
        services.AddTransient<ImageAttachmentService>();
        services.AddSingleton<SentLedger>();
        services.AddSingleton<ScheduleSettingsService>();
        services.AddSingleton<PagerStateStore>();
        services.AddSingleton<DiscordNotifier>();
        services.AddSingleton<BotReadiness>();
        services.AddSingleton<LoveNoteDelivery>();

        // Scheduled jobs, run by one scheduler loop.
        services.AddSingleton<IScheduledJob, DailyMemoryJob>();
        services.AddSingleton<IScheduledJob, DailyQuestionJob>();
        services.AddSingleton<IScheduledJob, AnniversaryJob>();
        services.AddSingleton<IScheduledJob, ReminderJob>();
        services.AddSingleton<IScheduledJob, LoveNoteJob>();
        services.AddHostedService<SchedulerService>();
        services.AddHostedService<DiscordBotService>();
        return services;
    }
}

