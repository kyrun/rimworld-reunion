namespace Kyrun.Reunion
{
    // Pure scheduling guard, separated from GameComponent state for unit testing.
    internal static class EventSchedulePolicy
    {
        internal static bool ShouldSchedule(
            int nextEventTick,
            int currentTick,
            bool forced,
            bool settingsUpdated)
        {
            if (!forced && nextEventTick == -1)
                return false;

            if (!forced && !settingsUpdated && nextEventTick > currentTick)
                return false;

            return true;
        }
    }
}
