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
			var points = StorytellerUtility.DefaultThreatPointsNow(Current.Game.AnyPlayerHomeMap);
			QuestUtility.SendLetterQuestAvailable(QuestUtility.GenerateQuestAndMakeAvailable(root, points));

			return true;
		}
	}

	public class IncidentAllyChased_Join : QuestNode
	{
		protected override void RunInt()
		{
			var pawn = GameComponent.GetRandomAllyForSpawning();

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
		protected override void RunInt()
		{
			Slate slate = QuestGen.slate;
			PawnsArrivalModeDef pawnsArrivalModeDef = this.arrivalMode.GetValue(slate) ?? PawnsArrivalModeDefOf.EdgeWalkIn;

			// this line is the only thing changed (we are using custom QuestPart)
			var pawnsArrive = new Kyrun.Reunion.QuestPart_PawnsArrive();

			pawnsArrive.inSignal = (QuestGenUtility.HardcodedSignalWithQuestID(this.inSignal.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignal", null, false));
			pawnsArrive.pawns.AddRange(this.pawns.GetValue(slate));
			pawnsArrive.arrivalMode = pawnsArrivalModeDef;
			pawnsArrive.joinPlayer = this.joinPlayer.GetValue(slate);
			pawnsArrive.mapParent = QuestGen.slate.Get<Map>("map", null, false).Parent;
			if (pawnsArrivalModeDef.walkIn)
			{
				pawnsArrive.spawnNear = (this.walkInSpot.GetValue(slate) ?? (QuestGen.slate.Get<IntVec3?>("walkInSpot", null, false) ?? IntVec3.Invalid));
			}
			if (!this.customLetterLabel.GetValue(slate).NullOrEmpty() || this.customLetterLabelRules.GetValue(slate) != null)
			{
				QuestGen.AddTextRequest("root", delegate (string x)
				{
					pawnsArrive.customLetterLabel = x;
				}, QuestGenUtility.MergeRules(this.customLetterLabelRules.GetValue(slate), this.customLetterLabel.GetValue(slate), "root"));
			}
			if (!this.customLetterText.GetValue(slate).NullOrEmpty() || this.customLetterTextRules.GetValue(slate) != null)
			{
				QuestGen.AddTextRequest("root", delegate (string x)
				{
					pawnsArrive.customLetterText = x;
				}, QuestGenUtility.MergeRules(this.customLetterTextRules.GetValue(slate), this.customLetterText.GetValue(slate), "root"));
			}
			QuestGen.quest.AddPart(pawnsArrive);
		}
	}

	public class QuestPart_PawnsArrive : RimWorld.QuestPart_PawnsArrive
	{
		public override void Cleanup()
		{
			base.Cleanup(); // there's nothing in the base call (at the moment), but just in case in the future there's something

			// get pawns that spawned from Reunion
			var listToReturn = pawns.FindAll((pawn) =>
			{
				return pawn.Faction != Faction.OfPlayer && GameComponent.ListAllySpawned.Contains(pawn.GetUniqueLoadID());
			});

			foreach (var pawn in listToReturn)
			{
				GameComponent.ReturnToAvailable(pawn, GameComponent.ListAllySpawned, GameComponent.ListAllyAvailable);
			}

			if (quest.State == QuestState.EndedOfferExpired) saveByReference = true;
			GameComponent.TryScheduleNextEvent();
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
