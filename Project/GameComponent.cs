using System;
using System.Collections.Generic;
using HarmonyLib;
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
        public List<Pawn> ListAllyAvailable = new List<Pawn>();
        public List<string> ListAllySpawned = new List<string>();

        // Variables
        public int NextEventTick = 0;

        // Resolve the component belonging to the currently active game.
        public static GameComponent CurrentGame => Current.Game?.GetComponent<GameComponent>();

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

        // Trait instances are mutable and hold a reference to their owning pawn.
        // Return a fresh instance for each dropdown so separate pawns never share one.
        public static IReadOnlyList<Trait> ReunionTraits =>
            new List<Trait>
            {
                new Trait(TraitDef_Character, TRAIT_DEGREE_ALLY, true)
            };

        // Save key
        const string SAVE_NEXT_EVENT_TICK = "Reunion_NextEventTick";
        const string SAVE_KEY_LIST_ALLY_AVAILABLE = "Reunion_AllyAvailable";
        const string SAVE_KEY_LIST_ALLY_SPAWNED = "Reunion_AllySpawned";

        public static Settings Settings { get; private set; }


        public GameComponent(Game game) : base()
        {
            Settings = LoadedModManager.GetMod<Mod>().GetSettings<Settings>();
        }

        // Use the GameComponent lifecycle instead of patching Game.InitNewGame
        // and Game.LoadGame. PostInit runs after the new-game/load callback,
        // when the game state has been restored or generated.
        public override void StartedNewGame()
        {
            base.StartedNewGame();

            InitOnNewGame();
            PostInit();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();

            InitOnLoad();
            PostInit();
        }

        // GameComponentTick is already called by the game each simulation tick.
        // This replaces the global Harmony patch on TickManager.DoSingleTick.
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (NextEventTick <= 0 || Find.TickManager == null)
                return;

            if (Find.TickManager.TicksGame >= NextEventTick)
                DecideAndDoEvent();
        }


        public void InitOnNewGame()
        {
#if TESTING
			const int TOTAL = 5;
            Util.Warn("TESTING mode enabled. Generating " + TOTAL + " random pawns with Ally trait for Reunion events.");
			for (int i = 0; i < TOTAL; ++i)
			{
				var pgr = new PawnGenerationRequest(PawnKindDef.Named("SpaceRefugee"), null,
					PawnGenerationContext.NonPlayer, -1, true);
				var newPawn = PawnGenerator.GeneratePawn(pgr);
				newPawn.Name = NameTriple.FromString("ReunionPawn" + i);
                AddPawnToAvailableList(newPawn);
			}
#endif
        }


        public void InitOnLoad()
        {
            if (ListAllyAvailable == null) ListAllyAvailable = new List<Pawn>();
            if (ListAllySpawned == null) ListAllySpawned = new List<string>();

            if (NextEventTick == 0 && ListAllyAvailable.Count > 0)
            {
                Util.Msg("No events scheduled, but there is at least 1 available pawn.");
                TryScheduleNextEvent(ScheduleMode.Normal);
            }
            else if (ListAllyAvailable.Count > 0)
            {
                Util.PrintNextEventTimeRemaining();
            }
            else
            {
                Util.Msg("Game loaded, no pawns in Ally list.");
            }
        }


        public void PostInit()
        {
            // Check player's existing pawns with Ally trait and put into list for saving.
            // This means that starting pawns (as opposed to "left behind" pawns) with the Ally trait
            // will be given the Reunion treatment if they are ever lost (passed to World pool).
            // This feature is not documented but if you are reading this comment off the repo, this is your reward. :)
            RegisterReunionPawnsFromList(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction, (pawn) =>
            {
                if (!ListAllySpawned.Contains(pawn.GetUniqueLoadID()))
                {
                    ListAllySpawned.Add(pawn.GetUniqueLoadID());
                    Util.Msg("Saved Player's pawn with Ally trait to Reunion list: " + pawn.Name);
                }
            });

            // Check all World Pawns with trait and put into list for saving.
            // Use case 1: New game creates STARTING World pawns flagged with Ally trait during creation.
            // Use case 2: Backwards compatibility for loading existing saves from older version of this mod.
            RegisterReunionPawnsFromList(Current.Game.World.worldPawns.AllPawnsAlive, (pawn) =>
            {
                AddPawnToAvailableList(pawn);
                Find.WorldPawns.RemovePawn(pawn);
            });

            if (ListAllyAvailable.Count > 0)
            {
                Util.PrintAllyList();
            }

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


        public void AddPawnToAvailableList(Pawn pawn)
        {
            if (pawn != null && !pawn.Dead && !pawn.Destroyed && !ListAllyAvailable.Contains(pawn))
            {
                if (pawn.health != null)
                {
                    // Tend to hediffs require tending
                    while (pawn.health.HasHediffsNeedingTend())
                    {
                        TendUtility.DoTend(null, pawn, null);
                    }
                }

                string pawnSavedMsg = "Saved World pawn with Ally trait to Reunion list: " + pawn.Name;

                // Substract the game-time passed from the pawns age during the time of saving.
                // This gives us the pawns age at the start of a new game.
                // We ONLY ever save the pawns age at the start of a new game.
                // When the pawn is restored, the age can then be back-calculated.
                if (pawn.ageTracker != null)
                {
                    pawn.ageTracker.AgeBiologicalTicks -= GenTicks.TicksAbs;
                    pawnSavedMsg += ", age " + pawn.ageTracker.AgeBiologicalYearsFloat.ToString("0.00");
                }

                Util.Msg(pawnSavedMsg);

                ListAllyAvailable.Add(pawn);
            }
        }


        public bool HasAvailableAlly =>
            ListAllyAvailable != null &&
            ListAllyAvailable.Exists(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed);


        public Pawn GetRandomAllyForSpawning(string sourceDebugName)
        {
            if (ListAllyAvailable == null || ListAllyAvailable.Count == 0)
            {
                Util.Error("Failed to get Ally for " + sourceDebugName +
                    ": the available Ally list is null or empty.");
                return null;
            }

            int invalidCount = ListAllyAvailable.RemoveAll(pawn =>
                pawn == null || pawn.Dead || pawn.Destroyed);

            if (invalidCount > 0)
            {
                Util.Warn("Removed " + invalidCount +
                    " null, dead, or destroyed pawn(s) from the available Ally list while preparing " +
                    sourceDebugName + ".");
            }

            if (ListAllyAvailable.Count == 0)
            {
                Util.Error("Failed to get Ally while preparing " + sourceDebugName +
                    ": the available Ally list contains no living, valid pawns.");
                return null;
            }

            Pawn pawn = ListAllyAvailable[Random.Range(0, ListAllyAvailable.Count)];
            SetupSpawn(pawn);
            return pawn;
        }


        public static bool TryRemoveTrait(Pawn pawn)
        {
            var trait = pawn.story.traits.GetTrait(TraitDef_Character);

            if (trait != null)
            {
                pawn.story.traits.RemoveTrait(trait);
                return true;
            }

            return false;
        }


        public void SetupSpawn(Pawn pawn)
        {
            ListAllyAvailable.Remove(pawn);
            ListAllySpawned.Add(pawn.GetUniqueLoadID());
            pawn.SetFactionDirect(null); // remove faction, if any

            if (Find.TickManager != null && pawn != null && pawn.ageTracker != null)
            {
                // The pawn is only ever saved with the age at the start of the game.
                // So whenever we setup pawn for spawning, just add back game-time passed.
                pawn.ageTracker.AgeBiologicalTicks += GenTicks.TicksAbs;
            }

            if (pawn.health != null)
            {
                if (pawn.health.capacities != null)
                {
                    pawn.health.capacities.Clear();
                }

                pawn.health.CheckForStateChange(null, null);

                if (pawn.health.hediffSet != null)
                {
                    pawn.health.hediffSet.DirtyCache();
                }
            }

            pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized(); // prevent some error which I don't yet understand
        }


        public void ReturnToAvailable(Pawn pawn)
        {
            ListAllySpawned.Remove(pawn.GetUniqueLoadID());

            if (pawn.health != null && pawn.health.hediffSet != null && pawn.health.hediffSet.hediffs != null)
            {
                HediffSet hediffSet = pawn.health.hediffSet;
                for (int i = 0; i < hediffSet.hediffs.Count; ++i)
                {
                    Hediff hediff = hediffSet.hediffs[i];
                    if (hediff is Hediff_MissingPart hediffMissingPart)
                    {
                        if (hediffMissingPart.IsFresh)
                        {
                            Util.Warn(pawn.Name + " was lost by the player while having a fresh missing part. The pawn has bled to death and is lost forever.");
                            return;
                        }
                    }
                }
            }

            AddPawnToAvailableList(pawn);

            Util.Msg(pawn.Name + " was lost by the player and made available for Reunion to spawn again.");

            TryScheduleNextEvent();
        }


        public void FlagNextEventReadyForScheduling()
        {
            NextEventTick = 0;
        }


        public void TryScheduleNextEvent(ScheduleMode mode = ScheduleMode.Normal)
        {
            if (ListAllyAvailable.Count == 0)
            {
                FlagNextEventReadyForScheduling();
                if (mode != ScheduleMode.Init) Util.Msg("No available Reunion pawns, Reunion events will not fire from now on.");
                return;
            }

            if (!EventSchedulePolicy.ShouldSchedule(
                NextEventTick,
                Find.TickManager.TicksGame,
                mode == ScheduleMode.Forced,
                mode == ScheduleMode.SettingsUpdated))
            {
                if (Prefs.DevMode && mode != ScheduleMode.Init)
                {
                    if (NextEventTick == -1)
                        Util.Msg("Tried to schedule an event but is in the middle of an event.");
                    else
                        Util.Msg("Tried to schedule an event but another event has already been scheduled.");
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


        public void DecideAndDoEvent()
        {
            NextEventTick = -1; // stop next event from happening

            if (ListAllyAvailable.Count <= 0) // no more allies
            {
                Util.Error("No available Ally pawns, event should not have fired!");

                FlagNextEventReadyForScheduling();

                return;
            }
            if (Current.Game.AnyPlayerHomeMap == null) // no map
            {
                Util.Msg("Player does not have any home map, event timer restarted.");
                FlagNextEventReadyForScheduling();
                TryScheduleNextEvent();
                return;
            }

            List<Settings.Event> listAllowedEvents = AllyEventSelection.GetAllowedEvents(
                (Settings.Event[])Enum.GetValues(typeof(Settings.Event)),
                eventType => Settings.EventAllow[eventType],
                new[]
                {
                    Settings.Event.WandererJoins,
                    Settings.Event.RefugeePodCrash,
                    Settings.Event.RefugeeChased
                },
                PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.Count,
                Settings.enableHarderEventsWhenSolo);

            if (listAllowedEvents.Count > 0)
            {
                var eventToDo = listAllowedEvents[Random.Range(0, listAllowedEvents.Count)];
                bool eventStarted = Settings.EventAction[eventToDo].Invoke();

                // Known issue: Exception caused by event invocation above will cause NextEventTick
                // to remain at -1. Unlikely to happen, so will not fix. If it happens, the game has bigger problems.

                if (!eventStarted)
                {
                    Util.Warn("Failed to start Reunion event: " + eventToDo + ". Event timer restarted.");
                    FlagNextEventReadyForScheduling();
                    TryScheduleNextEvent();
                }
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

            // Begin unpatching MVCF
            const string MVCF_ID = "legodude17.mvcf";
            var original = Harmony.HasAnyPatches(MVCF_ID) ? typeof(Pawn).GetMethod(nameof(Pawn.ExposeData)) : null;
            Patches patches;
            Patch patch = null;
            if (original != null)
            {
                patches = Harmony.GetPatchInfo(original);
                if (patches != null)
                {
                    if (patches.Prefixes != null && patches.Prefixes.Count > 0)
                    {
                        Util.Warn("MVCF Pawn.ExposeData now has Prefix patches! This might cause issues.");
                    }
                    if (patches.Transpilers != null && patches.Transpilers.Count > 0)
                    {
                        Util.Warn("MVCF Pawn.ExposeData now has Transpiler patches! This might cause issues.");
                    }
                    if (patches.Finalizers != null && patches.Finalizers.Count > 0)
                    {
                        Util.Warn("MVCF Pawn.ExposeData now has Finalizers patches! This might cause issues.");
                    }

                    if (patches.Postfixes != null && patches.Postfixes.Count > 0)
                    {
                        patch = patches.Postfixes[0];
                    }
                }
                Main.HarmonyInstance.Unpatch(original, HarmonyPatchType.Postfix, MVCF_ID);
            }
            // End unpatching MVCF

            Scribe_Collections.Look(ref ListAllyAvailable, SAVE_KEY_LIST_ALLY_AVAILABLE, LookMode.Deep);
            Scribe_Collections.Look(ref ListAllySpawned, SAVE_KEY_LIST_ALLY_SPAWNED, LookMode.Value);

            // Begin repatching MVCF
            if (original != null && patch != null)
            {
                var mvcfHarmony = new Harmony(MVCF_ID);
                mvcfHarmony.Patch(original, null,
                        new HarmonyMethod(patch.PatchMethod, patch.priority, patch.before, patch.after, patch.debug),
                        null, null);
            }
            // End repatching MVCF

            base.ExposeData();
        }
    }
}
