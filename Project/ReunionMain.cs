#define TESTING
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Harmony;
using Random = UnityEngine.Random;

namespace Kyrun
{
	public class ReunionSettings : ModSettings
	{
		public enum Event
		{
			WandererJoins,
			RefugeePodCrash,
			RefugeeChased,
			PrisonerRescue,
			DownedRefugee
		};

		public int minimumProbability = 10;
		public int probabilityIncrementStep = 10;

		// Toggle events
		public Dictionary<Event, bool> EventAllow = new Dictionary<Event, bool>()
		{
			{ Event.WandererJoins, true },
			{ Event.RefugeePodCrash, true },
			{ Event.RefugeeChased, true },
			{ Event.PrisonerRescue, true },
			{ Event.DownedRefugee, true },
		};

		// Save Mod Settings
		public override void ExposeData()
		{
			Scribe_Values.Look(ref minimumProbability, "minimumProbability", 10);
			Scribe_Values.Look(ref probabilityIncrementStep, "probabilityIncrementStep", 10);

			foreach (Event evtType in Enum.GetValues(typeof(Event)))
			{
				bool allowEvent = EventAllow[evtType];
				var saveKey = CreateSaveKey(evtType);
				Scribe_Values.Look(ref allowEvent, saveKey, true);
				EventAllow[evtType] = allowEvent;
			}

			base.ExposeData();
		}

		public static string CreateSaveKey(Event eventType)
		{
			return "allowEvent" + eventType;
		}

		public static string CreateTranslationKey(Event eventType)
		{
			return "Reunion.AllowEvent" + eventType;
		}
	}

	[StaticConstructorOnStartup]
	class Main
	{
		static Main()
		{
			var harmony = HarmonyInstance.Create("kyrun.mod.reunion");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}
	}

	class ReunionMod : Mod
	{
		ReunionSettings _settings;

		// Constructor
		public ReunionMod(ModContentPack content) : base(content)
		{
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

			foreach (ReunionSettings.Event evtType in Enum.GetValues(typeof(ReunionSettings.Event)))
			{
				var allow = _settings.EventAllow[evtType];
				listingStandard.CheckboxLabeled(ReunionSettings.CreateTranslationKey(evtType).Translate(), ref allow);
				_settings.EventAllow[evtType] = allow;
			}

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
			Settings = LoadedModManager.GetMod<ReunionMod>().GetSettings<ReunionSettings>();
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
					var pgr = new PawnGenerationRequest(PawnKindDef.Named("SpaceRefugee"), null,
						PawnGenerationContext.NonPlayer, -1, true);
					var newPawn = PawnGenerator.GeneratePawn(pgr);
					newPawn.story.traits.GainTrait(Trait_Ally);
					newPawn.Name = NameTriple.FromString("ReunionPawn" + i);
					Current.Game.World.worldPawns.PassToWorld(newPawn);
					Msg("generated " + newPawn.Name);
				}
#endif
			}
			Msg("Reunion Event Probability: " + _eventProbability);

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
						Msg("Found Ally: " + pawn.Name);
					}
				}
			}
			Msg("Reunion is Ready");
		}

		public static bool ShouldSpawnPawn(out Pawn pawn)
		{
			pawn = null;
			if (ListAlly.Count <= 0)
			{
				Msg("No more to spawn!");
				return false; // no more form list, don't need to check
			}

#if TESTING
			_eventProbability = 100;
#endif

			var roll = Random.Range(0, 100);
			if (roll < _eventProbability) // it's happening!
			{
				var oldProb = _eventProbability;
				_eventProbability = Settings.minimumProbability;

				var randomIndex = Random.Range(0, ListAlly.Count);
				pawn = ListAlly[randomIndex];
				pawn.SetFactionDirect(null); // remove faction, if any

				Msg("Roll success!: " + roll + " vs " + oldProb + ", probability reset to " + _eventProbability + ". Pawn chosen: " + pawn.Name);

				return true;
			}
			else // not happening, try again later
			{
				var oldProb = _eventProbability;
				_eventProbability += Settings.probabilityIncrementStep;
				_eventProbability = Math.Max(_eventProbability, 100); // cap at 100
				Msg("Roll failed: " + roll + " vs " + oldProb + ", probability incremented to " + _eventProbability);
				return false;
			}
		}

		public static bool TryRemoveTrait(Pawn pawn)
		{
			var trait = pawn.story.traits.GetTrait(TraitDef_Character);

			if (trait != null)
			{
				pawn.story.traits.allTraits.Remove(trait);
				Log.Message("REMOVED TRAIT FROM " + pawn.Name);
				return true;
			}

			return false;
		}

		public static bool TryRemoveFromList(Pawn pawn, List<Pawn> list)
		{
			if (list.Contains(pawn))
			{
				list.Remove(pawn);
				if (list.Count == 0)
				{
					Msg("All Pawns have joined!");
				}
#if TESTING
				else
				{
					PrintAllyList();
				}
#endif
				return true;
			}

			return false;
		}

		public static void SanitizePawn(Pawn pawn)
		{
			TryRemoveTrait(pawn);
			TryRemoveFromList(pawn, ListAlly);
		}

		// Save the variables
		public override void ExposeData()
		{
			Scribe_Values.Look(ref _eventProbability, SAVE_KEY, Settings.minimumProbability);

			base.ExposeData();
		}

		public static void PrintAllyList()
		{
			const string DELIMITER = ", ";
			var str = "";
			foreach (var p in ListAlly)
			{
				str += p.Name + DELIMITER;
			}
			if (str != "") str = str.Substring(0, str.Length - DELIMITER.Length); // truncate last delimiter
			Msg("Ally Pawns In World Pool: " + str);
		}

		public static void Msg(string msg)
		{
			Log.Message("[Reunion] " + msg);
		}

		public static void Warn(string msg)
		{
			Log.Warning("[Reunion] " + msg);
		}
	}

	// Called on loading/new game
	[HarmonyPatch(typeof(Game), "FinalizeInit")]
	internal static class Verse_Game_FinalizeInit
	{
		static void Postfix() => Reunion.Init();
	}

	// GET WORLD PAWNS ----------------------------------------------------------------------------
	// Ensure that vanilla game NEVER pulls Reunion pawns for any other reason
	[HarmonyPatch(typeof(RimWorld.Planet.WorldPawns), "GetPawnsBySituation")]
	[HarmonyPatch(new Type[] { typeof(RimWorld.Planet.WorldPawnSituation) })]
	static class WorldPawns_GetPawnsBySituation_Patch
	{
		static void Postfix(ref IEnumerable<Pawn> __result, RimWorld.Planet.WorldPawns __instance, RimWorld.Planet.WorldPawnSituation situation)
		{
			__result = from pawn in __result
					   where (!pawn.RaceProps.Humanlike || // not human
					   pawn.story.traits.GetTrait(Reunion.TraitDef_Character) == null) // human with no Reunion trait
					   select pawn;
		}
	}


	// SPAWN --------------------------------------------------------------------------------------
	// Whenever a Pawn spawns on any map, remove Ally trait and clear from Ally list
	[HarmonyPatch(typeof(GenSpawn), "Spawn")]
	[HarmonyPatch(new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(WipeMode) })]
	static class GenSpawn_Spawn_Patch
	{
		static void Postfix(Thing __result, Thing newThing, IntVec3 loc, Map map, WipeMode wipeMode)
		{
			if (__result is Pawn)
			{
				var pawn = (Pawn)__result;
				if (pawn.RaceProps.Humanlike)
				{
					Reunion.SanitizePawn(pawn);
				}
			}
		}
	}


	// WANDERER JOINS -----------------------------------------------------------------------------
	[HarmonyPatch(typeof(IncidentWorker_WandererJoin), "TryExecuteWorker")]
	[HarmonyPatch(new Type[] { typeof(IncidentParms) })]
	static class IncidentWorker_WandererJoin_TryExecuteWorker_Patch
	{
		static bool Prefix(IncidentWorker_WandererJoin __instance, ref IncidentParms parms)
		{
			if (!Reunion.Settings.EventAllow[ReunionSettings.Event.WandererJoins]) return true;

#if TESTING
			Reunion.Msg("Wanderer Joins");
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


	// REFUGEE POD CRASH --------------------------------------------------------------------------
	[HarmonyPatch(typeof(ThingSetMaker_RefugeePod), "Generate")]
	[HarmonyPatch(new Type[] { typeof(ThingSetMakerParams), typeof(List<Thing>) })]
	static class ThingSetMaker_RefugeePod_Generate_Patch
	{
		static bool Prefix(ThingSetMaker_RefugeePod __instance, ref ThingSetMakerParams parms, ref List<Thing> outThings)
		{
			if (!Reunion.Settings.EventAllow[ReunionSettings.Event.RefugeePodCrash]) return true;

#if TESTING
			Reunion.Msg("Refugee Pod Crash");
#endif
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				outThings.Add(pawn);
				HealthUtility.DamageUntilDowned(pawn, true);
				return false;
			}
			return true;
		}
	}


	// REFUGEE CHASED -----------------------------------------------------------------------------
	[HarmonyPatch(typeof(IncidentWorker_RefugeeChased), "TryExecuteWorker")]
	[HarmonyPatch(new Type[] { typeof(IncidentParms) })]
	static class IncidentWorker_RefugeeChased_TryExecuteWorker_Patch
	{
		private static readonly IntRange RaidDelay = new IntRange(1000, 4000);

		private static readonly FloatRange RaidPointsFactorRange = new FloatRange(1f, 1.6f);

		static bool Prefix(IncidentWorker_RefugeeChased __instance, ref IncidentParms parms)
		{
			if (!Reunion.Settings.EventAllow[ReunionSettings.Event.RefugeeChased]) return true;

#if TESTING
			Reunion.Msg("Refugee Chased");
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

					// Instead, Reunion Pawn stays in the world, with trait unchanged
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

	// PRISONER RESCUE ----------------------------------------------------------------------------
	[HarmonyPatch(typeof(PrisonerWillingToJoinQuestUtility), "GeneratePrisoner")]
	[HarmonyPatch(new Type[] { typeof(int), typeof(Faction) })]
	static class PrisonerWillingToJoinQuestUtility_GeneratePrisoner_Patch
	{
		static bool Prefix(ref int tile, ref Faction hostFaction, ref Pawn __result)
		{
			if (!Reunion.Settings.EventAllow[ReunionSettings.Event.PrisonerRescue]) return true;

#if TESTING
			Reunion.Msg("Prisoner Rescue");
#endif
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				Reunion.TryRemoveFromList(pawn, Reunion.ListAlly);
				pawn.guest.SetGuestStatus(hostFaction, true);
				__result = pawn;
				return false;
			}

			return true;
		}
	}


	// DOWNED REFUGEE -----------------------------------------------------------------------------
	[HarmonyPatch(typeof(DownedRefugeeQuestUtility), "GenerateRefugee")]
	[HarmonyPatch(new Type[] { typeof(int) })]
	static class DownedRefugeeQuestUtility_GenerateRefugee_Patch
	{
		static bool Prefix(ref int tile, ref Pawn __result)
		{
			if (!Reunion.Settings.EventAllow[ReunionSettings.Event.DownedRefugee]) return true;

#if TESTING
			Reunion.Msg("Downed Refugee");
#endif
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				Reunion.TryRemoveFromList(pawn, Reunion.ListAlly);
				HealthUtility.DamageUntilDowned(pawn, false);
				HealthUtility.DamageLegsUntilIncapableOfMoving(pawn, false);
				__result = pawn;
				return false;
			}

			return true;
		}
	}

	// DEBUG MENU ACTION --------------------------------------------------------------------------
	[HarmonyPatch(typeof(Dialog_DebugActionsMenu), "DoListingItems_AllModePlayActions")]
	[HarmonyPatch(new Type[] { })]
	static class Dialog_DebugActionsMenu_DoListingItems_AllModePlayActions_Patch
	{
		static void Postfix(Dialog_DebugActionsMenu __instance)
		{
			MethodInfo methodDoLabel = __instance.GetType().GetMethod("DoLabel", BindingFlags.NonPublic | BindingFlags.Instance);
			methodDoLabel.Invoke(__instance, new object[] { "Mod - Reunion (by Kyrun)" });

			// *** Add Ally Trait ***
			Action actionAddAllyTrait = delegate
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
							Reunion.Msg(p.Name + " gains the trait \"" + Reunion.Trait_Ally.Label + "\".");
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
			MethodInfo methodDebugActionAddAllyTrait = __instance.GetType().GetMethod("DebugAction", BindingFlags.NonPublic | BindingFlags.Instance);
			methodDebugActionAddAllyTrait.Invoke(__instance, new object[] { "Make world pawn \"Ally\"...", actionAddAllyTrait });

			// *** Print Ally List ***
			Action actionPrintAllyList = delegate
			{
				Reunion.PrintAllyList();
			};
			MethodInfo methodDebugActionPrintAllyList = __instance.GetType().GetMethod("DebugAction", BindingFlags.NonPublic | BindingFlags.Instance);
			methodDebugActionPrintAllyList.Invoke(__instance, new object[] { "Print \"Ally\" list", actionPrintAllyList });
		}

		// DEBUG to increase chance of accidental spawn
		[HarmonyPatch(typeof(PawnGenerator), "ChanceToRedressAnyWorldPawn")]
		[HarmonyPatch(new Type[] { typeof(PawnGenerationRequest) })]
		static class PawnGenerator_ChanceToRedressAnyWorldPawn_Patch
		{
			static void Postfix(ref float __result, PawnGenerationRequest request)
			{
				__result = 1f;
			}
		}
	}
}
