using Android.App;
using Android.Content;

using Zoltar.Platforms.Android;

namespace Zoltar;

public class AlarmScheduler : IAlarmScheduler
{
    public void ScheduleNotification(long triggerInMilliseconds)
    {
#if ANDROID21_0_OR_GREATER
#pragma warning disable CA1416 // Validate platform compatibility
        var context = Platform.CurrentActivity;
        var alarmIntent = new Intent(context, typeof(NotificationReceiver));
        alarmIntent.SetAction(NotificationReceiver.INTENT_FILTER);

        var pendingIntent = PendingIntent.GetBroadcast(context, 0, alarmIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var alarmManager = (AlarmManager)context.GetSystemService(Context.AlarmService);
        alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerInMilliseconds, pendingIntent);
#pragma warning restore CA1416 // Validate platform compatibility
#endif
    }
}
