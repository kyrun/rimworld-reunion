using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.Grammar;

namespace Kyrun.Reunion
{
    static class Util
    {
        public const string DELIMITER = ", ";

        public static void Msg(object o)
        {
            Log.Message("[Reunion] " + o);
        }

        public static void Warn(object o)
        {
            Log.Warning("[Reunion] " + o);
        }

        public static void Error(object o)
        {
            Log.Error("[Reunion] " + o);
        }

        public static void PrintAllyList()
        {
            GameComponent gameComponent = GameComponent.CurrentGame;
            if (gameComponent == null) return;

            var str = "";
            foreach (var ally in gameComponent.ListAllyAvailable)
            {
                if (ally != null)
                {
                    str += ally.Name + DELIMITER;
                }
            }
            if (str != "") str = str.Substring(0, str.Length - DELIMITER.Length); // truncate last delimiter
            Msg("Ally Pawns: " + str);
        }

        public static void PrintNextEventTimeRemaining()
        {
            GameComponent gameComponent = GameComponent.CurrentGame;

            if (Prefs.DevMode && gameComponent != null && Find.TickManager != null)
            {
                var diff = gameComponent.NextEventTick - Find.TickManager.TicksGame;
                if (diff > 0)
                {
                    Msg("Next event in " + ((float)diff / GenDate.TicksPerDay).ToString("0.00") + " days");
                }
                else
                {
                    Msg("A Reunion event is happening now.");
                }
            }
        }

        public static Quest GenerateQuestAndMakeAvailable(QuestScriptDef root)
        {
            GameComponent gameComponent = GameComponent.CurrentGame;
            if (gameComponent == null || !gameComponent.HasAvailableAlly)
            {
                int availableEntries = gameComponent?.ListAllyAvailable?.Count ?? 0;
                Error("Cannot generate Reunion quest " + root.defName + ": no valid available Ally pawn. List entries: " + availableEntries + ".");
                return null;
            }

            Map map = null;
            if (Current.Game != null)
            {
                foreach (Map candidate in Current.Game.Maps)
                {
                    if (candidate.IsPlayerHome)
                    {
                        map = candidate;
                        break;
                    }
                }
            }

            if (map == null)
            {
                Warn("Cannot start Reunion event: no player home map available.");
                return null;
            }

            Slate slate = new Slate();
            slate.Set<Map>("map", map);
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(root, slate);
            if (quest == null)
                Warn("Failed to generate Reunion quest: " + root.defName + ".");

            return quest;
        }

        public static Pawn PrepareAllyForSitePart(SitePart part)
        {
            string source = part?.def?.defName ?? "unknown site part";

            // GetRandomAllyForSpawning is a potential failure point here because it does multiple things in it.
            // An Ally might be lost if something inside it, or SetupSpawn fails. This is unlikely, but worth noting.
            // TODO: Fix later, or not at all. Changes here are too drastic for a rare edge case.
            Pawn pawn = GameComponent.CurrentGame?.GetRandomAllyForSpawning(source);
            if (pawn == null)
            {
                Error("Failed to prepare Ally for site part " + source + ": no pawn was returned.");
                return null;
            }

            string stage = "generating starting apparel";
            try
            {
                DressPawnIfCold(pawn, part.site.Tile);

                stage = "creating site-part ThingOwner";
                ThingOwner<Pawn> things = new ThingOwner<Pawn>(part, true, LookMode.Deep);

                stage = "adding pawn to site-part ThingOwner";
                if (!things.TryAdd(pawn, true))
                {
                    GameComponent gameComponent = GameComponent.CurrentGame;
                    string loadId = pawn.GetUniqueLoadID();
                    Error("Failed to prepare Ally for site part " + source + " at stage '" + stage +
                        "'. Pawn: " + pawn.Name + " (" + loadId +
                        "), available: " + (gameComponent?.ListAllyAvailable?.Contains(pawn) ?? false) +
                        ", tracked as spawned: " + (gameComponent?.ListAllySpawned?.Contains(loadId) ?? false) +
                        ", spawned: " + pawn.Spawned + ", destroyed: " + pawn.Destroyed +
                        ", faction: " + pawn.Faction + ", map: " + pawn.Map +
                        ", owner count: " + things.Count + ".");
                    return null;
                }

                part.things = things;
                return pawn;
            }
            catch (System.Exception ex)
            {
                Error("Exception while preparing Ally for site part " + source + " at stage '" +
                    stage + "'. Pawn: " + pawn.Name + " (" + pawn.GetUniqueLoadID() +
                    "). Exception: " + ex);
                throw;
            }
        }

        // From virtual function (since calling base.Notify_GeneratedByQuestGen will run the parent and not the grandparent)
        public static void SitePartWorker_Base_Notify_GeneratedByQuestGen(SitePart part, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            outExtraDescriptionRules.AddRange(GrammarUtility.RulesForDef("", part.def));
            outExtraDescriptionConstants.Add("sitePart", part.def.defName);
        }

        public static void DressPawnIfCold(Pawn pawn, int tile)
        {
            // Generate pawns with specific settings
            PawnApparelGenerator.GenerateStartingApparelFor(pawn, new PawnGenerationRequest(
                pawn.kindDef, pawn.Faction, PawnGenerationContext.NonPlayer,
                tile,
                false, // forceGenerateNewPawn
                false, // allowDead
                false, // allowDowned
                false, // canGeneratePawnRelations (NOT DEFAULT)
                false, // mustBeCapableOfViolence
                0f, // colonistRelationChanceFactor (NOT DEFAULT)
                true, // forceAddFreeWarmLayerIfNeeded (NOT DEFAULT) - IMPORTANT: this makes pawns not die when spawned in a cold environment
                true, // allowGay
                true, // allowPregnant
                true, // allowFood
                true, // allowAddictions
                false, // inhabitant
                false, // certainlyBeenInCryptosleep
                false, // forceRedressWorldPawnIfFormerColonist
                true, // worldPawnFactionDoesntMatter (NOT DEFAULT)
                0f, // biocodeWeaponChance
                0f, // biocodeApparelChance
                null, // extraPawnForExtraRelationChance
                0f, // relationWithExtraPawnChanceFactor (NOT DEFAULT)
                null, // validatorPreGear
                null, // validatorPostGear
                null, // forcedTraits
                null, // prohibitedTraits
                new float?(0.0f), // minChanceToRedressWorldPawn (NOT DEFAULT)
                null, // fixedBiologicalAge
                null, // fixedChronologicalAge
                null, // fixedGender
                null, // fixedLastName
                null, // fixedBirthName
                null, // fixedTitle
                null, // fixedIdeo
                false, // forceNoIdeo
                false, // forceNoBackstory
                false, // forbidAnyTitle
                false, // forceDead
                null, // forcedXenogenes
                null, // forcedEndogenes
                null, // forcedXenotype
                null, // forcedCustomXenotype
                null, // allowedXenotypes
                0f, // forceBaselinerChance
                pawn.DevelopmentalStage, // developmentalStages (use pawn value)
                null, // pawnKindDefGetter
                null, // excludeBiologicalAgeRange
                null, // biologicalAgeRange
                false // forceRecruitable
                ));
        }

        public static void OnPostDestroyReschedule(SitePart sitePart)
        {
            GameComponent gameComponent = GameComponent.CurrentGame;
            if (gameComponent == null) return;

            // If recruited, will have been flagged on map generate: schedule will fire on DoRecruit
            // If abandoned but pawn alive, also will have been flagged on map generate: schedule will fire on PassToWorld

            // If quest failed without entering the map, flag here: schedule will fire on PassToWorld
            if (gameComponent.NextEventTick == -1) gameComponent.FlagNextEventReadyForScheduling();

            if (sitePart.things != null && sitePart.things.Any)
            {
                Pawn pawn = (Pawn)sitePart.things[0];
                // If pawn is dead on post destroy: schedule it HERE!
                if (pawn.Dead)
                {
                    gameComponent.TryScheduleNextEvent();
                }
            }
            // No "thing" in SitePart, pawn is recruited, or something else happened.
            // As mentioned above, if recruited, schedule will already have fired on DoRecruit.
            // If not recruited for whatever reason (likely dead, according to tests): schedule it HERE!
            else if (gameComponent.NextEventTick == 0) gameComponent.TryScheduleNextEvent();
        }
    }
}
