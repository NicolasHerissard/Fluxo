using Finance.Android17.Application;

#if ANDROID
using System.Text.Json;
using Android.App;
using Android.Content;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace Finance.Android17.Maui;

public sealed class ReminderService : IReminderService
{
    internal const string ChannelId = "subscription-reminders";
    internal const string AlertChannelId = "budget-alerts";
    private const int NoticeId = 2401;
    internal const string PublicNoticeText = "Une échéance approche.";

#if ANDROID
    private const string StoredAlarmsKey = "reminder_alarms_v1";

    /// <summary>Alarme programmée, conservée pour pouvoir l'annuler plus tard et la reprogrammer après un redémarrage du téléphone.</summary>
    internal sealed record StoredAlarm(Guid SubscriptionId, string Message, long TriggerAtMs);
#endif

    public async Task ApplyAsync(ReminderPlan plan, CancellationToken cancellationToken = default)
    {
#if ANDROID
        var context = Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager is null) return;

        // Les alarmes qui ne figurent plus dans le plan (abonnement supprimé ou modifié, rappels désactivés,
        // données remplacées) sont annulées avant toute chose, même sans autorisation de notification.
        // Une alarme dont l'heure est passée a déjà sonné (ou sonne à l'instant) : l'annuler ferait perdre le rappel.
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var wanted = plan.Alarms.Select(x => x.SubscriptionId).ToHashSet();
        foreach (var stale in LoadStored().Where(x => x.TriggerAtMs > now && !wanted.Contains(x.SubscriptionId)))
            Cancel(context, alarmManager, stale.SubscriptionId);

        if (plan.Alarms.Count == 0 && plan.Notice is null)
        {
            SaveStored([]);
            return;
        }

        if (!await NotificationPermission.EnsureAsync())
        {
            foreach (var stored in LoadStored()) Cancel(context, alarmManager, stored.SubscriptionId);
            SaveStored([]);
            return;
        }

        EnsureChannel(context);
        var scheduled = new List<StoredAlarm>();
        foreach (var reminder in plan.Alarms)
        {
            var alarm = new StoredAlarm(reminder.SubscriptionId, reminder.Message, reminder.TriggerAt.ToUnixTimeMilliseconds());
            if (Schedule(context, alarmManager, alarm)) scheduled.Add(alarm);
        }
        SaveStored(scheduled);

        if (plan.Notice is { } notice && !Preferences.Default.Get(notice.Key, false))
        {
            ShowNotification(context, notice.Title, notice.Text, NoticeId);
            Preferences.Default.Set(notice.Key, true);
        }
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID
    private static PendingIntentFlags BaseFlags()
    {
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) flags |= PendingIntentFlags.Immutable;
        return flags;
    }

    // Guid.GetHashCode est stable d'un lancement à l'autre : c'est le code de requête qui identifie l'alarme.
    internal static bool Schedule(Context context, AlarmManager alarmManager, StoredAlarm alarm)
    {
        var intent = new Intent(context, typeof(SubscriptionReminderReceiver));
        intent.PutExtra("message", alarm.Message);
        var pending = PendingIntent.GetBroadcast(context, alarm.SubscriptionId.GetHashCode(), intent, BaseFlags());
        if (pending is null) return false;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, alarm.TriggerAtMs, pending);
        else alarmManager.Set(AlarmType.RtcWakeup, alarm.TriggerAtMs, pending);
        return true;
    }

    private static void Cancel(Context context, AlarmManager alarmManager, Guid subscriptionId)
    {
        var flags = PendingIntentFlags.NoCreate;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) flags |= PendingIntentFlags.Immutable;
        var pending = PendingIntent.GetBroadcast(context, subscriptionId.GetHashCode(), new Intent(context, typeof(SubscriptionReminderReceiver)), flags);
        if (pending is null) return;
        alarmManager.Cancel(pending);
        pending.Cancel();
    }

    internal static IReadOnlyList<StoredAlarm> LoadStored()
    {
        try
        {
            var json = Preferences.Default.Get(StoredAlarmsKey, string.Empty);
            return string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<StoredAlarm>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void SaveStored(IReadOnlyList<StoredAlarm> alarms) =>
        Preferences.Default.Set(StoredAlarmsKey, JsonSerializer.Serialize(alarms));

    internal static void EnsureChannel(Context context, string channelId = ChannelId)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        var name = channelId == AlertChannelId ? "Alertes budget" : "Échéances d’abonnements";
        var channel = new NotificationChannel(channelId, name, NotificationImportance.Default)
        {
            LockscreenVisibility = NotificationVisibility.Private
        };
        manager?.CreateNotificationChannel(channel);
    }

    internal static void ShowNotification(Context context, string title, string text, int id,
        string channelId = ChannelId, string? publicTitle = null, string publicText = PublicNoticeText)
    {
        EnsureChannel(context, channelId);
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null) return;
        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        var pending = launchIntent is null ? null : PendingIntent.GetActivity(context, 0, launchIntent, BaseFlags());

        // Sur l'écran verrouillé, seule la version publique (sans nom d'abonnement ni montant) s'affiche.
        var publicVersion = NewBuilder(context, channelId).SetContentTitle(publicTitle ?? title).SetContentText(publicText).Build();
        var builder = NewBuilder(context, channelId).SetContentTitle(title).SetContentText(text).SetAutoCancel(true)
            .SetVisibility(NotificationVisibility.Private).SetPublicVersion(publicVersion);
        if (pending is not null) builder.SetContentIntent(pending);
        manager.Notify(id, builder.Build());
    }

    private static Notification.Builder NewBuilder(Context context, string channelId) =>
        (OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(context, channelId) : new Notification.Builder(context))
            .SetSmallIcon(Resource.Mipmap.appicon);
#endif
}

#if ANDROID
internal static class NotificationPermission
{
    // Android cesse de proposer la boîte de dialogue après deux refus : une demande par lancement suffit.
    private static bool _asked;

    /// <summary>
    /// Vrai si l'application peut notifier. Au premier plan, demande l'autorisation (Android 13+) ; en arrière-plan,
    /// il n'y a pas d'activité où l'afficher, on constate seulement.
    /// </summary>
    internal static Task<bool> EnsureAsync()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33)) return Task.FromResult(true);
        // La demande d'autorisation doit se faire sur le thread principal.
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (await Permissions.CheckStatusAsync<Permissions.PostNotifications>() == PermissionStatus.Granted) return true;
            if (_asked || Platform.CurrentActivity is null) return false;
            _asked = true;
            return await Permissions.RequestAsync<Permissions.PostNotifications>() == PermissionStatus.Granted;
        });
    }

    /// <summary>État actuel, sans rien demander : utilisable depuis un récepteur.</summary>
    internal static bool AreEnabled(Context context) =>
        !OperatingSystem.IsAndroidVersionAtLeast(24) ||
        (context.GetSystemService(Context.NotificationService) as NotificationManager)?.AreNotificationsEnabled() != false;
}
#endif

#if ANDROID
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class SubscriptionReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        // Les alarmes programmées par une version précédente portent « name » et « date » au lieu de « message ».
        var message = intent?.GetStringExtra("message")
            ?? $"{intent?.GetStringExtra("name") ?? "Abonnement"} arrive le {intent?.GetStringExtra("date") ?? string.Empty}.";
        ReminderService.ShowNotification(context, ReminderPlanner.NoticeTitle, message, message.GetHashCode());
    }
}

/// <summary>
/// Redémarrage du téléphone (ou mise à jour de l'application) : Android a pu perdre les alarmes, on reprogramme
/// les rappels encore à venir et le contrôle quotidien.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionMyPackageReplaced])]
public sealed class ReminderBootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action is not (Intent.ActionBootCompleted or Intent.ActionMyPackageReplaced)) return;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager is null) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var alarm in ReminderService.LoadStored().Where(x => x.TriggerAtMs > now))
            ReminderService.Schedule(context, alarmManager, alarm);

        var clock = IPlatformApplication.Current?.Services.GetService<TimeProvider>() ?? TimeProvider.System;
        DailyCheckScheduler.Schedule(context, DailyCheckService.NextRun(clock, IPlatformApplication.Current?.Services.GetService<ISettingsService>()?.LastDailyCheckDay ?? string.Empty));
    }
}
#endif
