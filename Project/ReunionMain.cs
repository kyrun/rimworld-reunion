//#define TESTING
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using Harmony;
using Random = UnityEngine.Random;

namespace Kyrun
{
	public class ReunionSettings : ModSettings
	{
		public int minimumProbability = 10;
		public int probabilityIncrementStep = 10;
		public bool allowEventWandererJoins = true;
		public bool allowEventRefugeeChased = true;
		public bool allowEventPrisonerRescue = true;
		public bool allowEventDownedRefugee = true;

		public override void ExposeData()
		{
			Scribe_Values.Look(ref minimumProbability, "minimumProbability", 10);
			Scribe_Values.Look(ref probabilityIncrementStep, "probabilityIncrementStep", 10);
			Scribe_Values.Look(ref allowEventWandererJoins, "allowEventWandererJoins", true);
			Scribe_Values.Look(ref allowEventRefugeeChased, "allowEventRefugeeChased", true);
			Scribe_Values.Look(ref allowEventPrisonerRescue, "allowEventPrisonerRescue", true);
			Scribe_Values.Look(ref allowEventDownedRefugee, "allowEventDownedRefugee", true);
			base.ExposeData();
		}
	}

	class ReunionMain : Mod
	{
		ReunionSettings _settings;

		// Constructor
		public ReunionMain(ModContentPack content) : base(content)
		{
			var harmony = HarmonyInstance.Create("kyrun.mod.reunion");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			_settings = GetSettings<ReunionSettings>();
		}

		// Mod UI
		public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
		{
			const float GAP_HEIGHT = 12f;

			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(inRect);

			var strMinimumProb = _settings.minimumProbability.ToString();
			listingStandard.TextFieldNumericLabeled("Reunion.MinimumProbability".Translate(), ref _settings.minimumProbability, ref strMinimumProb, 0, 100);
			_settings.minimumProbability = UnityEngine.Mathf.RoundToInt(listingStandard.Slider(_settings.minimumProbability, 0, 100));

			listingStandard.Gap(GAP_HEIGHT);

			var strProbIncrStep = _settings.probabilityIncrementStep.ToString();
			listingStandard.TextFieldNumericLabeled("Reunion.ProbabilityIncrementStep".Translate(), ref _settings.probabilityIncrementStep, ref strProbIncrStep, 0, 100);
			_settings.probabilityIncrementStep = UnityEngine.Mathf.RoundToInt(listingStandard.Slider(_settings.probabilityIncrementStep, 0, 100));

			listingStandard.Gap(GAP_HEIGHT);

			listingStandard.CheckboxLabeled("Reunion.AllowEventWandererJoins".Translate(), ref _settings.allowEventWandererJoins);
			listingStandard.CheckboxLabeled("Reunion.AllowEventRefugeeChased".Translate(), ref _settings.allowEventRefugeeChased);
			listingStandard.CheckboxLabeled("Reunion.AllowEventPrisonerRescue".Translate(), ref _settings.allowEventPrisonerRescue);
			listingStandard.CheckboxLabeled("Reunion.AllowEventDownedRefugee".Translate(), ref _settings.allowEventDownedRefugee);

			listingStandard.End();

			base.DoSettingsWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "Reunion";
		}
	}

	class Reunion : GameComponent
	{
		// Track all pawns
		public static List<Pawn> ListAlly = new List<Pawn>();

		// Probabilities
		static int _eventProbability = -1;

		// Trait-related
		public const string TRAIT_DEF_CHARACTER = "ReunionCharacter";
		public const string TRAIT_ALLY = "Ally";
		public static TraitDef TraitDef_Character { get; private set; }
		public static Trait Trait_Ally { get; private set; }

		// Save key
		const string SAVE_KEY = "ReunionProbability";

		public static ReunionSettings Settings { get; private set; }

		public Reunion(Game game) : base()
		{
			Settings = LoadedModManager.GetMod<ReunionMain>().GetSettings<ReunionSettings>();
		}

		// Initialise
		public static void Init()
		{
			TraitDef_Character = TraitDef.Named(TRAIT_DEF_CHARACTER);
			Trait_Ally = new Trait(TraitDef_Character, 3, true);

			Scribe_Values.Look(ref _eventProbability, SAVE_KEY);
			if (_eventProbability < 0)
			{
				_eventProbability = Settings.minimumProbability;
#if TESTING
				for (int i = 0; i < 5; ++i)
				{
					var newPawn = PawnGenerator.GeneratePawn(PawnKindDef.Named("SpaceRefugee"));
					newPawn.story.traits.GainTrait(Trait_Ally);
					Current.Game.World.worldPawns.PassToWorld(newPawn);
					Log.Message("Generate Testing Pawn: " + newPawn.Name);
				}
#endif
			}
			Log.Message("Reunion Event Probability: " + _eventProbability);

			ListAlly.Clear(); // clear the list (a reload might have a populated list)
			foreach (var pawn in Current.Game.World.worldPawns.AllPawnsAlive)
			{
				if (pawn == null || pawn.story == null || pawn.story.traits == null) continue;

				var traits = pawn.story.traits;
				if (traits.HasTrait(TraitDef_Character))
				{
					var trait = traits.GetTrait(TraitDef_Character);
					if (trait.Label.Contains(TRAIT_ALLY))
					{
						ListAlly.Add(pawn);
						Log.Message("Found Ally: " + pawn.Name);
					}
				}
			}
			Log.Message("Reunion is Ready");
		}

		public static bool ShouldSpawnPawn(out Pawn pawn)
		{
			pawn = null;
			if (ListAlly.Count <= 0)
			{
				Log.Message("No more to spawn!");
				return false; // no more form list, don't need to check
			}

#if TESTING
			//_eventProbability = 100;
#endif

			var roll = Random.Range(0, 100);
			if (roll < _eventProbability) // it's happening!
			{
				var oldProb = _eventProbability;
				_eventProbability = Settings.minimumProbability;
				Log.Message("Roll success!: " + roll + " vs " + oldProb + ", probability reset to " + _eventProbability);

				var randomIndex = Random.Range(0, ListAlly.Count);
				pawn = ListAlly[randomIndex];
				ListAlly.RemoveAt(randomIndex);

				pawn.SetFactionDirect(null); // remove faction, if any

				var trait = pawn.story.traits.GetTrait(TraitDef_Character);
				if (trait != null)
				{
					pawn.story.traits.allTraits.Remove(trait);
				}

				if (ListAlly.Count == 0)
				{
					Log.Message("All Reunion Pawns are spawned!");
				}
				return true;
			}
			else // not happening, try again later
			{
				var oldProb = _eventProbability;
				_eventProbability += Settings.probabilityIncrementStep;
				_eventProbability = Math.Max(_eventProbability, 100); // cap at 100
				Log.Message("Roll failed: " + roll + " vs " + oldProb + ", probability incremented to " + _eventProbability);
				return false;
			}
		}

		// Save the variables
		public override void ExposeData()
		{
			Scribe_Values.Look(ref _eventProbability, SAVE_KEY, Settings.minimumProbability);

			base.ExposeData();
		}
	}

	// Called on loading/new game
	[HarmonyPatch(typeof(Game), "FinalizeInit")]
	internal static class Verse_Game_FinalizeInit
	{
		static void Postfix() => Reunion.Init();
	}

	// WANDERER JOINS
	[HarmonyPatch(typeof(IncidentWorker_WandererJoin), "TryExecuteWorker")]
	[HarmonyPatch(new Type[] { typeof(IncidentParms) })]
	static class IncidentWorker_WandererJoin_TryExecuteWorker_Patch
	{
		static bool Prefix(IncidentWorker_WandererJoin __instance, ref IncidentParms parms)
		{
			if (!Reunion.Settings.allowEventWandererJoins) return true;

#if TESTING
			Log.Message("[Reunion] Wanderer Joins");
#endif
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				Map map = (Map)parms.target;
				IntVec3 loc;
				if (!TryFindEntryCell(map, out loc))
				{
					return false;
				}

				pawn.SetFaction(Faction.OfPlayer, null);
				GenSpawn.Spawn(pawn, loc, map, WipeMode.Vanish);
				string text = __instance.def.letterText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN");
				string label = __instance.def.letterLabel.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN");
				PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, ref label, pawn);
				Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, pawn, null, null);

				return false;
			}

			return true;
		}

		static bool TryFindEntryCell(Map map, out IntVec3 cell) // copied function
		{
			return CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) => map.reachability.CanReachColony(c) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out cell);
		}
	}


	// REFUGEE CHASED
	// Known Issue: Using PawnGroupKindDefOf generates this warning:
	// Tried to use an uninitialized DefOf of type PawnGroupKindDefOf. DefOfs are initialized right after all defs all loaded.
	// Uninitialized DefOfs will return only nulls. (hint: don't use DefOfs as default field values in Defs, try to resolve them in ResolveReferences() instead)
	[HarmonyPatch(typeof(IncidentWorker_RefugeeChased), "TryExecuteWorker")]
	[HarmonyPatch(new Type[] { typeof(IncidentParms) })]
	static class IncidentWorker_RefugeeChased_TryExecuteWorker_Patch
	{
		private static readonly IntRange RaidDelay = new IntRange(1000, 4000);

		private static readonly FloatRange RaidPointsFactorRange = new FloatRange(1f, 1.6f);

		static bool Prefix(IncidentWorker_RefugeeChased __instance, ref IncidentParms parms)
		{
			if (!Reunion.Settings.allowEventRefugeeChased) return true;

#if TESTING
			Log.Message("[Reunion] Refugee Chased");
#endif
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				Map map = (Map)parms.target;
				IntVec3 spawnSpot;

				var TryFindSpawnSpot = __instance.GetType().GetMethod("TryFindSpawnSpot", BindingFlags.NonPublic | BindingFlags.Instance);
				object[] parameters = new object[] { map, null };
				bool result = (bool)TryFindSpawnSpot.Invoke(__instance, parameters);
				if (result)
				{
					spawnSpot = (IntVec3)parameters[1];
				}
				else
				{
					return false;
				}

				var TryFindEnemyFaction = __instance.GetType().GetMethod("TryFindEnemyFaction", BindingFlags.NonPublic | BindingFlags.Instance);
				parameters = new object[] { null };
				result = (bool)TryFindEnemyFaction.Invoke(__instance, parameters);
				Faction faction;
				if (result)
				{
					faction = (Faction)parameters[0];
				}
				else
				{
					return false;
				}

				int @int = Rand.Int;
				IncidentParms raidParms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
				raidParms.forced = true;
				raidParms.faction = faction;
				raidParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
				raidParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
				raidParms.spawnCenter = spawnSpot;
				raidParms.points = UnityEngine.Mathf.Max(raidParms.points * RaidPointsFactorRange.RandomInRange, faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));
				raidParms.pawnGroupMakerSeed = new int?(@int);
				PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat, raidParms, false);
				defaultPawnGroupMakerParms.points = IncidentWorker_Raid.AdjustedRaidPoints(defaultPawnGroupMakerParms.points, raidParms.raidArrivalMode, raidParms.raidStrategy, defaultPawnGroupMakerParms.faction, PawnGroupKindDefOf.Combat);
				IEnumerable<PawnKindDef> pawnKinds = PawnGroupMakerUtility.GeneratePawnKindsExample(defaultPawnGroupMakerParms);

				Pawn refugee = pawn; // EDIT
				refugee.relations.everSeenByPlayer = true;

				string text = "RefugeeChasedInitial".Translate(refugee.Name.ToStringFull, refugee.story.Title, faction.def.pawnsPlural, faction.Name, refugee.ageTracker.AgeBiologicalYears, PawnUtility.PawnKindsToCommaList(pawnKinds, true), refugee.Named("PAWN"));
				text = text.AdjustedFor(refugee, "PAWN");
				PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, refugee);
				DiaNode diaNode = new DiaNode(text);
				DiaOption diaOption = new DiaOption("RefugeeChasedInitial_Accept".Translate());
				diaOption.action = delegate
				{
					GenSpawn.Spawn(refugee, spawnSpot, map, WipeMode.Vanish);
					refugee.SetFaction(Faction.OfPlayer, null);
					CameraJumper.TryJump(refugee);
					QueuedIncident qi = new QueuedIncident(new FiringIncident(IncidentDefOf.RaidEnemy, null, raidParms), Find.TickManager.TicksGame + RaidDelay.RandomInRange, 0);
					Find.Storyteller.incidentQueue.Add(qi);
				};
				diaOption.resolveTree = true;
				diaNode.options.Add(diaOption);
				string text2 = "RefugeeChasedRejected".Translate(refugee.LabelShort, refugee);
				DiaNode diaNode2 = new DiaNode(text2);
				DiaOption diaOption2 = new DiaOption("OK".Translate());
				diaOption2.resolveTree = true;
				diaNode2.options.Add(diaOption2);
				DiaOption diaOption3 = new DiaOption("RefugeeChasedInitial_Reject".Translate());
				diaOption3.action = delegate
				{
					// EDIT: Skip this step because the pawn is already in WorldPawns
					// Find.WorldPawns.PassToWorld(refugee, RimWorld.Planet.PawnDiscardDecideMode.Decide);
				};
				diaOption3.link = diaNode2;
				diaNode.options.Add(diaOption3);
				string title = "RefugeeChasedTitle".Translate(map.Parent.Label);
				Find.WindowStack.Add(new Dialog_NodeTreeWithFactionInfo(diaNode, faction, true, true, title));
				Find.Archive.Add(new ArchivedDialog(diaNode.text, title, faction));

				return false;
			}
			return true;
		}
	}

	// PRISONER RESCUE
	[HarmonyPatch(typeof(PrisonerWillingToJoinQuestUtility), "GeneratePrisoner")]
	[HarmonyPatch(new Type[] { typeof(int), typeof(Faction) })]
	static class PrisonerWillingToJoinQuestUtility_GeneratePrisoner_Patch
	{
		static bool Prefix(ref int tile, ref Faction hostFaction, ref Pawn __result)
		{
			if (!Reunion.Settings.allowEventPrisonerRescue) return true;

#if TESTING
			Log.Message("[Reunion] Prisoner Rescue");
#endif
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				pawn.guest.SetGuestStatus(hostFaction, true);
				__result = pawn;
				return false;
			}

			return true;
		}
	}


	// DOWNED REFUGEE
	[HarmonyPatch(typeof(DownedRefugeeQuestUtility), "GenerateRefugee")]
	[HarmonyPatch(new Type[] { typeof(int) })]
	static class DownedRefugeeQuestUtility_GenerateRefugee_Patch
	{
		static bool Prefix(ref int tile, ref Pawn __result)
		{
			if (!Reunion.Settings.allowEventDownedRefugee) return true;

#if TESTING
			Log.Message("[Reunion] Downed Refugee");
#endif
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				HealthUtility.DamageUntilDowned(pawn, false);
				HealthUtility.DamageLegsUntilIncapableOfMoving(pawn, false);
				__result = pawn;
				return false;
			}

			return true;
		}
	}

	/* Disabled for now, not sure how to make it spawn as a friendly
	// REFUGEE POD
	[HarmonyPatch(typeof(ThingSetMaker_RefugeePod), "Generate")]
	[HarmonyPatch(new Type[] { typeof(ThingSetMakerParams), typeof(List<Thing>) })]
	static class ThingSetMaker_RefugeePod_Generate_Patch
	{
		static bool Prefix(ThingSetMaker_RefugeePod __instance, ref ThingSetMakerParams parms, ref List<Thing> outThings)
		{
			Log.Message("Refugee Pod Reunion");
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				outThings.Add(pawn);
				HealthUtility.DamageUntilDowned(pawn, true);
				return false;
			}
			return true;
		}
	}
	*/

	// DEBUG MENU ACTION
	[HarmonyPatch(typeof(Dialog_DebugActionsMenu), "DoListingItems_AllModePlayActions")]
	[HarmonyPatch(new Type[] { })]
	static class Dialog_DebugActionsMenu_DoListingItems_AllModePlayActions_Patch
	{
		static void Postfix(Dialog_DebugActionsMenu __instance)
		{
			MethodInfo methodDoLabel = __instance.GetType().GetMethod("DoLabel", BindingFlags.NonPublic | BindingFlags.Instance);
			methodDoLabel.Invoke(__instance, new object[] { "Mod - Reunion (by Kyrun)" });

			Action action = delegate
			{
				List<DebugMenuOption> list = new List<DebugMenuOption>();
				Action<Pawn> act = delegate (Pawn p)
				{
					if (p != null && p.story != null && !p.story.traits.HasTrait(Reunion.TraitDef_Character))
					{
						p.story.traits.GainTrait(Reunion.Trait_Ally);
						Reunion.ListAlly.Add(p);
						var trait = p.story.traits.GetTrait(Reunion.TraitDef_Character);
						if (trait.Label == Reunion.Trait_Ally.Label)
						{
							Log.Message(p.Name + " gains the trait \"" + Reunion.Trait_Ally.Label + "\".");
						}
					}
				};
				foreach (Pawn current in Find.WorldPawns.AllPawnsAlive)
				{
					Pawn pLocal = current;
					if (current != null && current.story != null &&
						!current.story.traits.HasTrait(Reunion.TraitDef_Character)) // don't list those already with the trait
					{
						list.Add(new DebugMenuOption(current.LabelShort, DebugMenuOptionMode.Action, delegate
						{
							act(pLocal);
						}));
					}
				}
				Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
			};
			MethodInfo methodDebugAction = __instance.GetType().GetMethod("DebugAction", BindingFlags.NonPublic | BindingFlags.Instance);
			methodDebugAction.Invoke(__instance, new object[] { "Make world pawn \"Ally\"...", action });
		}
	}
}
