using Finance.Android17.Application;
using Finance.Android17.Presentation;

#if ANDROID
using Android.App;
using Android.Content;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace Finance.Android17.Maui;

/// <summary>
/// Programme, avec <c>AlarmManager</c>, le contrôle quotidien (<see cref="DailyCheckService"/>) : une alarme par jour, reprogrammée
/// à chaque passage (au déverrouillage, par le récepteur lui-même et au redémarrage du téléphone).
/// </summary>
public sealed class DailyCheckScheduler(ISettingsService settings, TimeProvider clock) : IDailyCheckScheduler
{
#if ANDROID
    private const int RequestCode = 9001;
#endif

    public async Task EnsureScheduledAsync(CancellationToken cancellationToken = default)
    {
#if ANDROID
        try
        {
            Schedule(Android.App.Application.Context, DailyCheckService.NextRun(clock, settings.LastDailyCheckDay));
            if (settings.BackgroundAlertsEnabled) await NotificationPermission.EnsureAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Contrôle quotidien non programmé : {ex}");
        }
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID
    internal static void Schedule(Context context, DateTimeOffset at)
    {
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager is null) return;
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) flags |= PendingIntentFlags.Immutable;
        var pending = PendingIntent.GetBroadcast(context, RequestCode, new Intent(context, typeof(DailyCheckReceiver)), flags);
        if (pending is null) return;
        // Pas d'alarme exacte (autorisation spéciale depuis Android 12) : quelques minutes de retard ne gênent pas.
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, at.ToUnixTimeMilliseconds(), pending);
        else alarmManager.Set(AlarmType.RtcWakeup, at.ToUnixTimeMilliseconds(), pending);
    }
#endif
}

/// <summary>Montre les alertes du centre de notifications comme notifications Android (canal « Alertes budget »).</summary>
public sealed class AlertPublisher(ISettingsService settings) : IAlertPublisher
{
    /// <summary>Sur l'écran verrouillé, ni le titre ni le montant d'une alerte ne s'affichent.</summary>
    internal const string PublicTitle = "Mes Finances";

    internal const string PublicText = "Une alerte est à consulter.";

    public Task<bool> PublishAsync(IReadOnlyList<AppNotification> alerts, CancellationToken cancellationToken = default)
    {
#if ANDROID
        var context = Android.App.Application.Context;
        if (!NotificationPermission.AreEnabled(context)) return Task.FromResult(false);

        var amount = NotificationTexts.Amounts(settings.HideAmounts, settings.CurrencyCode);
        foreach (var alert in alerts)
        {
            var text = NotificationTexts.Compose(alert, amount);
            ReminderService.ShowNotification(context, text.Title, text.Text, NotificationId(alert.Key), ReminderService.AlertChannelId, PublicTitle, PublicText);
        }
        return Task.FromResult(true);
#else
        return Task.FromResult(false);
#endif
    }

#if ANDROID
    // string.GetHashCode change à chaque lancement : un identifiant stable remplace une alerte au lieu de l'empiler.
    private static int NotificationId(string key)
    {
        var hash = 17;
        foreach (var c in key) hash = unchecked(hash * 31 + c);
        return 3000 + (hash & 0x00FFFFFF);
    }
#endif
}

#if ANDROID
/// <summary>Déclenché chaque matin par l'alarme de <see cref="DailyCheckScheduler"/>, application ouverte ou non.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class DailyCheckReceiver : BroadcastReceiver
{
    // Un récepteur dispose d'une dizaine de secondes avant que le système ne le juge bloqué.
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(8);

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        var result = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                // Le processus vient d'être créé si l'application était fermée : Application.OnCreate a déjà composé les services.
                var services = IPlatformApplication.Current?.Services;

                // Demain d'abord : même si le contrôle échoue aujourd'hui, la chaîne des contrôles ne se rompt pas.
                DailyCheckScheduler.Schedule(context, DailyCheckService.NextRun(services?.GetService<TimeProvider>() ?? TimeProvider.System));
                if (services is null) return;

                using var timeout = new CancellationTokenSource(Budget);
                await services.GetRequiredService<DailyCheckService>().RunAsync(timeout.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Contrôle quotidien interrompu : {ex}");
            }
            finally
            {
                result?.Finish();
            }
        });
    }
}
#endif
