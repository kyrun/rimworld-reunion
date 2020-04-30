using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Random = UnityEngine.Random;

namespace Kyrun.Reunion
{
	class GameComponent : Verse.GameComponent
	{
		public static List<Pawn> ListAllyAvailable = new List<Pawn>();
		public static List<string> ListAllySpawned = new List<string>();

		// Variables
		public static int NextEventTick = -1;

		// Trait-related
		public const string TRAIT_DEF_CHARACTER = "ReunionCharacter";
		public const string TRAIT_ALLY = "Ally";
		public const int TRAIT_DEGREE_ALLY = 3;
		public static TraitDef TraitDef_Character { get; private set; }
		public static Trait Trait_Ally { get; private set; }

		// Save key
		const string SAVE_NEXT_EVENT_TICK = "Reunion_NextEventTick";
		const string SAVE_KEY_LIST_ALLY_SPAWNED = "Reunion_AllySpawned";
		const string SAVE_KEY_LIST_ALLY_AVAILABLE = "Reunion_AllyAvailable";

		public static Settings Settings { get; private set; }


		public GameComponent(Game game) : base()
		{
			Settings = LoadedModManager.GetMod<Mod>().GetSettings<Settings>();
		}


		public static void PreInit()
		{
			TraitDef_Character = TraitDef.Named(TRAIT_DEF_CHARACTER);
			Trait_Ally = new Trait(TraitDef_Character, TRAIT_DEGREE_ALLY, true);
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
			var diff = NextEventTick - Find.TickManager.TicksGame;
			if (NextEventTick <= 0) Util.Msg("No events scheduled");
			else Util.Msg("Next event in " + ((float)diff/GenDate.TicksPerDay).ToString("0.00") + " days");
		}


		public static void PostInit()
		{
			// Check player's existing colonists with traits and put into list for saving.
			// This means that "Prepare Carefully" starting colonists with the Ally trait will be "found" again
			// if they are somehow lost to the World pool.
			RegisterReunionPawnsFromList(PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction, (pawn) =>
			{
				if (!ListAllySpawned.Contains(pawn.GetUniqueLoadID()))
				{
					ListAllySpawned.Add(pawn.GetUniqueLoadID());
					Util.Msg("Saving Player's pawn with Ally trait to Reunion list: " + pawn.Name);
				}
			}, TRAIT_ALLY);

			// Check all World Pawns with trait and put into list for saving. Also remove trait.
			// Use case 1: New game with "Prepare Carefully" creates World pawns.
			// Use case 2: Backwards compatibility for loading existing saves from older version of this mod.
			RegisterReunionPawnsFromList(Current.Game.World.worldPawns.AllPawnsAlive, (pawn) =>
			{
				if (!ListAllyAvailable.Contains(pawn))
				{
					ListAllyAvailable.Add(pawn);
					Util.Msg("Saving World pawn with Ally trait to Reunion list: " + pawn.Name);
				}
				Find.WorldPawns.RemovePawn(pawn);
			}, TRAIT_ALLY);

#if TESTING
			Util.PrintAllyList();
#endif
		}


		static void RegisterReunionPawnsFromList(List<Pawn> listPawns, Action<Pawn> doToPawn, string traitKey)
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


		public static Pawn GetRandomAllyForSpawning()
		{
			var randomIndex = Random.Range(0, ListAllyAvailable.Count);
			var pawn = ListAllyAvailable[randomIndex];
			SetupSpawn(pawn, ListAllyAvailable, ListAllySpawned);

			return pawn;
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


		public static void SetupSpawn(Pawn pawn, List<Pawn> listSource, List<string> listDest)
		{
			listSource.Remove(pawn);
			listDest.Add(pawn.GetUniqueLoadID());
			pawn.SetFactionDirect(null); // remove faction, if any
			pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized(); // prevent some error which I don't yet understand
		}


		public static void ReturnToAvailable(Pawn pawn, List<string> listJoined, List<Pawn> listAvailable)
		{
			listJoined.Remove(pawn.GetUniqueLoadID());
			listAvailable.Add(pawn);
			Util.Msg(pawn.Name + " was lost by the player and made available for Reunion to spawn again.");

			// If there is exactly one, it means that previously was paused due to no available pawns.
			// So we schedule a new event.
			if (listAvailable.Count == 1) TryScheduleNextEvent();
		}


		public static void TryScheduleNextEvent(bool forceReschedule = false)
		{
			if (ListAllyAvailable.Count == 0)
			{
				NextEventTick = -1;

				Util.Msg("No available Reunion pawns, Reunion events will not fire from now on.");
				return;
			}

			if (!forceReschedule && NextEventTick > Find.TickManager.TicksGame)
			{
				// another event is already scheduled
#if TESTING
				Util.Warn("Tried to schedule an event but another event has already been scheduled.");
#endif
				return;
			}

			NextEventTick = Find.TickManager.TicksGame +
				Random.Range(Settings.minDaysBetweenEvents * GenDate.TicksPerDay, Settings.maxDaysBetweenEvents * GenDate.TicksPerDay);

#if TESTING
			NextEventTick = Find.TickManager.TicksGame + 1000;
			Util.Msg("Next event happening in " + NextEventTick);
#endif
		}


		public static void DecideAndDoEvent()
		{
			NextEventTick = -1; // stop next event from happening

			if (ListAllyAvailable.Count <= 0) // no more allies
			{
				Util.Warn("No available Ally pawns, event should not have fired!");
				return;
			}
			if (Current.Game.AnyPlayerHomeMap == null) // no map
			{
				Util.Msg("Player does not have any home map, event timer restarted.");
				TryScheduleNextEvent();
				return;
			}

			List<Settings.Event> listAllowedEvents = new List<Settings.Event>();

			// special case where there's only one colonist
			if (PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction.Count <= 1)
			{
				if (Settings.EventAllow[Settings.Event.WandererJoins]) listAllowedEvents.Add(Settings.Event.WandererJoins);
				if (Settings.EventAllow[Settings.Event.RefugeePodCrash]) listAllowedEvents.Add(Settings.Event.RefugeePodCrash);
			}

			// add the rest of the allowed events if more than 1 Colonist
			if (PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction.Count > 1)
			{
				foreach (Settings.Event eventType in Enum.GetValues(typeof(Settings.Event)))
				{
					if (Settings.EventAllow[eventType] && !listAllowedEvents.Contains(eventType))
					{
						listAllowedEvents.Add(eventType);
					}
				}
			}

			if (listAllowedEvents.Count > 0)
			{
				var eventToDo = listAllowedEvents[Random.Range(0, listAllowedEvents.Count)];
				Settings.EventAction[eventToDo].Invoke();
			}
			else
			{
				Util.Warn("No suitable event found, event timer restarted.");
				TryScheduleNextEvent();
			}
		}


		public override void ExposeData()
		{
			Scribe_Values.Look(ref NextEventTick, SAVE_NEXT_EVENT_TICK, -1);
			Scribe_Collections.Look(ref ListAllyAvailable, SAVE_KEY_LIST_ALLY_AVAILABLE, LookMode.Deep);
			Scribe_Collections.Look(ref ListAllySpawned, SAVE_KEY_LIST_ALLY_SPAWNED, LookMode.Value);

			base.ExposeData();
		}
	}
}
