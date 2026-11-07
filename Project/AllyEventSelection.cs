using System;
using System.Collections.Generic;

namespace Kyrun.Reunion
{
    // Pure event-selection policy, kept separate from RimWorld state so it can be tested
    // without loading the game assemblies.
    internal static class AllyEventSelection
    {
        internal static List<TEvent> GetAllowedEvents<TEvent>(
            IEnumerable<TEvent> allEvents,
            Func<TEvent, bool> isAllowed,
            IEnumerable<TEvent> soloSafeEvents,
            int playerColonistCount,
            bool enableHarderEventsWhenSolo)
        {
            var allowedEvents = new List<TEvent>();

            foreach (TEvent eventType in soloSafeEvents)
            {
                if (isAllowed(eventType))
                    allowedEvents.Add(eventType);
            }

            // Preserve the existing rules: harder events are included when explicitly
            // enabled, when the player has multiple colonists, or when no solo-safe
            // event is enabled.
            if (enableHarderEventsWhenSolo || allowedEvents.Count == 0 || playerColonistCount > 1)
            {
                foreach (TEvent eventType in allEvents)
                {
                    if (isAllowed(eventType) && !allowedEvents.Contains(eventType))
                        allowedEvents.Add(eventType);
                }
            }

            return allowedEvents;
        }
    }
}
