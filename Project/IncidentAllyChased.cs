using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace Kyrun.Reunion
{
    public static class IncidentAllyChased
    {
        public static bool Do()
        {
            var root = DefDatabase<QuestScriptDef>.GetNamed("Reunion_AllyChased");
            Quest quest = Util.GenerateQuestAndMakeAvailable(root);

            if (quest == null)
                return false;

            QuestUtility.SendLetterQuestAvailable(quest);
            return true;
        }
    }

    public class IncidentAllyChased_Join : QuestNode
    {
        protected override void RunInt()
        {
            Pawn pawn = GameComponent.CurrentGame?.GetRandomAllyForSpawning();
            if (pawn == null)
            {
                return;
            }

            Slate slate = QuestGen.slate;

            if (storeAs.GetValue(slate) != null)
            {
                QuestGen.slate.Set(storeAs.GetValue(slate), pawn, false);
            }
            if (addToList.GetValue(slate) != null)
            {
                QuestGenUtility.AddToOrMakeList(QuestGen.slate, addToList.GetValue(slate), pawn);
            }
            QuestGen.AddToGeneratedPawns(pawn);

            // Vanilla code: adds the pawn to the World.
            // For this mod, remove them from the available list and put them in the spawned list instead.
        }

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        [NoTranslate]
        public SlateRef<string> storeAs;

        [NoTranslate]
        public SlateRef<string> addToList;
    }

    public class IncidentAllyChased_PawnsArrive : QuestNode_PawnsArrive
    {
        // Intentionally empty. Used by Harmony transpiler.
    }

    public class QuestPart_PawnsArrive : RimWorld.QuestPart_PawnsArrive
    {
        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);

            // Only handle the MapRemoved signal for this quest's target map.
            if (!signal.tag.EndsWith(".MapRemoved", StringComparison.Ordinal))
                return;

            if (!signal.args.TryGetArg("SUBJECT", out MapParent subject) ||
                subject != mapParent)
                return;

            // The pawn's faction changes to Player when the arrival process runs.
            bool allySpawned = pawns.Exists(pawn =>
                pawn != null && pawn.Faction == Faction.OfPlayer);

            quest.End(
                allySpawned ? QuestEndOutcome.Success : QuestEndOutcome.Fail,
                sendLetter: true);
        }

        public override void Cleanup()
        {
            base.Cleanup(); // there's nothing in the base call (at the moment), but just in case in the future there's something

            GameComponent gameComponent = GameComponent.CurrentGame;
            if (gameComponent == null) return;

            // get pawns that spawned from Reunion
            var listToReturn = pawns.FindAll((pawn) =>
            {
                return pawn.Faction != Faction.OfPlayer && gameComponent.ListAllySpawned.Contains(pawn.GetUniqueLoadID());
            });

            gameComponent.FlagNextEventReadyForScheduling();
            foreach (var pawn in listToReturn)
            {
                gameComponent.ReturnToAvailable(pawn);
            }

            if (quest.State == QuestState.EndedOfferExpired) saveByReference = true;
            else gameComponent.TryScheduleNextEvent(ScheduleMode.Forced);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look<string>(ref this.inSignal, "inSignal", null, false);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                foreach (var pawn in pawns)
                {
                    if (pawn.Faction == Faction.OfPlayer) // if any pawn has already joined, that means it has spawned
                    {
                        saveByReference = true;
                        break;
                    }
                }
            }

            Scribe_Values.Look<bool>(ref this.saveByReference, "saveByReference", saveByReference, true);

            var lookModeForPawn = saveByReference ? LookMode.Reference : LookMode.Deep;
            Scribe_Collections.Look<Pawn>(ref this.pawns, "pawns", lookModeForPawn, Array.Empty<object>());

            Scribe_Defs.Look<PawnsArrivalModeDef>(ref this.arrivalMode, "arrivalMode");
            Scribe_References.Look<MapParent>(ref this.mapParent, "mapParent", false);
            Scribe_Values.Look<IntVec3>(ref this.spawnNear, "spawnNear", default(IntVec3), false);
            Scribe_Values.Look<bool>(ref this.joinPlayer, "joinPlayer", false, false);
            Scribe_Values.Look<string>(ref this.customLetterLabel, "customLetterLabel", null, false);
            Scribe_Values.Look<string>(ref this.customLetterText, "customLetterText", null, false);
            Scribe_Defs.Look<LetterDef>(ref this.customLetterDef, "customLetterDef");
            Scribe_Values.Look<bool>(ref this.sendStandardLetter, "sendStandardLetter", true, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.pawns.RemoveAll((Pawn x) => x == null);
            }
        }

        public bool saveByReference;
    }
}
