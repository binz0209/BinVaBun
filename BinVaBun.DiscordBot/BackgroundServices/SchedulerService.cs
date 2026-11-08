using BinVaBun.DiscordBot.Configuration;
using BinVaBun.DiscordBot.Content;
using BinVaBun.DiscordBot.Models;
using BinVaBun.DiscordBot.Services;
using Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BinVaBun.DiscordBot.BackgroundServices;

public sealed record JobContext(DateTimeOffset LocalNow, DateOnly Today, ScheduleSettingsService.Effective Settings, TimeSpan CatchUp);

public interface IScheduledJob
{
    string Name { get; }
    Task RunAsync(JobContext context, CancellationToken ct);
}

/// <summary>
/// One loop drives every scheduled job. Jobs are idempotent (claims in the persisted ledger),
/// so a restart, a slow tick or a failed send can never produce duplicates; failed work retries next tick.
/// </summary>
public sealed class SchedulerService(
    IEnumerable<IScheduledJob> jobs,
    ScheduleSettingsService settings,
    IClock clock,
    BotReadiness readiness,
    IOptions<CompanionOptions> options,
    ILogger<SchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await readiness.Ready.WaitAsync(stoppingToken);
        var tick = TimeSpan.FromSeconds(Math.Clamp(options.Value.TickSeconds, 5, 600));
        var catchUp = TimeSpan.FromHours(Math.Clamp(options.Value.CatchUpWindowHours, 0, 23));
        logger.LogInformation("Scheduler started with {Count} jobs (tick {Tick}s, time zone {Zone})",
            jobs.Count(), tick.TotalSeconds, clock.Zone.Id);

        using var timer = new PeriodicTimer(tick);
        do
        {
            ScheduleSettingsService.Effective effective;
            try
            {
                effective = await settings.GetEffectiveAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Scheduler could not load settings; skipping this tick");
                continue;
            }

            var context = new JobContext(clock.Now(), clock.Today(), effective, catchUp);
            foreach (var job in jobs)
            {
                try
                {
                    await job.RunAsync(context, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Job {Job} failed; will retry on the next tick", job.Name);
                }
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}

