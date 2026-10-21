using Finance.Android17.Application;

#if ANDROID
using System.Text.Json;
using Android.App;
using Android.Content;
#endif

namespace Finance.Android17.Maui;

public sealed class ReminderService : IReminderService
{
    internal const string ChannelId = "subscription-reminders";
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
        var wanted = plan.Alarms.Select(x => x.SubscriptionId).ToHashSet();
        foreach (var stale in LoadStored().Where(x => !wanted.Contains(x.SubscriptionId)))
            Cancel(context, alarmManager, stale.SubscriptionId);

        if (plan.Alarms.Count == 0 && plan.Notice is null)
        {
            SaveStored([]);
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            // La demande d'autorisation doit se faire sur le thread principal.
            var status = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var current = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                return current == PermissionStatus.Granted ? current : await Permissions.RequestAsync<Permissions.PostNotifications>();
            });
            if (status != PermissionStatus.Granted)
            {
                foreach (var stored in LoadStored()) Cancel(context, alarmManager, stored.SubscriptionId);
                SaveStored([]);
                return;
            }
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

    internal static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        var channel = new NotificationChannel(ChannelId, "Échéances d’abonnements", NotificationImportance.Default)
        {
            LockscreenVisibility = NotificationVisibility.Private
        };
        manager?.CreateNotificationChannel(channel);
    }

    internal static void ShowNotification(Context context, string title, string text, int id)
    {
        EnsureChannel(context);
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null) return;
        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        var pending = launchIntent is null ? null : PendingIntent.GetActivity(context, 0, launchIntent, BaseFlags());

        // Sur l'écran verrouillé, seule la version publique (sans nom d'abonnement) s'affiche.
        var publicVersion = NewBuilder(context).SetContentTitle(title).SetContentText(PublicNoticeText).Build();
        var builder = NewBuilder(context).SetContentTitle(title).SetContentText(text).SetAutoCancel(true)
            .SetVisibility(NotificationVisibility.Private).SetPublicVersion(publicVersion);
        if (pending is not null) builder.SetContentIntent(pending);
        manager.Notify(id, builder.Build());
    }

    private static Notification.Builder NewBuilder(Context context) =>
        (OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(context, ChannelId) : new Notification.Builder(context))
            .SetSmallIcon(Resource.Mipmap.appicon);
#endif
}

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

/// <summary>Redémarrage du téléphone : Android a perdu les alarmes, on reprogramme celles qui sont encore à venir.</summary>
[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false)]
[IntentFilter([Intent.ActionBootCompleted])]
public sealed class ReminderBootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != Intent.ActionBootCompleted) return;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager is null) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var alarm in ReminderService.LoadStored().Where(x => x.TriggerAtMs > now))
            ReminderService.Schedule(context, alarmManager, alarm);
    }
}
#endif
