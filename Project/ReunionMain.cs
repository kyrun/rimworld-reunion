//#define TESTING
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

namespace Kyrun
{
	public class ReunionSettings : ModSettings
	{
		public enum Event
		{
			WandererJoins,
			//RefugeeChased,
			PrisonerRescue,
			DownedRefugee,
			RefugeePodCrash,
		};

		public int minimumProbability = 10;
		public int probabilityIncrementStep = 10;

		// Toggle events
		public Dictionary<Event, bool> EventAllow = new Dictionary<Event, bool>()
		{
			{ Event.WandererJoins, true },
			//{ Event.RefugeeChased, true },
			{ Event.PrisonerRescue, true },
			{ Event.DownedRefugee, true },
			{ Event.RefugeePodCrash, false },
		};

		// Save Mod Settings
		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref minimumProbability, "minimumProbability", 10, true);
			Scribe_Values.Look(ref probabilityIncrementStep, "probabilityIncrementStep", 10, true);

			foreach (Event evtType in Enum.GetValues(typeof(Event)))
			{
				bool allowEvent = EventAllow[evtType];
				var saveKey = CreateSaveKey(evtType);
				Scribe_Values.Look(ref allowEvent, saveKey, allowEvent, true);
				EventAllow[evtType] = allowEvent;
			}
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
			var harmony = new Harmony("kyrun.mod.reunion");
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
		public static List<Pawn> ListAllyAvailable = new List<Pawn>();
		public static List<string> ListAllySpawned = new List<string>();

		// Probabilities
		static int _eventProbability = -1;

		// Trait-related
		public const string DELIMITER = ", ";
		public const string TRAIT_DEF_CHARACTER = "ReunionCharacter";
		public const string TRAIT_ALLY = "Ally";
		public const int TRAIT_DEGREE_ALLY = 3;
		public static TraitDef TraitDef_Character { get; private set; }
		public static Trait Trait_Ally { get; private set; }

		// Save key
		const string SAVE_KEY_PROBABILITY = "Reunion_Probability";
		const string SAVE_KEY_LIST_ALLY_SPAWNED = "Reunion_AllySpawned";
		const string SAVE_KEY_LIST_ALLY_AVAILABLE = "Reunion_AllyAvailable";

		public static ReunionSettings Settings { get; private set; }


		public Reunion(Game game) : base()
		{
			Settings = LoadedModManager.GetMod<ReunionMod>().GetSettings<ReunionSettings>();
		}


		public static void PreInit()
		{
			TraitDef_Character = TraitDef.Named(TRAIT_DEF_CHARACTER);
			Trait_Ally = new Trait(TraitDef_Character, TRAIT_DEGREE_ALLY, true);

			Msg("Reunion Event Probability: " + _eventProbability);
		}


		public static void InitOnNewGame()
		{
			ListAllyAvailable.Clear();
			ListAllySpawned.Clear();
#if TESTING
			/* */
			const int TOTAL = 5;
			for (int i = 0; i < TOTAL; ++i)
			{
				var pgr = new PawnGenerationRequest(PawnKindDef.Named("SpaceRefugee"), null,
					PawnGenerationContext.NonPlayer, -1, true);
				var newPawn = PawnGenerator.GeneratePawn(pgr);
				newPawn.Name = NameTriple.FromString("ReunionPawn" + i);
				ListAllyAvailable.Add(newPawn);
			}
			/* */
#endif
		}


		public static void InitOnLoad()
		{
			if (ListAllyAvailable == null) ListAllyAvailable = new List<Pawn>();
			if (ListAllySpawned == null) ListAllySpawned = new List<string>();
		}


		public static void PostInit()
		{
			// Check player's existing colonists with traits and put into list for saving.
			// This means that "Prepare Carefully" starting colonists with the Ally trait will be "found" again
			// if they are somehow lost to the World pool.
			IdentifyReunionPawnsFromList(PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction, (pawn) =>
			{
				if (!ListAllySpawned.Contains(pawn.GetUniqueLoadID()))
				{
					ListAllySpawned.Add(pawn.GetUniqueLoadID());
					Msg("Saving Player's pawn with Ally trait to Reunion list: " + pawn.Name);
				}
			}, TRAIT_ALLY);

			// Check all World Pawns with trait and put into list for saving. Also remove trait.
			// Use case 1: New game with "Prepare Carefully" creates World pawns.
			// Use case 2: Backwards compatibility for loading existing saves from older version of this mod.
			IdentifyReunionPawnsFromList(Current.Game.World.worldPawns.AllPawnsAlive, (pawn) =>
			{
				if (!ListAllyAvailable.Contains(pawn))
				{
					ListAllyAvailable.Add(pawn);
					Msg("Saving World pawn with Ally trait to Reunion list: " + pawn.Name);
				}
				Find.WorldPawns.RemovePawn(pawn);
			}, TRAIT_ALLY);

			if (_eventProbability < 0) _eventProbability = Settings.minimumProbability;
#if TESTING
			PrintAllyList();
#endif
		}

		static void IdentifyReunionPawnsFromList(List<Pawn> listPawns, Action<Pawn> doToPawn, string traitKey)
		{
			foreach (var pawn in listPawns)
			{
				if (pawn == null || pawn.story == null || pawn.story.traits == null) continue;

				var traits = pawn.story.traits;
				if (traits.HasTrait(TraitDef_Character))
				{
					var trait = traits.GetTrait(TraitDef_Character);
					if (trait != null && trait.Label.Contains(traitKey))
					{
						doToPawn?.Invoke(pawn);
						TryRemoveTrait(pawn);
					}
				}
			}
		}


		public static bool ShouldSpawnPawn(out Pawn pawn)
		{
			pawn = null;

			if (ListAllyAvailable.Count <= 0)
			{
				Msg("No more to spawn!");
				return false; // no more form list, don't need to check
			}

#if TESTING
			var roll = 0; // always happens
#else
			var roll = Random.Range(0, 100);
#endif

			if (roll < _eventProbability) // it's happening!
			{
				var oldProb = _eventProbability;
				_eventProbability = Settings.minimumProbability;

				var randomIndex = Random.Range(0, ListAllyAvailable.Count);
				pawn = ListAllyAvailable[randomIndex];
				SetupSpawn(pawn, ListAllyAvailable, ListAllySpawned);

				Msg("Roll success!: " + roll + " vs " + oldProb + ", probability reset to " + _eventProbability + ". Pawn chosen: " + pawn.Name);

				return true;
			}
			else // not happening, try again later
			{
				var oldProb = _eventProbability;
				_eventProbability += Settings.probabilityIncrementStep;
				_eventProbability = Math.Min(_eventProbability, 100); // cap at 100
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
				return true;
			}

			return false;
		}


		public static void SetupSpawn(Pawn pawn, List<Pawn> listAvailable, List<string> listJoined)
		{
			listAvailable.Remove(pawn);
			listJoined.Add(pawn.GetUniqueLoadID());
			pawn.SetFactionDirect(null); // remove faction, if any
			pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
		}


		public static void ReturnToAvailable(Pawn pawn, List<string> listJoined, List<Pawn> listAvailable)
		{
			listJoined.Remove(pawn.GetUniqueLoadID());
			listAvailable.Add(pawn);
			Msg(pawn.Name + " was lost by the player and made available for Reunion to spawn again.");
		}


		public override void ExposeData()
		{
			Scribe_Values.Look(ref _eventProbability, SAVE_KEY_PROBABILITY, Settings.minimumProbability);
			Scribe_Collections.Look(ref ListAllyAvailable, SAVE_KEY_LIST_ALLY_AVAILABLE, LookMode.Deep);
			Scribe_Collections.Look(ref ListAllySpawned, SAVE_KEY_LIST_ALLY_SPAWNED, LookMode.Value);

			base.ExposeData();
		}

		public static void PrintAllyList()
		{
			var str = "";
			foreach (var ally in ListAllyAvailable)
			{
				str += ally.Name + DELIMITER;
			}
			if (str != "") str = str.Substring(0, str.Length - DELIMITER.Length); // truncate last delimiter
			Msg("Ally Pawns: " + str);
		}

		public static void Msg(object o)
		{
			Log.Message("[Reunion] " + o);
		}

		public static void Warn(object o)
		{
			Log.Warning("[Reunion] " + o);
		}
	}


	// Called on new game
	[HarmonyPatch(typeof(Game), "InitNewGame")]
	internal static class Verse_Game_InitNewGame
	{
		static void Postfix()
		{
			Reunion.PreInit();
			Reunion.InitOnNewGame();
			Reunion.PostInit();
		}
	}


	// Called on load game
	[HarmonyPatch(typeof(Game), "LoadGame")]
	internal static class Verse_Game_LoadGame
	{
		static void Postfix()
		{
			Reunion.PreInit();
			Reunion.InitOnLoad();
			Reunion.PostInit();
		}
	}


	[HarmonyPatch(typeof(RimWorld.Planet.WorldPawns), "PassToWorld")]
	[HarmonyPatch(new Type[] { typeof(Pawn), typeof(RimWorld.Planet.PawnDiscardDecideMode) })]
	static class WorldPawns_PassToWorld
	{
		static bool Prefix(RimWorld.Planet.WorldPawns __instance, ref Pawn pawn, ref RimWorld.Planet.PawnDiscardDecideMode discardMode)
		{
#if TESTING
			if (Reunion.ListAllySpawned.Contains(pawn.GetUniqueLoadID()))
			{
				Reunion.Msg("Passed to world: " + pawn.Name);
			}
#endif
			if (Current.Game.Info.RealPlayTimeInteracting > 0 && // prevent this from firing when the game hasn't even started proper
				!pawn.Destroyed && // ignore pawns destroyed for whatever reason
				!KidnapUtility.IsKidnapped(pawn) && // don't make kidnapped pawns available; vanilla handles that naturally
				!PawnsFinder.AllCaravansAndTravelingTransportPods_Alive.Contains(pawn) && // ignore caravan/pods
				Reunion.ListAllySpawned.Contains(pawn.GetUniqueLoadID()))
			{
				if (PawnComponentsUtility.HasSpawnedComponents(pawn))
				{
					PawnComponentsUtility.RemoveComponentsOnDespawned(pawn);
				}
				Reunion.ReturnToAvailable(pawn, Reunion.ListAllySpawned, Reunion.ListAllyAvailable);
				return false;
			}
			return true;
		}
	}


	/* for testing purposes * /
	[HarmonyPatch(typeof(StorytellerComp), "IncidentChanceFinal")]
	[HarmonyPatch(new Type[] { typeof(IncidentDef) })]
	static class StorytellerComp_IncidentChanceFinal
	{
		static void Postfix(ref float __result, ref IncidentWorker __instance, ref IncidentDef def)
		{
			if (Reunion.ListAllyAvailable.Count > 0)
			{
				if ((Reunion.Settings.EventAllow[ReunionSettings.Event.WandererJoins] && def.defName == "WandererJoin") ||
					(Reunion.Settings.EventAllow[ReunionSettings.Event.RefugeePodCrash] && def.defName == "RefugeePodCrash"))
				{
					__result = 10000.0f;
				}
			}
		}
	}


	[HarmonyPatch(typeof(StorytellerUtilityPopulation), "PopulationIntentForQuest", MethodType.Getter)]
	static class StorytellerUtilityPopulation_PopulationIntentForQuest
	{
		static bool Prefix(ref float __result)
		{
			if (Reunion.ListAllyAvailable.Count > 0)
			{
				__result = 10000.0f;
				return false;
			}
			return true;
		}
	}
	/* */


	[HarmonyPatch]
	public class ReversePatch
	{
		[HarmonyReversePatch]
		[HarmonyPatch(typeof(IncidentWorker), "SendStandardLetter")]
		[HarmonyPatch(new Type[] { typeof(TaggedString), typeof(TaggedString), typeof(LetterDef),
			typeof(IncidentParms), typeof(LookTargets), typeof(NamedArgument[]) })]
		public static void SendStandardLetter(object instance, TaggedString baseLetterLabel, TaggedString baseLetterText, LetterDef baseLetterDef,
			IncidentParms parms, LookTargets lookTargets, params NamedArgument[] textArgs)
		{ }
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
				pawn.SetFactionDirect(Faction.OfPlayer);
				GenSpawn.Spawn(pawn, loc, map, WipeMode.Vanish);

				TaggedString baseLetterText = __instance.def.letterText.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
				TaggedString baseLetterLabel = __instance.def.letterLabel.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
				PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref baseLetterText, ref baseLetterLabel, pawn);
				ReversePatch.SendStandardLetter(__instance, baseLetterLabel, baseLetterText, LetterDefOf.PositiveEvent, parms, pawn, Array.Empty<NamedArgument>());

				return false;
			}

			return true;
		}

		static bool TryFindEntryCell(Map map, out IntVec3 cell) // copied function
		{
			return CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) => map.reachability.CanReachColony(c) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out cell);
		}
	}


	// TRANSPORT POD CRASH ------------------------------------------------------------------------
	[HarmonyPatch(typeof(IncidentWorker_TransportPodCrash), "TryExecuteWorker")]
	[HarmonyPatch(new Type[] { typeof(IncidentParms) })]
	static class IncidentWorker_TransportPodCrash_Generate_Patch
	{
		static bool Prefix(IncidentWorker_TransportPodCrash __instance, ref IncidentParms parms)
		{
			if (!Reunion.Settings.EventAllow[ReunionSettings.Event.RefugeePodCrash]) return true;

#if TESTING
			Reunion.Msg("Refugee Pod Crash");
#endif
			if (Reunion.ShouldSpawnPawn(out Pawn pawn))
			{
				pawn.mindState.WillJoinColonyIfRescued = true; // still doesn't 100% rescue :(
				HealthUtility.DamageUntilDowned(pawn, true);

				Map map = (Map)parms.target;
				List<Thing> things = new List<Thing>();
				things.Add(pawn);
				IntVec3 intVec = DropCellFinder.RandomDropSpot(map);
				pawn.guest.getRescuedThoughtOnUndownedBecauseOfPlayer = true;
				TaggedString baseLetterLabel = "LetterLabelRefugeePodCrash".Translate();
				TaggedString taggedString = "RefugeePodCrash".Translate(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
				taggedString += "\n\n";
				if (pawn.Faction == null)
				{
					taggedString += "RefugeePodCrash_Factionless".Translate(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
				}
				else if (pawn.Faction.HostileTo(Faction.OfPlayer))
				{
					taggedString += "RefugeePodCrash_Hostile".Translate(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
				}
				else
				{
					taggedString += "RefugeePodCrash_NonHostile".Translate(pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
				}
				PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref taggedString, ref baseLetterLabel, pawn);
				ReversePatch.SendStandardLetter(__instance, baseLetterLabel, taggedString, LetterDefOf.NeutralEvent, parms, new TargetInfo(intVec, map, false), Array.Empty<NamedArgument>());
				ActiveDropPodInfo activeDropPodInfo = new ActiveDropPodInfo();
				activeDropPodInfo.innerContainer.TryAddRangeOrTransfer(things, true, false);
				activeDropPodInfo.openDelay = 180;
				activeDropPodInfo.leaveSlag = true;
				DropPodUtility.MakeDropPodAt(intVec, map, activeDropPodInfo);

				return false;
			}

			return true;
		}
	}


	/* Refugee Chased WIP
	[HarmonyPatch(typeof(QuestNode_GeneratePawn), "RunInt")]
	static class QuestNode_GeneratePawn_RunInt_Patch
	{
		static bool Prefix(QuestNode_GeneratePawn __instance)
		{
			if (Reunion.Settings.EventAllow[ReunionSettings.Event.RefugeeChased] &&
				QuestGen.Root.defName == "ThreatReward_Raid_Joiner")
			{
				if (Reunion.ShouldSpawnPawn(out Pawn pawn))
				{
					Slate slate = QuestGen.slate;

					if (__instance.storeAs.GetValue(slate) != null)
					{
						QuestGen.slate.Set<Pawn>(__instance.storeAs.GetValue(slate), pawn, false);
					}
					if (__instance.addToList.GetValue(slate) != null)
					{
						QuestGenUtility.AddToOrMakeList(QuestGen.slate, __instance.addToList.GetValue(slate), pawn);
					}
					QuestGen.AddToGeneratedPawns(pawn);

					// Vanilla code: adds the pawn to the World.
					// For this mod, remove them from the available list and put them in the spawned list instead.

					return false;
				}

				return false;
			}

			return true;
		}
	}


	[HarmonyPatch(typeof(Quest), "CleanupQuestParts")]
	static class Quest_CleanupQuestParts
	{
		static void Postfix(Quest __instance)
		{
			if (__instance.root.defName == "ThreatReward_Raid_Joiner" && __instance.State == QuestState.EndedOfferExpired)
			{
				// get the QuestPart_PawnsArrive part
				var questPartPawnsArrive = __instance.PartsListForReading.Find((part) =>
				{
					return part is QuestPart_PawnsArrive;
				}) as QuestPart_PawnsArrive;

				if (questPartPawnsArrive != null && questPartPawnsArrive.pawns != null && questPartPawnsArrive.pawns.Count > 0)
				{
					// get pawns that spawned from Reunion
					var listToReturn = questPartPawnsArrive.pawns.FindAll((pawn) =>
					{
						return Reunion.ListAllySpawned.Contains(pawn.GetUniqueLoadID());
					});

					foreach (var pawn in listToReturn)
					{
						Reunion.ReturnToAvailable(pawn, Reunion.ListAllySpawned, Reunion.ListAllyAvailable);
					}
				}
			}
		}
	}
	*/


	// OPPORTUNITY SITE: PRISONER WILLING TO JOIN -------------------------------------------------
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
				pawn.guest.SetGuestStatus(hostFaction, true);
				__result = pawn;
				return false;
			}

			return true;
		}
	}


	// OPPORTUNITY SITE: DOWNED REFUGEE -----------------------------------------------------------
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
				HealthUtility.DamageUntilDowned(pawn, false);
				HealthUtility.DamageLegsUntilIncapableOfMoving(pawn, false);
				__result = pawn;
				return false;
			}

			return true;
		}
	}

	// DEBUG MENU ACTION --------------------------------------------------------------------------

	[HarmonyPatch(typeof(Dialog_DebugActionsMenu))]
	[HarmonyPatch(new Type[] { })]
	[HarmonyPatch(MethodType.Constructor)]
	static class Dialog_DebugActionsMenu_DoListingItems_Patch
	{
		const string CATEGORY = "Mod - Reunion (by Kyrun)";
		static void Postfix(Dialog_DebugActionsMenu __instance, ref List<Dialog_DebugActionsMenu.DebugActionOption> ___debugActions)
		{
			// *** Add Ally Trait ***
			var debugAddAlly = new Dialog_DebugActionsMenu.DebugActionOption();
			debugAddAlly.actionType = DebugActionType.Action;
			debugAddAlly.label = "Make world pawn \"Ally\"...";
			debugAddAlly.category = CATEGORY;
			debugAddAlly.action = delegate
			{
				List<DebugMenuOption> list = new List<DebugMenuOption>();
				Action<Pawn> actionPawn = delegate (Pawn p)
				{
					if (p != null && p.story != null)
					{
						Reunion.TryRemoveTrait(p);
						Reunion.ListAllyAvailable.Add(p);
						Find.WorldPawns.RemovePawn(p);
						Reunion.Msg(p.Name + " has been removed from the World and added to the Ally list.");
					}
				};

				foreach (Pawn current in Find.WorldPawns.AllPawnsAlive)
				{
					Pawn pLocal = current;
					if (current != null && current.story != null) // don't list those already with the trait
					{
						list.Add(new DebugMenuOption(current.LabelShort, DebugMenuOptionMode.Action, delegate
						{
							actionPawn(pLocal);
						}));
					}
				}

				Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
			};
			___debugActions.Add(debugAddAlly); // add to main list

			// *** Print Ally List ***
			var debugPrintAllyList = new Dialog_DebugActionsMenu.DebugActionOption();
			debugPrintAllyList.actionType = DebugActionType.Action;
			debugPrintAllyList.label = "Print \"Ally\" list";
			debugPrintAllyList.category = CATEGORY;
			debugPrintAllyList.action = delegate
			{
				if (Reunion.ListAllyAvailable.Count > 0)
				{
					Reunion.PrintAllyList();
				}
				else
				{
					Reunion.Msg("There are no allies in the Ally list!");
				}
			};
			___debugActions.Add(debugPrintAllyList); // add to main list
		}
	}
}
