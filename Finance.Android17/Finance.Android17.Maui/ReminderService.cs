using Finance.Android17.Application;
using Finance.Android17.Domain;

#if ANDROID
using Android.App;
using Android.Content;
#endif

namespace Finance.Android17.Maui;

public sealed class ReminderService : IReminderService
{
    internal const string ChannelId = "subscription-reminders";

    public async Task ShowSubscriptionReminderAsync(IReadOnlyList<Subscription> subscriptions, int reminderDays, CancellationToken cancellationToken = default)
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted) status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted) return;
        }

        var context = Android.App.Application.Context;
        EnsureChannel(context);
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager is null) return;
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) flags |= PendingIntentFlags.Immutable;

        foreach (var subscription in subscriptions.Where(x => x.ReminderEnabled))
        {
            var trigger = subscription.NextBillingDate.Date.AddDays(-Math.Max(0, reminderDays)).AddHours(9);
            if (trigger <= DateTime.Now) continue;
            var intent = new Intent(context, typeof(SubscriptionReminderReceiver));
            intent.PutExtra("name", subscription.Name);
            intent.PutExtra("date", subscription.NextBillingDate.ToString("dd/MM"));
            var pending = PendingIntent.GetBroadcast(context, subscription.Id.GetHashCode(), intent, flags);
            if (pending is null) continue;
            var triggerAt = new DateTimeOffset(trigger).ToUnixTimeMilliseconds();
            if (OperatingSystem.IsAndroidVersionAtLeast(23)) alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pending);
            else alarmManager.Set(AlarmType.RtcWakeup, triggerAt, pending);
        }

        var upcoming = subscriptions.Where(x => x.ReminderEnabled && x.NextBillingDate.Date >= DateTime.Today && x.NextBillingDate.Date <= DateTime.Today.AddDays(Math.Max(0, reminderDays))).ToList();
        if (upcoming.Count == 0) return;
        var reminderKey = $"subscription_reminder_{DateTime.Today:yyyyMMdd}";
        if (Preferences.Default.Get(reminderKey, false)) return;
        var first = upcoming[0];
        var text = upcoming.Count == 1 ? $"{first.Name} arrive le {first.NextBillingDate:dd/MM}." : $"{upcoming.Count} abonnements arrivent dans les {reminderDays} prochains jours.";
        ShowNotification(context, "Échéance à venir", text, 2401);
        Preferences.Default.Set(reminderKey, true);
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID
    internal static void EnsureChannel(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(ChannelId, "Échéances d’abonnements", NotificationImportance.Default));
    }

    internal static void ShowNotification(Context context, string title, string text, int id)
    {
        EnsureChannel(context);
        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null) return;
        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        var flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23)) flags |= PendingIntentFlags.Immutable;
        var pending = launchIntent is null ? null : PendingIntent.GetActivity(context, 0, launchIntent, flags);
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(context, ChannelId) : new Notification.Builder(context);
        builder.SetSmallIcon(Resource.Mipmap.appicon).SetContentTitle(title).SetContentText(text).SetAutoCancel(true);
        if (pending is not null) builder.SetContentIntent(pending);
        manager.Notify(id, builder.Build());
    }
#endif
}

#if ANDROID
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class SubscriptionReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;
        var name = intent?.GetStringExtra("name") ?? "Abonnement";
        var date = intent?.GetStringExtra("date") ?? string.Empty;
        ReminderService.ShowNotification(context, "Échéance à venir", $"{name} arrive le {date}.", name.GetHashCode());
    }
}
#endif
