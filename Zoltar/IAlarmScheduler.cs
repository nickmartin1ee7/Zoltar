namespace Zoltar;

public interface IAlarmScheduler
{
    void ScheduleNotification(long triggerInMilliseconds);
}
