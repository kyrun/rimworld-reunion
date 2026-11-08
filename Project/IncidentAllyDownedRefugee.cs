using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;
using Verse.Grammar;

namespace Kyrun.Reunion
{
    public static class IncidentAllyDownedRefugee
    {
        public static bool Do()
        {
            var root = DefDatabase<QuestScriptDef>.GetNamed("Reunion_DownedRefugee");
            Quest quest = Util.GenerateQuestAndMakeAvailable(root);

            if (quest == null)
                return false;

            QuestUtility.SendLetterQuestAvailable(quest);
            return true;
        }
    }

    public class SitePartWorker_DownedRefugee : RimWorld.Planet.SitePartWorker_DownedRefugee
    {
        public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            // Reproduce the grandparent setup without invoking vanilla pawn generation.
            Util.SitePartWorker_Base_Notify_GeneratedByQuestGen(part, outExtraDescriptionRules, outExtraDescriptionConstants);

            // Replaces DownedRefugeeQuestUtility.GenerateRefugee.
            Pawn pawn = Util.PrepareAllyForSitePart(part, "Downed Refugee");
            if (pawn == null)
                return;

            slate.Set<Pawn>("refugee", pawn, false);

            HealthUtility.DamageUntilDowned(pawn, false);
            HealthUtility.DamageLegsUntilIncapableOfMoving(pawn, false);

            if (pawn.relations != null)
            {
                pawn.relations.everSeenByPlayer = true;
            }
            Pawn mostImportantColonyRelative = PawnRelationUtility.GetMostImportantColonyRelative(pawn);
            if (mostImportantColonyRelative != null)
            {
                PawnRelationDef mostImportantRelation = mostImportantColonyRelative.GetMostImportantRelation(pawn);
                TaggedString taggedString = "";
                if (mostImportantRelation != null && mostImportantRelation.opinionOffset > 0)
                {
                    pawn.relations.relativeInvolvedInRescueQuest = mostImportantColonyRelative;
                    taggedString = "\n\n" + "RelatedPawnInvolvedInQuest".Translate(mostImportantColonyRelative.LabelShort, mostImportantRelation.GetGenderSpecificLabel(pawn), mostImportantColonyRelative.Named("RELATIVE"), pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
                }
                else
                {
                    PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref taggedString, pawn);
                }
                outExtraDescriptionRules.Add(new Rule_String("pawnInvolvedInQuestInfo", taggedString));
            }
            outExtraDescriptionRules.AddRange(GrammarUtility.RulesForPawn("refugee", pawn, outExtraDescriptionConstants, true, true));
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
