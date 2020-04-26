using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;
using Verse.Grammar;

namespace Kyrun.Reunion
{
	public static class IncidentAllyPrisonerRescue
	{
		public static bool Do()
		{
			var root = DefDatabase<QuestScriptDef>.GetNamed("Reunion_PrisonerRescue");
			var points = StorytellerUtility.DefaultThreatPointsNow(Current.Game.AnyPlayerHomeMap);
			QuestUtility.SendLetterQuestAvailable(QuestUtility.GenerateQuestAndMakeAvailable(root, points));

			return true;
		}
	}

	public class SitePartWorker_PrisonerRescue : RimWorld.Planet.SitePartWorker_PrisonerWillingToJoin
	{
		public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
		{
			// Duplicate code so modularize in Util
			Util.SitePartWorker_Base_Notify_GeneratedByQuestGen(part, outExtraDescriptionRules, outExtraDescriptionConstants);

			// Replaces PrisonerWillingToJoinQuestUtility.GeneratePrisoner
			Pawn pawn = GameComponent.GetRandomAllyForSpawning();

			part.things = new ThingOwner<Pawn>(part, true, LookMode.Deep);
			part.things.TryAdd(pawn, true);
			string text;
			PawnRelationUtility.Notify_PawnsSeenByPlayer(Gen.YieldSingle<Pawn>(pawn), out text, true, false);
			outExtraDescriptionRules.AddRange(GrammarUtility.RulesForPawn("prisoner", pawn, outExtraDescriptionConstants, true, true));
			string output;
			if (!text.NullOrEmpty())
			{
				output = "\n\n" + "PawnHasTheseRelationshipsWithColonists".Translate(pawn.LabelShort, pawn) + "\n\n" + text;
			}
			else
			{
				output = "";
			}
			outExtraDescriptionRules.Add(new Rule_String("prisonerFullRelationInfo", output));
		}
	}
}
