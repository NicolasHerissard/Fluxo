using Finance.Android17.Application;

#if ANDROID
using Android.App;
using Android.Content;
#endif

namespace Finance.Android17.Maui;

public sealed class ReminderService : IReminderService
{
    internal const string ChannelId = "subscription-reminders";
    private const int NoticeId = 2401;

    public async Task ApplyAsync(ReminderPlan plan, CancellationToken cancellationToken = default)
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

        foreach (var reminder in plan.Alarms)
        {
            var intent = new Intent(context, typeof(SubscriptionReminderReceiver));
            intent.PutExtra("message", reminder.Message);
            var pending = PendingIntent.GetBroadcast(context, reminder.SubscriptionId.GetHashCode(), intent, flags);
            if (pending is null) continue;
            var triggerAt = reminder.TriggerAt.ToUnixTimeMilliseconds();
            if (OperatingSystem.IsAndroidVersionAtLeast(23)) alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pending);
            else alarmManager.Set(AlarmType.RtcWakeup, triggerAt, pending);
        }

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
        // Les alarmes programmées par une version précédente portent « name » et « date » au lieu de « message ».
        var message = intent?.GetStringExtra("message")
            ?? $"{intent?.GetStringExtra("name") ?? "Abonnement"} arrive le {intent?.GetStringExtra("date") ?? string.Empty}.";
        ReminderService.ShowNotification(context, ReminderPlanner.NoticeTitle, message, message.GetHashCode());
    }
}
#endif
