namespace Zoltar.Models.Services;

public interface IAlarmScheduler
{
    void ScheduleNotification(long triggerInMilliseconds);
}
