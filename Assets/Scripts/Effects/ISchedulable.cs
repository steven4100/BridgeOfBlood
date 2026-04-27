public interface ISchedulable
{
	public ScheduleType scheduleType { get; }

	public void OnExpired();
}
