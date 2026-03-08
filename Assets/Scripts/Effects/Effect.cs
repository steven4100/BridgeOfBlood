using UnityEngine;

public class Effect
{
    
}



public enum ScheduleType
{
    Game,
    Round,
    SpellLoop,
    NextSpell,
}


public interface ISchedulable
{
    public ScheduleType scheduleType { get; }

    public void OnExpired();
}




