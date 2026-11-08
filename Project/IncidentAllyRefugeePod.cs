using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Kyrun.Reunion
{
    public static class IncidentAllyRefugeePod
    {
        public static bool Do()
        {
            GameComponent gameComponent = GameComponent.CurrentGame;
            if (gameComponent == null) return false;

            Pawn pawn = gameComponent.GetRandomAllyForSpawning("Refugee Pod");
            if (pawn == null) return false;

            HealthUtility.DamageUntilDowned(pawn, true);

            var def = IncidentDefs.Reunion_AllyRefugeePod;
            Map map = Current.Game.RandomPlayerHomeMap;
            List<Thing> things = new List<Thing>();
            things.Add(pawn);
            IntVec3 intVec = DropCellFinder.RandomDropSpot(map);
            pawn.guest.getRescuedThoughtOnUndownedBecauseOfPlayer = true;

            // NOTE: This shows the Downed Refugee message which mentions "Reform caravan",
            // but I don't think people read this, and it's a minor issue if they do.
            pawn.mindState.WillJoinColonyIfRescued = true;
            

            TaggedString baseLetterLabel = def.letterLabel.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
            TaggedString baseLetterText = def.letterText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);

            Find.LetterStack.ReceiveLetter(baseLetterLabel, baseLetterText, def.letterDef, new LookTargets(pawn));

            ActiveTransporterInfo activeDropPodInfo = new ActiveTransporterInfo();
            activeDropPodInfo.innerContainer.TryAddRangeOrTransfer(things, true, false);
            activeDropPodInfo.openDelay = 180;
            activeDropPodInfo.leaveSlag = true;
            DropPodUtility.MakeDropPodAt(intVec, map, activeDropPodInfo);

            gameComponent.TryScheduleNextEvent(ScheduleMode.Forced);
            return true;
        }
    }

    public class IncidentWorker_AllyRefugeePod : IncidentWorker
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            return IncidentAllyRefugeePod.Do();
        }
    }
}
