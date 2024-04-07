using RimWorld;
using Verse;

namespace Kyrun.Reunion
{
	public static class IncidentAllyJoin
	{
		public static bool Do()
		{
			Map map =  Current.Game.RandomPlayerHomeMap;
			IntVec3 loc;
			if (!TryFindEntryCell(map, out loc))
			{
				return false;
			}

            Pawn pawn = GameComponent.GetRandomAllyForSpawning();
            if (pawn == null)
            {
                return false;
            }

            pawn.SetFactionDirect(Faction.OfPlayer);
			GenSpawn.Spawn(pawn, loc, map, WipeMode.Vanish);

			var def = IncidentDefs.Reunion_AllyJoin;
			TaggedString baseLetterLabel = def.letterLabel.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
			TaggedString baseLetterText = def.letterText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);

			Find.LetterStack.ReceiveLetter(baseLetterLabel, baseLetterText, def.letterDef, new LookTargets(pawn));

			GameComponent.TryScheduleNextEvent(ScheduleMode.Forced);

			return true;
		}

		// TODO: Reverse Patch
		public static bool TryFindEntryCell(Map map, out IntVec3 cell) // copied function
		{
			return CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) => map.reachability.CanReachColony(c) && !c.Fogged(map),
				map, CellFinder.EdgeRoadChance_Neutral, out cell);
		}
	}


	public class IncidentWorker_AllyJoin : IncidentWorker
	{
		protected override bool CanFireNowSub(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			IntVec3 intVec;
			return IncidentAllyJoin.TryFindEntryCell(map, out intVec);
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			return IncidentAllyJoin.Do();
		}
	}
}
