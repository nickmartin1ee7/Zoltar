using Android.App;
using Android.Content;

using Zoltar.Models.Services;
using Zoltar.Platforms.Android;

namespace Zoltar;

public class AlarmScheduler : IAlarmScheduler
{
    public void ScheduleNotification(long triggerInMilliseconds)
    {
#if ANDROID23_0_OR_GREATER
#pragma warning disable CA1416 // Validate platform compatibility - #if above checks this
        var context = Platform.CurrentActivity
            ?? throw new ArgumentNullException(nameof(Platform.CurrentActivity));

        var alarmIntent = new Intent(context, typeof(NotificationReceiver));
        alarmIntent.SetAction(NotificationReceiver.INTENT_FILTER);

        var pendingIntent = PendingIntent.GetBroadcast(context, 0, alarmIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;

        ArgumentNullException.ThrowIfNull(alarmManager);
        ArgumentNullException.ThrowIfNull(pendingIntent);

        alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerInMilliseconds, pendingIntent);
#pragma warning restore CA1416 // Validate platform compatibility
#endif
    }
}
