using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Grammar;

namespace Kyrun.Reunion
{
	static class Util
	{
		public const string DELIMITER = ", ";

		public static void Msg(object o)
		{
			Log.Message("[Reunion] " + o);
		}

		public static void Warn(object o)
		{
			Log.Warning("[Reunion] " + o);
		}

		public static void PrintAllyList()
		{
			var str = "";
			foreach (var ally in GameComponent.ListAllyAvailable)
			{
				str += ally.Name + DELIMITER;
			}
			if (str != "") str = str.Substring(0, str.Length - DELIMITER.Length); // truncate last delimiter
			Msg("Ally Pawns: " + str);
		}

		public static void PrintNextEventTimeRemaining()
		{
			if (Prefs.DevMode)
			{
				var diff = GameComponent.NextEventTick - Find.TickManager.TicksGame;
				Msg("Next event in " + ((float)diff / GenDate.TicksPerDay).ToString("0.00") + " days");
			}
		}

		// From virtual function (since calling base.Notify_GeneratedByQuestGen will run the parent and not the grandparent)
		public static void SitePartWorker_Base_Notify_GeneratedByQuestGen(SitePart part, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
		{
			outExtraDescriptionRules.AddRange(GrammarUtility.RulesForDef("", part.def));
			outExtraDescriptionConstants.Add("sitePart", part.def.defName);
		}

		public static void DressPawnIfCold(Pawn pawn, int tile)
		{
			// Create warm apparel in case they freeze to death
			PawnApparelGenerator.GenerateStartingApparelFor(pawn, new PawnGenerationRequest(
				pawn.kindDef, pawn.Faction, PawnGenerationContext.NonPlayer,
				tile, false, false, false, false, true, false, 0f,
				true, // "forceAddFreeWarmLayerIfNeeded": THIS IS THE MOST IMPORTANT VARIABLE
				true, true, true, false, false, false, false, 0f, 0f, null, 0f, null, null, null, null,
				new float?(0.0f), null, null, null, null, null, null, null));
		}

		public static void OnPostDestroyReschedule(SitePart sitePart)
		{
			// If recruited, will have been flagged on map generate: schedule will fire on DoRecruit
			// If abandoned but pawn alive, also will have been flagged on map generate: schedule will fire on PassToWorld

			// If quest failed without entering the map, flag here: schedule will fire on PassToWorld
			if (GameComponent.NextEventTick == -1) GameComponent.FlagNextEventReadyForScheduling();

			if (sitePart.things != null && sitePart.things.Any)
			{
				Pawn pawn = (Pawn)sitePart.things[0];
				// If pawn is dead on post destroy: schedule it HERE!
				if (pawn.Dead)
				{
					GameComponent.TryScheduleNextEvent();
				}
			}
			// No "thing" in SitePart, pawn is recruited, or something else happened.
			// As mentioned above, if recruited, schedule will already have fired on DoRecruit.
			// If not recruited for whatever reason (likely dead, according to tests): schedule it HERE!
			else if (GameComponent.NextEventTick == 0) GameComponent.TryScheduleNextEvent();
		}
	}
}
