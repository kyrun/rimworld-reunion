using System;
using System.Collections.Generic;

namespace Kyrun.Reunion
{
    // Pure list/state operations for Ally tracking. RimWorld-specific validity checks
    // and Pawn mutations remain in GameComponent.
    internal static class AllyRosterPolicy
    {
        internal static bool HasAvailable<T>(IEnumerable<T> available, Predicate<T> isValid)
        {
            foreach (T item in available)
            {
                if (isValid(item))
                    return true;
            }

            return false;
        }

        internal static bool CanAddAvailable<T>(
            List<T> available,
            T item,
            Predicate<T> isValid)
        {
            return isValid(item) && !available.Contains(item);
        }

        internal static bool AddAvailable<T>(
            List<T> available,
            T item,
            Predicate<T> isValid)
        {
            if (!CanAddAvailable(available, item, isValid))
                return false;

            available.Add(item);
            return true;
        }

        internal static int RemoveInvalid<T>(List<T> available, Predicate<T> isValid)
        {
            return available.RemoveAll(item => !isValid(item));
        }

        internal static bool MoveToSpawned<T>(
            List<T> available,
            List<string> spawnedIds,
            T item,
            Func<T, string> getId)
        {
            if (available.RemoveAll(candidate =>
                EqualityComparer<T>.Default.Equals(candidate, item)) == 0)
            {
                return false;
            }

            string id = getId(item);
            if (!spawnedIds.Contains(id))
                spawnedIds.Add(id);

            return true;
        }

        internal static bool RemoveSpawned<T>(
            List<string> spawnedIds,
            T item,
            Func<T, string> getId)
        {
            return spawnedIds.RemoveAll(id => id == getId(item)) > 0;
        }
    }
}
