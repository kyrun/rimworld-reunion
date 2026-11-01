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
            Quest quest = Util.GenerateQuestAndMakeAvailable(root);

            if (quest == null)
                return false;

            QuestUtility.SendLetterQuestAvailable(quest);
            return true;
        }
    }

    public class SitePartWorker_PrisonerRescue : RimWorld.Planet.SitePartWorker_PrisonerWillingToJoin
    {
        public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            // Reproduce the grandparent setup without invoking vanilla pawn generation.
            Util.SitePartWorker_Base_Notify_GeneratedByQuestGen(part, outExtraDescriptionRules, outExtraDescriptionConstants);

            // Replaces PrisonerWillingToJoinQuestUtility.GeneratePrisoner.
            Pawn pawn = Util.PrepareAllyForSitePart(part);
            if (pawn == null)
                return;

            pawn.guest.SetGuestStatus(part.site.Faction, GuestStatus.Prisoner);
            slate.Set<Pawn>("prisoner", pawn, false);

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

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            GameComponent.CurrentGame?.FlagNextEventReadyForScheduling();
        }

        public override void PostDestroy(SitePart sitePart)
        {
            base.PostDestroy(sitePart);
            Util.OnPostDestroyReschedule(sitePart);
        }
    }
}
