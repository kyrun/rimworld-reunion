using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;
using HarmonyLib;
using Random = UnityEngine.Random;

namespace Kyrun.Reunion
{
	public static class IncidentAllyRefugeePod
	{
		public static bool Do()
		{
			var pawn = GameComponent.GetRandomAllyForSpawning();
			if (pawn == null) return false;

			HealthUtility.DamageUntilDowned(pawn, true);

			var def = IncidentDefs.Reunion_AllyRefugeePod;
			Map map = Current.Game.RandomPlayerHomeMap;
			List<Thing> things = new List<Thing>();
			things.Add(pawn);
			IntVec3 intVec = DropCellFinder.RandomDropSpot(map);
			pawn.guest.getRescuedThoughtOnUndownedBecauseOfPlayer = true;
			//pawn.mindState.WillJoinColonyIfRescued = true; // will trigger LetterRescueQuestFinished which is wrong. We want LetterRescueeJoins

			TaggedString baseLetterLabel = def.letterLabel.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
			TaggedString baseLetterText = def.letterText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);

			Find.LetterStack.ReceiveLetter(baseLetterLabel, baseLetterText, def.letterDef, new LookTargets(pawn));

			ActiveDropPodInfo activeDropPodInfo = new ActiveDropPodInfo();
			activeDropPodInfo.innerContainer.TryAddRangeOrTransfer(things, true, false);
			activeDropPodInfo.openDelay = 180;
			activeDropPodInfo.leaveSlag = true;
			DropPodUtility.MakeDropPodAt(intVec, map, activeDropPodInfo);

			GameComponent.TryScheduleNextEvent(true);
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
