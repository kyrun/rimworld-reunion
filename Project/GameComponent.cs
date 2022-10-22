using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Random = UnityEngine.Random;

namespace Kyrun.Reunion
{
	public enum ScheduleMode
	{
		Normal,
		Init, // suppress messages so it doesn't look like there's a problem
		SettingsUpdated,
		Forced
	}

	class GameComponent : Verse.GameComponent
	{
		public static List<Pawn> ListAllyAvailable = new List<Pawn>();
		public static List<string> ListAllySpawned = new List<string>();

		// Variables
		public static int NextEventTick = 0;

		// Trait-related
		public const string TRAIT_DEF_CHARACTER = "ReunionCharacter";
		public const int TRAIT_DEGREE_ALLY = 3;

		static TraitDef m_TraitDefCharacter;
		public static TraitDef TraitDef_Character
		{
			get
			{
                if (m_TraitDefCharacter == null)
				{
                    m_TraitDefCharacter = TraitDef.Named(TRAIT_DEF_CHARACTER);
                }
				return m_TraitDefCharacter;
            }
		}

		static Trait m_TraitAlly;
		public static Trait Trait_Ally
        {
            get
            {
                if (m_TraitAlly == null)
                {
                    m_TraitAlly = new Trait(TraitDef_Character, TRAIT_DEGREE_ALLY, true);
                }
                return m_TraitAlly;
            }
        }

        public static IReadOnlyList<Trait> ReunionTraits
		{
			get
			{
				return new List<Trait>()
				{
					Trait_Ally
				};
			}
		}

		// Save key
		const string SAVE_NEXT_EVENT_TICK = "Reunion_NextEventTick";
		const string SAVE_KEY_LIST_ALLY_SPAWNED = "Reunion_AllySpawned";
		const string SAVE_KEY_LIST_ALLY_AVAILABLE = "Reunion_AllyAvailable";

		public static Settings Settings { get; private set; }


		public GameComponent(Game game) : base()
		{
			Settings = LoadedModManager.GetMod<Mod>().GetSettings<Settings>();
        }


		public static void InitOnNewGame()
		{
			ListAllyAvailable.Clear();
			ListAllySpawned.Clear();
			NextEventTick = 0;
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

			if (NextEventTick <= 0) Util.Msg("No events scheduled");
			else Util.PrintNextEventTimeRemaining();
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
			});

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
			});

			if (Prefs.DevMode) Util.PrintAllyList();

			TryScheduleNextEvent(ScheduleMode.Init);
		}


		static void RegisterReunionPawnsFromList(List<Pawn> listPawns, Action<Pawn> doToPawn)
		{
			foreach (var pawn in listPawns)
			{
				if (pawn == null || pawn.story == null || pawn.story.traits == null) continue;

				var traits = pawn.story.traits;
				if (traits.HasTrait(TraitDef_Character))
				{
					var trait = traits.GetTrait(TraitDef_Character);
					if (trait != null)
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

			TryScheduleNextEvent();
		}


		public static void FlagNextEventReadyForScheduling()
		{
			NextEventTick = 0;
		}


		public static void TryScheduleNextEvent(ScheduleMode mode = ScheduleMode.Normal)
		{
			if (ListAllyAvailable.Count == 0)
			{
				if (mode != ScheduleMode.Init) Util.Msg("No available Reunion pawns, Reunion events will not fire from now on.");
				return;
			}

			if (mode != ScheduleMode.Forced && // don't check if forced
				NextEventTick == -1)
			{
				// another event is currently happening
				if (Prefs.DevMode)
				{
					if (mode != ScheduleMode.Init) Util.Msg("Tried to schedule an event but is in the middle of an event.");
				}

				return;
			}

			if (mode != ScheduleMode.Forced && // don't check if forced
				mode != ScheduleMode.SettingsUpdated && // only updating settings will force a reschedule
				NextEventTick > Find.TickManager.TicksGame)
			{
				// another event is already scheduled
				if (Prefs.DevMode)
				{
					if (mode != ScheduleMode.Init) Util.Msg("Tried to schedule an event but another event has already been scheduled.");
				}
				return;
			}

			var min = Settings.minDaysBetweenEvents * GenDate.TicksPerDay;
			if (min == 0) min = 1; // limit it to at least 1 tick
			var max = Settings.maxDaysBetweenEvents * GenDate.TicksPerDay;
			NextEventTick = Find.TickManager.TicksGame + Random.Range(min, max);

#if TESTING
			NextEventTick = Find.TickManager.TicksGame + 1000;
#endif
			Util.PrintNextEventTimeRemaining();
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
				FlagNextEventReadyForScheduling();
				TryScheduleNextEvent();
				return;
			}

			List<Settings.Event> listAllowedEvents = new List<Settings.Event>();

			if (Settings.EventAllow[Settings.Event.WandererJoins]) listAllowedEvents.Add(Settings.Event.WandererJoins);
			if (Settings.EventAllow[Settings.Event.RefugeePodCrash]) listAllowedEvents.Add(Settings.Event.RefugeePodCrash);
			if (Settings.EventAllow[Settings.Event.RefugeeChased]) listAllowedEvents.Add(Settings.Event.RefugeeChased);

			// add the rest of the allowed events if more than 1 Colonist
			if (Settings.enableHarderEventsWhenSolo || // if option for harder solo is turned on
				listAllowedEvents.Count == 0 || // or the player has turned off both the one-colonist events
				PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction.Count > 1)  // this is the actual condition
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
				TryScheduleNextEvent(ScheduleMode.Forced);
			}
		}


		public override void ExposeData()
		{
			Scribe_Values.Look(ref NextEventTick, SAVE_NEXT_EVENT_TICK, 0);
			Scribe_Collections.Look(ref ListAllyAvailable, SAVE_KEY_LIST_ALLY_AVAILABLE, LookMode.Deep);
			Scribe_Collections.Look(ref ListAllySpawned, SAVE_KEY_LIST_ALLY_SPAWNED, LookMode.Value);

			base.ExposeData();
		}
	}
}
