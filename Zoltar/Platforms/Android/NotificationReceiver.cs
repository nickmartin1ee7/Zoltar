using Android.App;
using Android.Content;

using AndroidX.Core.App;

namespace Zoltar.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(new[] { INTENT_FILTER })]
public class NotificationReceiver : BroadcastReceiver
{
    public const string INTENT_FILTER = "com.gitgoodsoftware.zoltar.NOTIFICATION_TRIGGER";

    public override void OnReceive(Context context, Intent intent)
    {
        SendNotification(
            context,
            "Your Fate Has Changed",
            "Zoltar has a new fortune for you! 🔮");
    }

    public void SendNotification(Context context, string title, string message)
    {
#if ANDROID21_0_OR_GREATER
#pragma warning disable CA1416 // Validate platform compatibility
        var notificationIntent = new Intent(context, typeof(MainActivity));
        notificationIntent.SetAction("android.intent.action.MAIN");
        notificationIntent.AddCategory("android.intent.category.LAUNCHER");

        var pendingIntent = PendingIntent.GetActivity(context, 0, notificationIntent, PendingIntentFlags.UpdateCurrent);

        var channelId = "new-fortune";
        var channelName = "New Fortune";
        var channelDescription = "Channel being notified when a New Fortune is available";

        var notificationChannel = new NotificationChannel(channelId, channelName, NotificationImportance.High)
        {
            Description = channelDescription
        };

        var notificationManager = NotificationManagerCompat.From(context);
        notificationManager.CreateNotificationChannel(notificationChannel);

        var notification = new NotificationCompat.Builder(context, channelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(Resource.Mipmap.appicon_round)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .Build();

        notificationManager.Notify(1, notification);
#pragma warning restore CA1416 // Validate platform compatibility
#endif
    }
}