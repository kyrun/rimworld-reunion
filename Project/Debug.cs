using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace Kyrun.Reunion
{
    public static class Debug
    {
        const string CATEGORY = "Reunion (by Kyrun)";

        [DebugAction(category = CATEGORY,
            name = "Force Start Reunion Event",
            requiresRoyalty = false,
            requiresIdeology = false,
            requiresBiotech = false,
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceStartReunionEvent()
        {
            GameComponent.DecideAndDoEvent();
        }

        [DebugAction(category = CATEGORY,
            name = "Make world pawn \"Ally\"...",
            requiresRoyalty = false,
            requiresIdeology = false,
            requiresBiotech = false,
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakeWorldPawnAlly()
        {
            List<DebugMenuOption> listDebugMenuOption = new List<DebugMenuOption>();
            Action<Pawn> actionPawn = delegate (Pawn p)
            {
                if (p != null && p.story != null)
                {
                    GameComponent.TryRemoveTrait(p);
                    GameComponent.ListAllyAvailable.Add(p);
                    Find.WorldPawns.RemovePawn(p);
                    Util.Msg(p.Name + " has been removed from the World and added to the Ally list.");
                    if (GameComponent.ListAllyAvailable.Count == 1) // list is not empty anymore, try to schedule a new event
                    {
                        GameComponent.TryScheduleNextEvent(ScheduleMode.Forced);
                    }
                }
            };

            foreach (Pawn current in Find.WorldPawns.AllPawnsAlive)
            {
                Pawn pLocal = current;
                if (current != null && current.story != null) // don't list those already with the trait
                {
                    listDebugMenuOption.Add(new DebugMenuOption(current.LabelShort, DebugMenuOptionMode.Action, delegate
                    {
                        actionPawn(pLocal);
                    }));
                }
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(listDebugMenuOption));
        }

        [DebugAction(category = CATEGORY,
            name = "Print Ally List",
            requiresRoyalty = false,
            requiresIdeology = false,
            requiresBiotech = false,
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PrintAllyList()
        {
            if (GameComponent.ListAllyAvailable.Count > 0)
            {
                Util.PrintAllyList();
            }
            else
            {
                Util.Msg("There are no allies in the Ally list!");
            }
        }
    }
}
